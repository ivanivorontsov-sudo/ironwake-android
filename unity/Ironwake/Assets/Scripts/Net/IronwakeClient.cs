using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Ironwake.Net
{
    /// <summary>
    /// Flexible net client for the evolving ironwake-server protocol.
    /// Prefers WebSocket (/ws); falls back to HTTP poll (/room/*) because
    /// Beget nginx currently blocks WS (health reports ws:"blocked-on-beget").
    /// Default base: http://biker9td.beget.tech
    /// </summary>
    public sealed class IronwakeClient : MonoBehaviour
    {
        public static IronwakeClient Instance { get; private set; }

        [Header("Server")]
        [SerializeField] string baseUrl = "http://biker9td.beget.tech";
        [SerializeField] float httpPollHz = 8f;
        [SerializeField] float inputHz = 12f;
        [SerializeField] bool preferWebSocket = true;

        public string BaseUrl => baseUrl.TrimEnd('/');
        public string UserId { get; private set; }
        public string RoomId { get; private set; } = "public";
        public string Team { get; private set; } = "blue";
        public bool Joined { get; private set; }
        public bool UsingWebSocket { get; private set; }
        public string LastStatus { get; private set; } = "idle";

        public event Action<RoomStatePayload> OnState;
        public event Action<string> OnStatus;
        public event Action OnDisconnected;

        ClientWebSocket _ws;
        CancellationTokenSource _wsCts;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        Coroutine _httpLoop;
        Coroutine _inputLoop;
        InputFrame _pendingInput;
        bool _hasPendingInput;
        readonly object _inputLock = new object();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (string.IsNullOrEmpty(UserId))
                UserId = "u" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        void Update()
        {
            while (_incoming.TryDequeue(out var raw))
                HandleIncomingJson(raw);
        }

        void OnDestroy()
        {
            Disconnect();
            if (Instance == this) Instance = null;
        }

        public void Configure(string url, string userId = null, string room = null)
        {
            if (!string.IsNullOrEmpty(url)) baseUrl = url.TrimEnd('/');
            if (!string.IsNullOrEmpty(userId)) UserId = userId;
            if (!string.IsNullOrEmpty(room)) RoomId = room;
        }

        public IEnumerator HealthCheck(Action<bool, string> done)
        {
            using var req = UnityWebRequest.Get($"{BaseUrl}/health");
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"health fail: {req.error}");
                done?.Invoke(false, req.error);
                yield break;
            }
            SetStatus($"health ok: {req.downloadHandler.text}");
            done?.Invoke(true, req.downloadHandler.text);
        }

        public IEnumerator Join(string callsign, string vehicleId, string mode = "laststand", Action<bool> done = null)
        {
            Disconnect();
            RoomId = string.IsNullOrEmpty(RoomId) ? "public" : RoomId;

            bool wsOk = false;
            if (preferWebSocket)
            {
                var joinTask = ConnectWsAndJoinAsync(callsign, vehicleId, mode);
                while (!joinTask.IsCompleted) yield return null;
                wsOk = joinTask.Status == TaskStatus.RanToCompletion && joinTask.Result;
            }

            if (wsOk)
            {
                UsingWebSocket = true;
                Joined = true;
                SetStatus($"ws joined room={RoomId} team={Team}");
                StartInputPump();
                done?.Invoke(true);
                yield break;
            }

            // HTTP fallback (primary path on Beget today)
            UsingWebSocket = false;
            var body = $"{{\"room\":\"{Escape(RoomId)}\",\"mode\":\"{Escape(mode)}\",\"userId\":\"{Escape(UserId)}\",\"callsign\":\"{Escape(callsign)}\",\"vehicleId\":\"{Escape(vehicleId)}\"}}";
            using (var req = new UnityWebRequest($"{BaseUrl}/room/join", "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(body);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"join fail: {req.error}");
                    Joined = false;
                    done?.Invoke(false);
                    yield break;
                }
                ParseJoinResponse(req.downloadHandler.text);
            }

            Joined = true;
            SetStatus($"http joined room={RoomId} team={Team}");
            _httpLoop = StartCoroutine(HttpPollLoop());
            StartInputPump();
            done?.Invoke(true);
        }

        public void SendInput(InputFrame frame)
        {
            lock (_inputLock)
            {
                _pendingInput = frame;
                _hasPendingInput = true;
            }
        }

        public void Disconnect()
        {
            if (_httpLoop != null) { StopCoroutine(_httpLoop); _httpLoop = null; }
            if (_inputLoop != null) { StopCoroutine(_inputLoop); _inputLoop = null; }
            try
            {
                _wsCts?.Cancel();
                if (_ws != null)
                {
                    if (_ws.State == WebSocketState.Open)
                        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(500);
                    _ws.Dispose();
                }
            }
            catch { /* ignore */ }
            _ws = null;
            _wsCts = null;
            Joined = false;
            UsingWebSocket = false;
            OnDisconnected?.Invoke();
        }

        void StartInputPump()
        {
            if (_inputLoop != null) StopCoroutine(_inputLoop);
            _inputLoop = StartCoroutine(InputPump());
        }

        IEnumerator InputPump()
        {
            var wait = new WaitForSeconds(1f / Mathf.Max(1f, inputHz));
            while (Joined)
            {
                InputFrame frame;
                bool send;
                lock (_inputLock)
                {
                    send = _hasPendingInput;
                    frame = _pendingInput;
                    _hasPendingInput = false;
                }
                if (send)
                {
                    if (UsingWebSocket) _ = SendWsInputAsync(frame);
                    else yield return PostHttpInput(frame);
                }
                yield return wait;
            }
        }

        IEnumerator HttpPollLoop()
        {
            var wait = new WaitForSeconds(1f / Mathf.Max(1f, httpPollHz));
            while (Joined && !UsingWebSocket)
            {
                using var req = UnityWebRequest.Get($"{BaseUrl}/room/state?room={UnityWebRequest.EscapeURL(RoomId)}");
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    HandleIncomingJson(req.downloadHandler.text);
                yield return wait;
            }
        }

        IEnumerator PostHttpInput(InputFrame frame)
        {
            string json = BuildInputJson(frame, httpEnvelope: true);
            using var req = new UnityWebRequest($"{BaseUrl}/room/input", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;
            yield return req.SendWebRequest();
        }

        async Task<bool> ConnectWsAndJoinAsync(string callsign, string vehicleId, string mode)
        {
            try
            {
                var uri = new Uri(BaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/ws");
                _wsCts = new CancellationTokenSource();
                _ws = new ClientWebSocket();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                await _ws.ConnectAsync(uri, timeout.Token);
                string join = $"{{\"type\":\"join\",\"room\":\"{Escape(RoomId)}\",\"mode\":\"{Escape(mode)}\",\"userId\":\"{Escape(UserId)}\",\"callsign\":\"{Escape(callsign)}\",\"vehicleId\":\"{Escape(vehicleId)}\"}}";
                await _ws.SendAsync(Encoding.UTF8.GetBytes(join), WebSocketMessageType.Text, true, _wsCts.Token);
                _ = ReceiveLoopAsync(_wsCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"ws unavailable: {ex.Message}");
                try { _ws?.Dispose(); } catch { }
                _ws = null;
                return false;
            }
        }

        async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buf = new byte[64 * 1024];
            var sb = new StringBuilder();
            try
            {
                while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
                    } while (!result.EndOfMessage);
                    _incoming.Enqueue(sb.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetStatus($"ws recv error: {ex.Message}");
            }
        }

        async Task SendWsInputAsync(InputFrame frame)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            try
            {
                string json = BuildInputJson(frame, httpEnvelope: false);
                await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, _wsCts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                SetStatus($"ws send fail: {ex.Message}");
            }
        }

        string BuildInputJson(InputFrame f, bool httpEnvelope)
        {
            var hit = f.HasHit
                ? $",\"hit\":{{\"target\":\"{Escape(f.HitTarget)}\",\"module\":\"{Escape(f.HitModule)}\",\"damage\":{f.HitDamage.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"
                : "";
            if (httpEnvelope)
            {
                return $"{{\"room\":\"{Escape(RoomId)}\",\"userId\":\"{Escape(UserId)}\",\"x\":{F(f.X)},\"y\":{F(f.Y)},\"z\":{F(f.Z)},\"yaw\":{F(f.Yaw)},\"turretYaw\":{F(f.TurretYaw)},\"gunPitch\":{F(f.GunPitch)}{hit}}}";
            }
            return $"{{\"type\":\"input\",\"x\":{F(f.X)},\"y\":{F(f.Y)},\"z\":{F(f.Z)},\"yaw\":{F(f.Yaw)},\"turretYaw\":{F(f.TurretYaw)},\"gunPitch\":{F(f.GunPitch)}{hit}}}";
        }

        void ParseJoinResponse(string json)
        {
            // Minimal tolerant parse — protocol may evolve.
            if (TryExtractString(json, "team", out var team)) Team = team;
            if (TryExtractString(json, "id", out var id) && !string.IsNullOrEmpty(id)) UserId = id;
            if (TryExtractString(json, "room", out var room)) RoomId = room;
        }

        void HandleIncomingJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            // HTTP /room/state and WS state share a flexible shape.
            var payload = RoomStatePayload.ParseFlexible(json);
            if (payload != null) OnState?.Invoke(payload);
        }

        void SetStatus(string s)
        {
            LastStatus = s;
            OnStatus?.Invoke(s);
            Debug.Log($"[IronwakeClient] {s}");
        }

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static bool TryExtractString(string json, string key, out string value)
        {
            value = null;
            string needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return false;
            int q1 = json.IndexOf('"', i + 1);
            if (q1 < 0) return false;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return false;
            value = json.Substring(q1 + 1, q2 - q1 - 1);
            return true;
        }
    }

    [Serializable]
    public struct InputFrame
    {
        public float X, Y, Z, Yaw, TurretYaw, GunPitch;
        public bool HasHit;
        public string HitTarget;
        public string HitModule;
        public float HitDamage;
    }

    [Serializable]
    public class RoomStatePayload
    {
        public long T;
        public UnitSnapshot[] Units = Array.Empty<UnitSnapshot>();
        public bool Ended;
        public string Raw;

        public static RoomStatePayload ParseFlexible(string json)
        {
            // Intentionally loose: server may nest under payload or send flat.
            var p = new RoomStatePayload { Raw = json };
            p.Ended = json.IndexOf("\"ended\":true", StringComparison.OrdinalIgnoreCase) >= 0
                   || json.IndexOf("\"ended\": true", StringComparison.OrdinalIgnoreCase) >= 0;
            // Full JsonUtility deserialization needs matching DTOs; for scaffold we keep Raw
            // and let presenters scan. Replace with proper serializer when protocol freezes.
            try
            {
                // Attempt nested: {"type":"state","payload":{"units":[...]}}
                string unitsBlock = ExtractArrayBlock(json, "units");
                if (!string.IsNullOrEmpty(unitsBlock))
                    p.Units = ParseUnitsRough(unitsBlock);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IronwakeClient] parse units: {ex.Message}");
            }
            return p;
        }

        static string ExtractArrayBlock(string json, string key)
        {
            int k = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0) return null;
            int start = json.IndexOf('[', k);
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        static UnitSnapshot[] ParseUnitsRough(string arrayJson)
        {
            // Split on "},{" boundaries — good enough for scaffold / live testing.
            var list = new System.Collections.Generic.List<UnitSnapshot>();
            string inner = arrayJson.Trim().TrimStart('[').TrimEnd(']');
            if (string.IsNullOrWhiteSpace(inner)) return Array.Empty<UnitSnapshot>();
            string[] parts = System.Text.RegularExpressions.Regex.Split(inner, @"\}\s*,\s*\{");
            foreach (var part in parts)
            {
                string obj = part.Trim();
                if (!obj.StartsWith("{")) obj = "{" + obj;
                if (!obj.EndsWith("}")) obj = obj + "}";
                var u = new UnitSnapshot();
                TryNum(obj, "x", out u.X);
                TryNum(obj, "y", out u.Y);
                TryNum(obj, "z", out u.Z);
                TryNum(obj, "yaw", out u.Yaw);
                TryNum(obj, "hp", out u.Hp);
                TryStr(obj, "id", out u.Id);
                TryStr(obj, "team", out u.Team);
                TryStr(obj, "callsign", out u.Callsign);
                TryStr(obj, "vehicleId", out u.VehicleId);
                if (string.IsNullOrEmpty(u.VehicleId)) TryStr(obj, "defId", out u.VehicleId);
                u.Alive = obj.IndexOf("\"alive\":false", StringComparison.OrdinalIgnoreCase) < 0
                       && obj.IndexOf("\"alive\": false", StringComparison.OrdinalIgnoreCase) < 0;
                list.Add(u);
            }
            return list.ToArray();
        }

        static void TryNum(string json, string key, out float v)
        {
            v = 0;
            string needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return;
            int j = i + 1;
            while (j < json.Length && (char.IsWhiteSpace(json[j]) || json[j] == '+')) j++;
            int k = j;
            while (k < json.Length && "0123456789.-+eE".IndexOf(json[k]) >= 0) k++;
            float.TryParse(json.Substring(j, k - j), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        static void TryStr(string json, string key, out string v)
        {
            v = null;
            string needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return;
            int q1 = json.IndexOf('"', i + 1);
            if (q1 < 0) return;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return;
            v = json.Substring(q1 + 1, q2 - q1 - 1);
        }
    }

    [Serializable]
    public class UnitSnapshot
    {
        public string Id;
        public string Team;
        public string Callsign;
        public string VehicleId;
        public float X, Y, Z, Yaw, Hp;
        public bool Alive = true;
    }
}
