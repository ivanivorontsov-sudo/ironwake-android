using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;
using Ironwake.Meta;

namespace Ironwake.Combat
{
    /// <summary>
    /// Drop onto an empty Battle scene. Spawns ground + sun + player vehicle from
    /// PlayerPrefs (set by HangarUI). Subscribes to IronwakeClient state for remotes.
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] VehicleCatalog catalog;
        [SerializeField] bool spawnRuntimeHud = true;

        VehicleController _local;
        readonly System.Collections.Generic.Dictionary<string, Transform> _remotes =
            new System.Collections.Generic.Dictionary<string, Transform>();

        void Start()
        {
            if (catalog == null) catalog = VehicleCatalog.CreateDefaultRuntime();
            EnsureEnv();
            string vid = PlayerPrefs.GetString("iw.vehicle", "k72-ural");
            string callsign = PlayerPrefs.GetString("iw.callsign", "OPERATOR");
            var def = catalog.Get(vid);
            Vector3 spawn = new Vector3(8f, def != null ? def.groundClearance : 2.2f, 22f);
            if (IronwakeClient.Instance != null && IronwakeClient.Instance.Team == "red")
                spawn = new Vector3(-8f, spawn.y, -22f);

            _local = VehicleController.SpawnPrimitive(def, spawn);
            _local.SetCallsign(callsign);
            var net = _local.GetComponent<NetUnitId>();
            if (net != null && IronwakeClient.Instance != null)
                net.Id = IronwakeClient.Instance.UserId;

            if (spawnRuntimeHud)
            {
                var hud = new GameObject("BattleHud").AddComponent<BattleHudStub>();
                hud.Bind(_local);
            }

            if (IronwakeClient.Instance != null)
                IronwakeClient.Instance.OnState += OnRoomState;
        }

        void OnDestroy()
        {
            if (IronwakeClient.Instance != null)
                IronwakeClient.Instance.OnState -= OnRoomState;
        }

        void EnsureEnv()
        {
            if (RenderSettings.skybox == null)
                Camera.main?.gameObject.SetActive(true);
            if (FindObjectOfType<Light>() == null)
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
            // Soft fog like the legacy Three.js client
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.53f, 0.63f, 0.65f);
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 240f;
        }

        void OnRoomState(RoomStatePayload state)
        {
            if (state?.Units == null) return;
            var seen = new System.Collections.Generic.HashSet<string>();
            string myId = IronwakeClient.Instance != null ? IronwakeClient.Instance.UserId : null;
            foreach (var u in state.Units)
            {
                if (u == null || u.Id == myId) continue;
                seen.Add(u.Id);
                if (!_remotes.TryGetValue(u.Id, out var t) || t == null)
                {
                    var def = catalog.Get(u.VehicleId ?? "k72-ural");
                    var remote = VehicleController.SpawnPrimitive(def, new Vector3(u.X, 0f, u.Z));
                    // Remotes are visual only — disable local control
                    remote.enabled = false;
                    var nid = remote.GetComponent<NetUnitId>();
                    if (nid) nid.Id = u.Id;
                    t = remote.transform;
                    _remotes[u.Id] = t;
                }
                t.position = new Vector3(u.X, t.position.y, u.Z);
                t.rotation = Quaternion.Euler(0f, u.Yaw * Mathf.Rad2Deg, 0f);
                t.gameObject.SetActive(u.Alive);
            }
            var toRemove = new System.Collections.Generic.List<string>();
            foreach (var kv in _remotes)
                if (!seen.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var id in toRemove)
            {
                if (_remotes[id]) Destroy(_remotes[id].gameObject);
                _remotes.Remove(id);
            }
        }
    }
}
