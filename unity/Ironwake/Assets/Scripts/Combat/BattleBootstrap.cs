using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;
using Ironwake.Meta;
using Ironwake.Graphics;
using Ironwake.Sim;
using Ironwake.Input;

namespace Ironwake.Combat
{
    /// <summary>
    /// Default: LocalBattleSim (device-authoritative graphics/combat + bots).
    /// Optional: online room via IronwakeClient when PlayerPrefs iw.battleMode=online.
    /// Builds environment + tank visuals at runtime so empty YAML scenes still play.
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        public enum BattleMode { LocalSim, Online }

        [SerializeField] VehicleCatalog catalog;
        [SerializeField] bool spawnRuntimeHud = true;
        [SerializeField] BattleMode mode = BattleMode.LocalSim;

        VehicleController _local;
        ModuleDamagePresenter _localMods;
        ProjectilePresenter _projectiles;
        CombatVfx _vfx;
        LocalBattleSim _sim;
        readonly Dictionary<string, RemoteUnitView> _remotes = new Dictionary<string, RemoteUnitView>();
        string _killerId;
        bool _matchEnded;
        string _winner;
        bool _reportedMatch;
        ParticleSystem _localDust;

        void Start()
        {
            if (catalog == null) catalog = VehicleCatalog.CreateDefaultRuntime();

            string modePref = PlayerPrefs.GetString("iw.battleMode", "local");
            if (modePref == "online") mode = BattleMode.Online;
            else mode = BattleMode.LocalSim;

            BattleEnvironmentBuilder.Build();
            _vfx = CombatVfx.Ensure();

            string vid = PlayerPrefs.GetString("iw.vehicle", "k72-ural");
            string callsign = PlayerPrefs.GetString("iw.callsign", "OPERATOR");
            var def = catalog.Get(vid);
            float y = def != null ? Mathf.Max(0.5f, def.groundClearance * 0.15f) : 1f;
            Vector3 spawn = new Vector3(8f, y, 22f);

            if (mode == BattleMode.Online)
            {
                EnsureClient();
                if (IronwakeClient.Instance != null && IronwakeClient.Instance.Team == "red")
                    spawn = new Vector3(-8f, y, -22f);
            }

            _local = VehicleController.SpawnPrimitive(def, spawn);
            _local.IsLocalPlayer = true;
            _local.SetCallsign(callsign);
            // Local sim owns motion — disable client prediction soft-correct against missing server
            if (mode == BattleMode.LocalSim)
                _local.SetLocalSimDriven(true);

            _localMods = _local.GetComponent<ModuleDamagePresenter>();
            var net = _local.GetComponent<NetUnitId>();

            var projGo = new GameObject("ProjectilePresenter");
            _projectiles = projGo.AddComponent<ProjectilePresenter>();

            _localDust = _vfx.AttachDust(_local.transform);

            if (spawnRuntimeHud)
            {
                var hud = new GameObject("BattleHud").AddComponent<BattleHudStub>();
                hud.Bind(_local);
                var canvas = Object.FindObjectOfType<Canvas>();
                if (canvas != null && _localMods != null)
                    _localMods.EnsureUiStrip(canvas.transform);
            }

            if (mode == BattleMode.LocalSim)
                BootLocal(callsign, vid, net);
            else
                BootOnline(net);
        }

        void BootLocal(string callsign, string vid, NetUnitId net)
        {
            var simGo = new GameObject("LocalBattleSim");
            _sim = simGo.AddComponent<LocalBattleSim>();

            string userId = PlayerPrefs.GetString("iw.userId", "");
            if (string.IsNullOrEmpty(userId) && IronwakeClient.Instance != null)
                userId = IronwakeClient.Instance.UserId;
            if (string.IsNullOrEmpty(userId))
                userId = "local_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            if (net != null) net.Id = userId;

            _sim.OnState += OnLocalState;
            _sim.OnGameEvent += OnGameEvent;
            _sim.OnMatchEnd += OnMatchEnd;

            string team = PlayerPrefs.GetString("iw.team", "blue");
            _sim.StartLocalBattle(catalog, userId, callsign, vid, team);

            var input = new GameObject("MobileBattleInput").AddComponent<MobileBattleInput>();
            input.Bind(_local, _sim);

            // Meta client for POST /match only (no room join required)
            EnsureClient();
            if (IronwakeClient.Instance != null)
                _projectiles.BindClient(IronwakeClient.Instance);

            Debug.Log("[BattleBootstrap] LocalSim mode — graphics & combat on device");
        }

