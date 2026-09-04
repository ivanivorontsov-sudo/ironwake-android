using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Ironwake.Net
{
    /// <summary>
    /// Authoritative-protocol client. Prefers WS /ws; on Beget nginx WS is blocked
    /// so production path is HTTP: POST /room/join, POST /room/input, GET /room/state
    /// polled at ~10–20 Hz. Clients send ONLY control inputs — never hp/hit/damage.
    /// </summary>
    public sealed class IronwakeClient : MonoBehaviour
    {
        public static IronwakeClient Instance { get; private set; }

        [Header("Server")]
        [SerializeField] string baseUrl = "http://biker9td.beget.tech";
        [SerializeField] float httpPollHz = 15f;
        [SerializeField] float inputHz = 15f;
        [SerializeField] bool preferWebSocket = true;

        public string BaseUrl => baseUrl.TrimEnd('/');
        public string UserId { get; private set; }
        public string RoomId { get; private set; } = "public";
        public string Team { get; private set; } = "blue";
        public string VehicleId { get; private set; }
        public bool Joined { get; private set; }
        public bool UsingWebSocket { get; private set; }
        public string LastStatus { get; private set; } = "idle";
        public RoomStatePayload LastState { get; private set; }

        public event Action<RoomStatePayload> OnState;
        public event Action<GameEvent> OnGameEvent;
        public event Action<string> OnStatus;
        public event Action OnDisconnected;
        public event Action OnJoined;
        public event Action<string> OnMatchEnd; // winner team or null

        ClientWebSocket _ws;
        CancellationTokenSource _wsCts;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        Coroutine _httpLoop;
        Coroutine _inputLoop;
        InputFrame _pendingInput;
        bool _hasPendingInput;
        readonly object _inputLock = new object();
        long _lastEventT;
        readonly HashSet<string> _seenEventKeys = new HashSet<string>();

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

        public IEnumerator FetchUser(string userId, Action<UserProfile> done)
        {
            string id = string.IsNullOrEmpty(userId) ? UserId : userId;
            using var req = UnityWebRequest.Get($"{BaseUrl}/user?userId={UnityWebRequest.EscapeURL(id)}");
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                done?.Invoke(null);
                yield break;
            }
            done?.Invoke(UserProfile.Parse(req.downloadHandler.text));
        }

        public IEnumerator Join(string callsign, string vehicleId, string mode = "laststand", Action<bool> done = null)
        {
            Disconnect();
            RoomId = string.IsNullOrEmpty(RoomId) ? "public" : RoomId;
            VehicleId = vehicleId;
            _lastEventT = 0;
            _seenEventKeys.Clear();

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
                OnJoined?.Invoke();
                done?.Invoke(true);
                yield break;
            }

            UsingWebSocket = false;
            var body =
                $"{{\"room\":\"{Escape(RoomId)}\",\"mode\":\"{Escape(mode)}\",\"userId\":\"{Escape(UserId)}\",\"callsign\":\"{Escape(callsign)}\",\"vehicleId\":\"{Escape(vehicleId)}\",\"team\":\"{Escape(Team)}\"}}";
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
            SetStatus($"http joined room={RoomId} team={Team} poll={httpPollHz:0.#}Hz");
            _httpLoop = StartCoroutine(HttpPollLoop());
            StartInputPump();
            OnJoined?.Invoke();
            done?.Invoke(true);
        }

        /// <summary>Queue control-only input. hp/hit/damage must never be sent.</summary>
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
                    // Keep last frame sticky for continuous throttle on HTTP
                    // but only fire once until next press from caller.
                }
                if (send)
                {
                    if (UsingWebSocket) _ = SendWsInputAsync(frame);
                    else yield return PostHttpInput(frame);
                    // Clear fire pulse after send so we don't auto-fire
                    lock (_inputLock)
                    {
                        if (_pendingInput.Fire && frame.Fire)
                        {
                            _pendingInput.Fire = false;
                        }
                    }
                }
                yield return wait;
            }
        }

        IEnumerator HttpPollLoop()
        {
            float hz = Mathf.Clamp(httpPollHz, 10f, 20f);
            var wait = new WaitForSeconds(1f / hz);
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
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _ws.ConnectAsync(uri, timeout.Token);
                string join =
                    $"{{\"type\":\"join\",\"room\":\"{Escape(RoomId)}\",\"mode\":\"{Escape(mode)}\",\"userId\":\"{Escape(UserId)}\",\"callsign\":\"{Escape(callsign)}\",\"vehicleId\":\"{Escape(vehicleId)}\"}}";
                await _ws.SendAsync(Encoding.UTF8.GetBytes(join), WebSocketMessageType.Text, true, _wsCts.Token);
                _ = ReceiveLoopAsync(_wsCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"ws unavailable → HTTP poll: {ex.Message}");
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
                await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true,
                    _wsCts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                SetStatus($"ws send fail: {ex.Message}");
            }
        }

        /// <summary>
        /// Control fields only — matches PROTOCOL.md. Never include hp/alive/hit/damage.
        /// </summary>
        string BuildInputJson(InputFrame f, bool httpEnvelope)
        {
            string core =
                $"\"throttle\":{F(f.Throttle)},\"steer\":{F(f.Steer)},\"brake\":{(f.Brake ? "true" : "false")}," +
                $"\"fire\":{(f.Fire ? "true" : "false")}," +
                $"\"aimYaw\":{F(f.AimYaw)},\"aimPitch\":{F(f.AimPitch)}," +
                $"\"turretYaw\":{F(f.TurretYaw)},\"gunPitch\":{F(f.GunPitch)}";
            if (httpEnvelope)
                return $"{{\"room\":\"{Escape(RoomId)}\",\"userId\":\"{Escape(UserId)}\",{core}}}";
            return $"{{\"type\":\"input\",{core}}}";
        }

        void ParseJoinResponse(string json)
        {
            if (TryExtractString(json, "team", out var team)) Team = team;
            if (TryExtractString(json, "id", out var id) && !string.IsNullOrEmpty(id)) UserId = id;
            if (TryExtractString(json, "room", out var room)) RoomId = room;
            if (TryExtractString(json, "vehicleId", out var vid)) VehicleId = vid;
        }

        void HandleIncomingJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            // Discrete WS event frames: {"type":"shot",...} without full state
            if (LooksLikeDiscreteEvent(json))
            {
                var ev = GameEvent.Parse(json);
                if (ev != null) DispatchEvent(ev);
                return;
            }

            var payload = RoomStatePayload.ParseFlexible(json);
            if (payload == null) return;
            LastState = payload;
            OnState?.Invoke(payload);

            if (payload.Events != null)
            {
                foreach (var ev in payload.Events)
                {
                    if (ev == null) continue;
                    string key = ev.DedupKey;
                    if (_seenEventKeys.Contains(key)) continue;
                    // Prefer newer events when HTTP poll re-sends a sliding window
                    if (ev.T > 0 && ev.T < _lastEventT - 5000) continue;
                    _seenEventKeys.Add(key);
                    if (_seenEventKeys.Count > 256)
                        _seenEventKeys.Clear();
                    if (ev.T > _lastEventT) _lastEventT = ev.T;
                    DispatchEvent(ev);
                }
            }

            if (payload.Ended)
                OnMatchEnd?.Invoke(payload.Winner);
        }

        void DispatchEvent(GameEvent ev)
        {
            OnGameEvent?.Invoke(ev);
            if (ev.Type == "end")
                OnMatchEnd?.Invoke(ev.Winner);
        }

        static bool LooksLikeDiscreteEvent(string json)
        {
            // state frames always contain "units" or type:state
            if (json.IndexOf("\"units\"", StringComparison.Ordinal) >= 0) return false;
            if (json.IndexOf("\"type\":\"state\"", StringComparison.Ordinal) >= 0) return false;
            string[] types =
            {
                "shot", "hit", "module_break", "fire_start", "fire_end", "cookoff",
                "kill", "spectator", "end", "join", "leave"
            };
            foreach (var t in types)
            {
                if (json.IndexOf($"\"type\":\"{t}\"", StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        void SetStatus(string s)
        {
            LastStatus = s;
            OnStatus?.Invoke(s);
            Debug.Log($"[IronwakeClient] {s}");
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        internal static bool TryExtractString(string json, string key, out string value)
        {
            value = null;
            string needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return false;
            int j = i + 1;
            while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
            if (j < json.Length && json[j] == 'n') { value = null; return true; } // null
            int q1 = json.IndexOf('"', i + 1);
            if (q1 < 0) return false;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return false;
            value = json.Substring(q1 + 1, q2 - q1 - 1);
            return true;
        }

        internal static bool TryExtractNumber(string json, string key, out float v)
        {
            v = 0;
            string needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return false;
            int j = i + 1;
            while (j < json.Length && (char.IsWhiteSpace(json[j]) || json[j] == '+')) j++;
            int k = j;
            while (k < json.Length && "0123456789.-+eE".IndexOf(json[k]) >= 0) k++;
            return float.TryParse(json.Substring(j, k - j), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        internal static bool TryExtractLong(string json, string key, out long v)
        {
            v = 0;
            if (!TryExtractNumber(json, key, out float f)) return false;
            v = (long)f;
            return true;
        }

        internal static bool TryExtractBool(string json, string key, out bool v)
        {
            v = false;
            string needle = $"\"{key}\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return false;
            int j = i + 1;
            while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
            if (j + 4 <= json.Length && string.Compare(json, j, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            { v = true; return true; }
            if (j + 5 <= json.Length && string.Compare(json, j, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            { v = false; return true; }
            return false;
        }

        internal static string ExtractArrayBlock(string json, string key)
        {
            int k = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0) return null;
            int start = json.IndexOf('[', k);
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        internal static string ExtractObjectBlock(string json, string key)
        {
            int k = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0) return null;
            int start = json.IndexOf('{', k);
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        internal static List<string> SplitJsonObjects(string arrayJson)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(arrayJson)) return list;
            string inner = arrayJson.Trim();
            if (inner.StartsWith("[")) inner = inner.Substring(1);
            if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
            if (string.IsNullOrWhiteSpace(inner)) return list;

            int depth = 0;
            int start = -1;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        list.Add(inner.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }
            return list;
        }
    }

    /// <summary>Control-only input. Never send hp / hit / damage.</summary>
    [Serializable]
    public struct InputFrame
    {
        public float Throttle;   // [-1,1]
        public float Steer;      // [-1,1]
        public bool Brake;
        public bool Fire;
        public float AimYaw;     // rad
        public float AimPitch;   // rad
        public float TurretYaw;  // rad (alias / mirror of aim)
        public float GunPitch;   // rad
    }

    [Serializable]
    public class RoomStatePayload
    {
        public long T;
        public UnitSnapshot[] Units = Array.Empty<UnitSnapshot>();
        public ProjectileSnapshot[] Projectiles = Array.Empty<ProjectileSnapshot>();
        public GameEvent[] Events = Array.Empty<GameEvent>();
        public bool Ended;
        public string Winner;
        public string Raw;

        public static RoomStatePayload ParseFlexible(string json)
        {
            var p = new RoomStatePayload { Raw = json };
            IronwakeClient.TryExtractLong(json, "t", out p.T);
            IronwakeClient.TryExtractBool(json, "ended", out p.Ended);
            IronwakeClient.TryExtractString(json, "winner", out p.Winner);

            try
            {
                string unitsBlock = IronwakeClient.ExtractArrayBlock(json, "units");
                if (!string.IsNullOrEmpty(unitsBlock))
                    p.Units = ParseUnits(unitsBlock);

                string projBlock = IronwakeClient.ExtractArrayBlock(json, "projectiles");
                if (!string.IsNullOrEmpty(projBlock))
                    p.Projectiles = ParseProjectiles(projBlock);

                string evBlock = IronwakeClient.ExtractArrayBlock(json, "events");
                if (!string.IsNullOrEmpty(evBlock))
                    p.Events = ParseEvents(evBlock);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IronwakeClient] parse state: {ex.Message}");
            }
            return p;
        }

        static UnitSnapshot[] ParseUnits(string arrayJson)
        {
            var list = new List<UnitSnapshot>();
            foreach (var obj in IronwakeClient.SplitJsonObjects(arrayJson))
            {
                var u = new UnitSnapshot();
                IronwakeClient.TryExtractString(obj, "id", out u.Id);
                IronwakeClient.TryExtractString(obj, "team", out u.Team);
                IronwakeClient.TryExtractString(obj, "callsign", out u.Callsign);
                IronwakeClient.TryExtractString(obj, "vehicleId", out u.VehicleId);
                if (string.IsNullOrEmpty(u.VehicleId))
                    IronwakeClient.TryExtractString(obj, "defId", out u.VehicleId);
                IronwakeClient.TryExtractNumber(obj, "x", out u.X);
                IronwakeClient.TryExtractNumber(obj, "y", out u.Y);
                IronwakeClient.TryExtractNumber(obj, "z", out u.Z);
                IronwakeClient.TryExtractNumber(obj, "yaw", out u.Yaw);
                IronwakeClient.TryExtractNumber(obj, "turretYaw", out u.TurretYaw);
                IronwakeClient.TryExtractNumber(obj, "gunPitch", out u.GunPitch);
                IronwakeClient.TryExtractNumber(obj, "hp", out u.Hp);
                IronwakeClient.TryExtractNumber(obj, "maxHp", out u.MaxHp);
                if (u.MaxHp <= 0f) u.MaxHp = Mathf.Max(1f, u.Hp);
                if (!IronwakeClient.TryExtractBool(obj, "alive", out u.Alive)) u.Alive = true;
                IronwakeClient.TryExtractBool(obj, "spectator", out u.Spectator);
                IronwakeClient.TryExtractBool(obj, "onFire", out u.OnFire);
                IronwakeClient.TryExtractBool(obj, "immobilized", out u.Immobilized);
                if (!IronwakeClient.TryExtractBool(obj, "canFire", out u.CanFire)) u.CanFire = true;
                IronwakeClient.TryExtractBool(obj, "opticsBroken", out u.OpticsBroken);
                IronwakeClient.TryExtractNumber(obj, "fuel", out u.Fuel);
                IronwakeClient.TryExtractNumber(obj, "ammo", out u.Ammo);

                string mods = IronwakeClient.ExtractObjectBlock(obj, "modules");
                if (!string.IsNullOrEmpty(mods))
                    u.Modules = ModuleMap.Parse(mods);
                list.Add(u);
            }
            return list.ToArray();
        }

        static ProjectileSnapshot[] ParseProjectiles(string arrayJson)
        {
            var list = new List<ProjectileSnapshot>();
            foreach (var obj in IronwakeClient.SplitJsonObjects(arrayJson))
            {
                var p = new ProjectileSnapshot();
                IronwakeClient.TryExtractString(obj, "id", out p.Id);
                IronwakeClient.TryExtractString(obj, "ownerId", out p.OwnerId);
                IronwakeClient.TryExtractNumber(obj, "x", out p.X);
                IronwakeClient.TryExtractNumber(obj, "y", out p.Y);
                IronwakeClient.TryExtractNumber(obj, "z", out p.Z);
                list.Add(p);
            }
            return list.ToArray();
        }

        static GameEvent[] ParseEvents(string arrayJson)
        {
            var list = new List<GameEvent>();
            foreach (var obj in IronwakeClient.SplitJsonObjects(arrayJson))
            {
                var ev = GameEvent.Parse(obj);
                if (ev != null) list.Add(ev);
            }
            return list.ToArray();
        }
    }

    [Serializable]
    public class UnitSnapshot
    {
        public string Id, Team, Callsign, VehicleId;
        public float X, Y, Z, Yaw, TurretYaw, GunPitch;
        public float Hp, MaxHp;
        public bool Alive = true;
        public bool Spectator;
        public bool OnFire;
        public bool Immobilized;
        public bool CanFire = true;
        public bool OpticsBroken;
        public float Fuel, Ammo;
        public ModuleMap Modules = new ModuleMap();
    }

    [Serializable]
    public class ProjectileSnapshot
    {
        public string Id;
        public string OwnerId;
        public float X, Y, Z;
    }

    [Serializable]
    public class ModuleMap
    {
        // PROTOCOL: hull_f/s/r, turret, gun, engine, ammo, track_l/r, fuel, optics
        public float HullF = 1f, HullS = 1f, HullR = 1f;
        public float Turret = 1f, Gun = 1f, Engine = 1f, Ammo = 1f;
        public float TrackL = 1f, TrackR = 1f, Fuel = 1f, Optics = 1f;

        public static readonly string[] Keys =
        {
            "hull_f", "hull_s", "hull_r", "turret", "gun", "engine",
            "ammo", "track_l", "track_r", "fuel", "optics"
        };

        public float Get(string key)
        {
            switch (key)
            {
                case "hull_f": case "hull_front": return HullF;
                case "hull_s": case "hull_side": return HullS;
                case "hull_r": case "hull_rear": return HullR;
                case "turret": return Turret;
                case "gun": return Gun;
                case "engine": return Engine;
                case "ammo": return Ammo;
                case "track_l": return TrackL;
                case "track_r": return TrackR;
                case "fuel": return Fuel;
                case "optics": return Optics;
                default: return 1f;
            }
        }

        public void Set(string key, float v)
        {
            v = Mathf.Clamp01(v);
            switch (key)
            {
                case "hull_f": case "hull_front": HullF = v; break;
                case "hull_s": case "hull_side": HullS = v; break;
                case "hull_r": case "hull_rear": HullR = v; break;
                case "turret": Turret = v; break;
                case "gun": Gun = v; break;
                case "engine": Engine = v; break;
                case "ammo": Ammo = v; break;
                case "track_l": TrackL = v; break;
                case "track_r": TrackR = v; break;
                case "fuel": Fuel = v; break;
                case "optics": Optics = v; break;
            }
        }

        public Dictionary<string, float> ToDictionary()
        {
            var d = new Dictionary<string, float>();
            foreach (var k in Keys) d[k] = Get(k);
            return d;
        }

        public static ModuleMap Parse(string jsonObj)
        {
            var m = new ModuleMap();
            foreach (var k in Keys)
            {
                if (IronwakeClient.TryExtractNumber(jsonObj, k, out float v))
                    m.Set(k, v);
            }
            // legacy aliases
            if (IronwakeClient.TryExtractNumber(jsonObj, "hull_front", out float hf)) m.HullF = Mathf.Clamp01(hf);
            return m;
        }
    }

    [Serializable]
    public class GameEvent
    {
        public string Type;
        public string Id;
        public string By;
        public string Module;
        public string ProjectileId;
        public string Winner;
        public string Callsign;
        public string Team;
        public string VehicleId;
        public float Hp;
        public string Facing;
        public bool Bounce;
        public bool Pen;
        public long T;
        public string Raw;

        public string DedupKey => $"{Type}|{Id}|{By}|{Module}|{ProjectileId}|{T}";

        public static GameEvent Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var ev = new GameEvent { Raw = json };
            IronwakeClient.TryExtractString(json, "type", out ev.Type);
            if (string.IsNullOrEmpty(ev.Type)) return null;

            // Prefer nested payload object fields
            string payload = IronwakeClient.ExtractObjectBlock(json, "payload") ?? json;
            IronwakeClient.TryExtractString(payload, "id", out ev.Id);
            IronwakeClient.TryExtractString(payload, "by", out ev.By);
            IronwakeClient.TryExtractString(payload, "module", out ev.Module);
            IronwakeClient.TryExtractString(payload, "projectileId", out ev.ProjectileId);
            IronwakeClient.TryExtractString(payload, "winner", out ev.Winner);
            IronwakeClient.TryExtractString(payload, "callsign", out ev.Callsign);
            IronwakeClient.TryExtractString(payload, "team", out ev.Team);
            IronwakeClient.TryExtractString(payload, "vehicleId", out ev.VehicleId);
            IronwakeClient.TryExtractString(payload, "facing", out ev.Facing);
            IronwakeClient.TryExtractNumber(payload, "hp", out ev.Hp);
            IronwakeClient.TryExtractBool(payload, "bounce", out ev.Bounce);
            IronwakeClient.TryExtractBool(payload, "pen", out ev.Pen);
            IronwakeClient.TryExtractLong(json, "t", out ev.T);
            // top-level winner for type:end sometimes
            if (string.IsNullOrEmpty(ev.Winner))
                IronwakeClient.TryExtractString(json, "winner", out ev.Winner);
            return ev;
        }
    }

    [Serializable]
    public class UserProfile
    {
        public string Id;
        public string Callsign;
        public int Steel;
        public int Intel;
        public int Commendations; // Награды / xp proxy
        public int Xp;

        public static UserProfile Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string block = IronwakeClient.ExtractObjectBlock(json, "user") ?? json;
            var u = new UserProfile();
            IronwakeClient.TryExtractString(block, "id", out u.Id);
            if (string.IsNullOrEmpty(u.Id)) IronwakeClient.TryExtractString(block, "userId", out u.Id);
            IronwakeClient.TryExtractString(block, "callsign", out u.Callsign);
            if (string.IsNullOrEmpty(u.Callsign)) IronwakeClient.TryExtractString(block, "name", out u.Callsign);
            if (IronwakeClient.TryExtractNumber(block, "steel", out float s)) u.Steel = Mathf.RoundToInt(s);
            if (IronwakeClient.TryExtractNumber(block, "intel", out float i)) u.Intel = Mathf.RoundToInt(i);
            if (IronwakeClient.TryExtractNumber(block, "commendations", out float c)) u.Commendations = Mathf.RoundToInt(c);
            else if (IronwakeClient.TryExtractNumber(block, "xp", out float xp))
            {
                u.Xp = Mathf.RoundToInt(xp);
                u.Commendations = u.Xp; // UI label Награды until dedicated field exists
            }
            return u;
        }
    }
}
