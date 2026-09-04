using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;
using Ironwake.Meta;

namespace Ironwake.Combat
{
    /// <summary>
    /// Wires IronwakeClient + local player + remote interpolators + projectile presenter.
    /// Soft-corrects local pose from authoritative units; remotes lerp toward snapshots.
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] VehicleCatalog catalog;
        [SerializeField] bool spawnRuntimeHud = true;

        VehicleController _local;
        ModuleDamagePresenter _localMods;
        ProjectilePresenter _projectiles;
        readonly Dictionary<string, RemoteUnitView> _remotes = new Dictionary<string, RemoteUnitView>();
        string _killerId;
        bool _matchEnded;
        string _winner;

        void Start()
        {
            if (catalog == null) catalog = VehicleCatalog.CreateDefaultRuntime();
            EnsureEnv();
            EnsureClient();

            string vid = PlayerPrefs.GetString("iw.vehicle", "k72-ural");
            string callsign = PlayerPrefs.GetString("iw.callsign", "OPERATOR");
            var def = catalog.Get(vid);
            float y = def != null ? Mathf.Max(0.5f, def.groundClearance * 0.15f) : 1f;
            Vector3 spawn = new Vector3(8f, y, 22f);
            if (IronwakeClient.Instance != null && IronwakeClient.Instance.Team == "red")
                spawn = new Vector3(-8f, y, -22f);

            _local = VehicleController.SpawnPrimitive(def, spawn);
            _local.IsLocalPlayer = true;
            _local.SetCallsign(callsign);
            _localMods = _local.GetComponent<ModuleDamagePresenter>();
            var net = _local.GetComponent<NetUnitId>();
            if (net != null && IronwakeClient.Instance != null)
                net.Id = IronwakeClient.Instance.UserId;

            var projGo = new GameObject("ProjectilePresenter");
            _projectiles = projGo.AddComponent<ProjectilePresenter>();
            if (IronwakeClient.Instance != null)
                _projectiles.BindClient(IronwakeClient.Instance);

            if (spawnRuntimeHud)
            {
                var hud = new GameObject("BattleHud").AddComponent<BattleHudStub>();
                hud.Bind(_local);
                // Module icon strip on same canvas
                var canvas = Object.FindObjectOfType<Canvas>();
                if (canvas != null && _localMods != null)
                    _localMods.EnsureUiStrip(canvas.transform);
            }

            if (IronwakeClient.Instance != null)
            {
                IronwakeClient.Instance.OnState += OnRoomState;
                IronwakeClient.Instance.OnGameEvent += OnGameEvent;
                IronwakeClient.Instance.OnMatchEnd += OnMatchEnd;
            }
        }

        void OnDestroy()
        {
            if (IronwakeClient.Instance != null)
            {
                IronwakeClient.Instance.OnState -= OnRoomState;
                IronwakeClient.Instance.OnGameEvent -= OnGameEvent;
                IronwakeClient.Instance.OnMatchEnd -= OnMatchEnd;
            }
        }

        void EnsureClient()
        {
            if (IronwakeClient.Instance != null) return;
            var go = new GameObject("IronwakeClient");
            go.AddComponent<IronwakeClient>();
        }

        void EnsureEnv()
        {
            if (Object.FindObjectOfType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }
            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = Vector3.one * 42f;
                var r = ground.GetComponent<Renderer>();
                if (r) r.material.color = new Color(0.36f, 0.41f, 0.28f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.53f, 0.63f, 0.65f);
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 240f;
        }

        void OnRoomState(RoomStatePayload state)
        {
            if (state?.Units == null) return;
            var seen = new HashSet<string>();
            string myId = IronwakeClient.Instance != null ? IronwakeClient.Instance.UserId : null;

            foreach (var u in state.Units)
            {
                if (u == null || string.IsNullOrEmpty(u.Id)) continue;
                if (u.Id == myId)
                {
                    _local?.ApplyServerSnapshot(u, softCorrect: true);
                    if (u.Spectator || !u.Alive)
                    {
                        if (_local != null && !_local.IsSpectator)
                            _local.EnterSpectator(_killerId);
                    }
                    continue;
                }

                seen.Add(u.Id);
                if (!_remotes.TryGetValue(u.Id, out var view) || view == null || view.Root == null)
                {
                    var def = catalog.Get(u.VehicleId ?? "k72-ural");
                    float y = u.Y > 0.01f ? u.Y : (def != null ? def.groundClearance * 0.15f : 1f);
                    var remote = VehicleController.SpawnPrimitive(def, new Vector3(u.X, y, u.Z));
                    remote.IsLocalPlayer = false;
                    remote.enabled = false; // RemoteUnitView owns pose interpolation
                    // Disable local cameras on remotes
                    foreach (var cam in remote.GetComponentsInChildren<Camera>())
                        cam.enabled = false;
                    var nid = remote.GetComponent<NetUnitId>();
                    if (nid) nid.Id = u.Id;
                    view = remote.gameObject.AddComponent<RemoteUnitView>();
                    view.Bind(remote);
                    _remotes[u.Id] = view;
                }
                view.PushSnapshot(u);

                if (!string.IsNullOrEmpty(_killerId) && u.Id == _killerId && _local != null && _local.IsSpectator)
                    _local.SetFollowTarget(view.Root);
            }

            var toRemove = new List<string>();
            foreach (var kv in _remotes)
                if (!seen.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var id in toRemove)
            {
                if (_remotes[id] != null && _remotes[id].Root) Destroy(_remotes[id].Root.gameObject);
                _remotes.Remove(id);
            }
        }

        void OnGameEvent(GameEvent ev)
        {
            if (ev == null) return;
            string myId = IronwakeClient.Instance != null ? IronwakeClient.Instance.UserId : null;

            switch (ev.Type)
            {
                case "hit":
                    if (ev.Id == myId && _localMods != null && !string.IsNullOrEmpty(ev.Module))
                        _localMods.ApplyHit(ev.Module, 0.05f); // flash; real HP from state
                    break;
                case "module_break":
                    if (ev.Id == myId && _localMods != null && !string.IsNullOrEmpty(ev.Module))
                        _localMods.SetModule(ev.Module, 0f);
                    break;
                case "fire_start":
                    if (ev.Id == myId) _localMods?.ForceFireVisual(true);
                    break;
                case "fire_end":
                    if (ev.Id == myId) _localMods?.ForceFireVisual(false);
                    break;
                case "cookoff":
                    if (ev.Id == myId) _localMods?.TriggerCookOffExternal();
                    break;
                case "kill":
                    if (ev.Id == myId)
                    {
                        _killerId = ev.By;
                        _local?.EnterSpectator(_killerId);
                        if (!string.IsNullOrEmpty(_killerId) && _remotes.TryGetValue(_killerId, out var kv) && kv != null)
                            _local?.SetFollowTarget(kv.Root);
                    }
                    break;
                case "spectator":
                    if (ev.Id == myId)
                        _local?.EnterSpectator(_killerId);
                    break;
                case "end":
                    OnMatchEnd(ev.Winner);
                    break;
            }
        }

        void OnMatchEnd(string winner)
        {
            _matchEnded = true;
            _winner = winner;
            Debug.Log($"[Battle] MATCH END winner={winner}");
        }

        void OnGUI()
        {
            if (!_matchEnded) return;
            string msg = string.IsNullOrEmpty(_winner)
                ? "БОЙ ОКОНЧЕН"
                : $"ПОБЕДА: {(_winner == "blue" ? "СИНИЕ" : "КРАСНЫЕ")}";
            var style = new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * 0.25f, Screen.height * 0.4f, Screen.width * 0.5f, 80), msg, style);
        }
    }

    /// <summary>Interpolates remote unit pose between server snapshots (~15 Hz HTTP).</summary>
    public sealed class RemoteUnitView : MonoBehaviour
    {
        VehicleController _vc;
        ModuleDamagePresenter _mods;
        Vector3 _fromPos, _toPos;
        float _fromYaw, _toYaw, _fromTurret, _toTurret, _fromPitch, _toPitch;
        float _t;
        const float SnapshotInterval = 1f / 15f;

        public Transform Root => transform;

        public void Bind(VehicleController vc)
        {
            _vc = vc;
            _mods = vc.GetComponent<ModuleDamagePresenter>();
            _fromPos = _toPos = vc.transform.position;
        }

        public void PushSnapshot(UnitSnapshot u)
        {
            _fromPos = transform.position;
            _toPos = new Vector3(u.X, u.Y > 0.01f ? u.Y : transform.position.y, u.Z);
            _fromYaw = _vc != null ? transform.eulerAngles.y : 0f;
            // Prefer hull child yaw
            if (_vc != null && _vc.Hull) _fromYaw = _vc.Hull.eulerAngles.y;
            _toYaw = u.Yaw * Mathf.Rad2Deg;
            _fromTurret = _toTurret;
            _toTurret = u.TurretYaw * Mathf.Rad2Deg;
            _fromPitch = _toPitch;
            _toPitch = u.GunPitch * Mathf.Rad2Deg;
            _t = 0f;

            _vc?.ApplyServerSnapshot(u, softCorrect: false);
            if (_mods != null && u.Modules != null)
                _mods.ApplyServerModules(u.Modules.ToDictionary());
            if (u.OnFire) _mods?.ForceFireVisual(true);
            gameObject.SetActive(u.Alive || u.Spectator);
        }

        void Update()
        {
            _t += Time.deltaTime / SnapshotInterval;
            float a = Mathf.Clamp01(_t);
            transform.position = Vector3.Lerp(_fromPos, _toPos, a);
            float yaw = Mathf.LerpAngle(_fromYaw, _toYaw, a);
            float tur = Mathf.LerpAngle(_fromTurret, _toTurret, a);
            float pit = Mathf.Lerp(_fromPitch, _toPitch, a);
            if (_vc != null)
            {
                if (_vc.Hull) _vc.Hull.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (_vc.Turret) _vc.Turret.rotation = Quaternion.Euler(0f, tur, 0f);
                if (_vc.Gun) _vc.Gun.localRotation = Quaternion.Euler(pit, 0f, 0f);
            }
            else transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