        void BootOnline(NetUnitId net)
        {
            EnsureClient();
            if (net != null && IronwakeClient.Instance != null)
                net.Id = IronwakeClient.Instance.UserId;

            if (IronwakeClient.Instance != null)
            {
                _projectiles.BindClient(IronwakeClient.Instance);
                IronwakeClient.Instance.OnState += OnRoomState;
                IronwakeClient.Instance.OnGameEvent += OnGameEvent;
                IronwakeClient.Instance.OnMatchEnd += OnMatchEnd;
            }

            var input = new GameObject("MobileBattleInput").AddComponent<MobileBattleInput>();
            input.Bind(_local, null);
            Debug.Log("[BattleBootstrap] Online mode — IronwakeClient room sync");
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnState -= OnLocalState;
                _sim.OnGameEvent -= OnGameEvent;
                _sim.OnMatchEnd -= OnMatchEnd;
            }
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

        void Update()
        {
            if (_local != null && _localDust != null && _vfx != null)
            {
                float spd = _local.EstimatedSpeed;
                _vfx.SetDustRate(_localDust, spd);
            }
        }

        void OnLocalState(RoomStatePayload state)
        {
            ApplyState(state, fromLocal: true);
            // Drive projectiles visually from local shells
            if (state?.Projectiles != null && _vfx != null)
            {
                foreach (var p in state.Projectiles)
                {
                    if (p == null) continue;
                    // Lightweight streak toward motion — presenter also handles server path
                }
            }
        }

        void OnRoomState(RoomStatePayload state) => ApplyState(state, fromLocal: false);

        void ApplyState(RoomStatePayload state, bool fromLocal)
        {
            if (state?.Units == null) return;
            var seen = new HashSet<string>();
            string myId = fromLocal
                ? (_sim != null ? _sim.LocalPlayerId : null)
                : (IronwakeClient.Instance != null ? IronwakeClient.Instance.UserId : null);

            foreach (var u in state.Units)
            {
                if (u == null || string.IsNullOrEmpty(u.Id)) continue;
                if (u.Id == myId)
                {
                    if (fromLocal)
                        _local?.ApplySimSnapshot(u);
                    else
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
                    remote.enabled = false;
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
            string myId = _sim != null && mode == BattleMode.LocalSim
                ? _sim.LocalPlayerId
                : (IronwakeClient.Instance != null ? IronwakeClient.Instance.UserId : null);

            switch (ev.Type)
            {
                case "shot":
                    HandleShotVfx(ev);
                    break;
                case "hit":
                    if (ev.Id == myId && _localMods != null && !string.IsNullOrEmpty(ev.Module))
                        _localMods.ApplyHit(ev.Module, 0.05f);
                    HandleHitVfx(ev);
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
                    HandleCookOff(ev.Id);
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

        void HandleShotVfx(GameEvent ev)
        {
            if (_vfx == null) return;
            Transform muzzle = null;
            if (_sim != null && ev.Id == _sim.LocalPlayerId && _local != null)
                muzzle = _local.Muzzle;
            else if (!string.IsNullOrEmpty(ev.Id) && _remotes.TryGetValue(ev.Id, out var view) && view != null)
                muzzle = view.GetMuzzle();

            Vector3 pos = muzzle != null ? muzzle.position : Vector3.up;
            Vector3 dir = muzzle != null ? muzzle.forward : Vector3.forward;
            _vfx.MuzzleFlash(pos, dir);
            _vfx.Tracer(pos, pos + dir * 40f);
        }

        void HandleHitVfx(GameEvent ev)
        {
            if (_vfx == null) return;
            Vector3 pos = Vector3.up;
            if (!string.IsNullOrEmpty(ev.Id))
            {
                if (_sim != null && ev.Id == _sim.LocalPlayerId && _local != null)
                    pos = _local.transform.position + Vector3.up;
                else if (_remotes.TryGetValue(ev.Id, out var v) && v != null && v.Root)
                    pos = v.Root.position + Vector3.up;
            }
            _vfx.ImpactSparks(pos, Vector3.up);
        }

        void HandleCookOff(string unitId)
        {
            if (_vfx == null) return;
            Vector3 pos = _local != null ? _local.transform.position : Vector3.zero;
            if (!string.IsNullOrEmpty(unitId) && _remotes.TryGetValue(unitId, out var v) && v != null && v.Root)
                pos = v.Root.position;
            _vfx.CookOffExplosion(pos);
        }

        void OnMatchEnd(string winner)
        {
            if (_matchEnded) return;
            _matchEnded = true;
            _winner = winner;
            Debug.Log($"[Battle] MATCH END winner={winner}");
            if (!_reportedMatch && mode == BattleMode.LocalSim)
            {
                _reportedMatch = true;
                StartCoroutine(ReportMatchBestEffort());
            }
        }

        IEnumerator ReportMatchBestEffort()
        {
            EnsureClient();
            var client = IronwakeClient.Instance;
            if (client == null || _sim == null) yield break;
            var result = _sim.BuildResult();
            if (string.IsNullOrEmpty(result.UserId) || result.UserId.StartsWith("local_"))
            {
                Debug.Log("[Battle] skip POST /match — anonymous local user");
                yield break;
            }
            bool ok = false;
            yield return client.ReportMatch(result, s => ok = s);
            Debug.Log(ok ? "[Battle] POST /match ok" : "[Battle] POST /match failed (best-effort)");
        }

        void OnGUI()
        {
            if (!_matchEnded) return;
            string msg = string.IsNullOrEmpty(_winner)
                ? "БОЙ ОКОНЧЕН / MATCH OVER"
                : $"ПОБЕДА / WINNER: {(_winner == "blue" ? "СИНИЕ / BLUE" : "КРАСНЫЕ / RED")}";
            var style = new GUIStyle(GUI.skin.box) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * 0.2f, Screen.height * 0.38f, Screen.width * 0.6f, 90), msg, style);
            string modeLabel = mode == BattleMode.LocalSim ? "LocalSim" : "Online";
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.38f + 95, Screen.width * 0.6f, 28),
                $"[{modeLabel}]");
        }
    }

    /// <summary>Interpolates remote unit pose between snapshots (~15–20 Hz).</summary>
    public sealed class RemoteUnitView : MonoBehaviour
    {
        VehicleController _vc;
        ModuleDamagePresenter _mods;
        Vector3 _fromPos, _toPos;
        float _fromYaw, _toYaw, _fromTurret, _toTurret, _fromPitch, _toPitch;
        float _t;
        const float SnapshotInterval = 1f / 20f;

        public Transform Root => transform;

        public void Bind(VehicleController vc)
        {
            _vc = vc;
            _mods = vc.GetComponent<ModuleDamagePresenter>();
            _fromPos = _toPos = vc.transform.position;
        }

        public Transform GetMuzzle() => _vc != null ? _vc.Muzzle : null;

        public void PushSnapshot(UnitSnapshot u)
        {
            _fromPos = transform.position;
            _toPos = new Vector3(u.X, u.Y > 0.01f ? u.Y : transform.position.y, u.Z);
            _fromYaw = _vc != null && _vc.Hull ? _vc.Hull.eulerAngles.y : transform.eulerAngles.y;
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
