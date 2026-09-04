using System.Collections.Generic;
using UnityEngine;

namespace Ironwake.Combat
{
    /// <summary>
    /// Visualizes module HP zones: hull, engine, ammo, tracks.
    /// Hooks for fire VFX, track break, and ammo cook-off.
    /// Server remains authoritative; this is presentation only.
    /// </summary>
    public sealed class ModuleDamagePresenter : MonoBehaviour
    {
        public const string HullFront = "hull_front";
        public const string Engine = "engine";
        public const string Ammo = "ammo";
        public const string TrackL = "track_l";
        public const string TrackR = "track_r";

        [SerializeField] ParticleSystem fireFx;
        [SerializeField] ParticleSystem cookOffFx;
        [SerializeField] GameObject trackSparksL;
        [SerializeField] GameObject trackSparksR;
        [SerializeField] float cookOffThreshold = 0.15f;

        readonly Dictionary<string, float> _hp = new Dictionary<string, float>
        {
            { HullFront, 1f },
            { Engine, 1f },
            { Ammo, 1f },
            { TrackL, 1f },
            { TrackR, 1f },
        };

        VehicleController _vehicle;
        bool _fireLit;
        public bool IsCookedOff { get; private set; }

        public IReadOnlyDictionary<string, float> Modules => _hp;

        void Awake()
        {
            _vehicle = GetComponent<VehicleController>();
            EnsureZoneColliders();
        }

        void EnsureZoneColliders()
        {
            // Lightweight trigger boxes for raycast hit modules (primitives).
            CreateZone(HullFront, new Vector3(0f, 1f, 2.2f), new Vector3(2.2f, 1f, 0.6f));
            CreateZone(Engine, new Vector3(0f, 1f, -1.8f), new Vector3(1.8f, 0.9f, 1.2f));
            CreateZone(Ammo, new Vector3(0f, 1.1f, 0.2f), new Vector3(1.2f, 0.8f, 1.2f));
            CreateZone(TrackL, new Vector3(-1.3f, 0.4f, 0f), new Vector3(0.35f, 0.6f, 4.2f));
            CreateZone(TrackR, new Vector3(1.3f, 0.4f, 0f), new Vector3(0.35f, 0.6f, 4.2f));
        }

        void CreateZone(string id, Vector3 localPos, Vector3 size)
        {
            var go = new GameObject("Zone_" + id);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.layer = LayerMask.NameToLayer("Default");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var zone = go.AddComponent<ModuleHitZone>();
            zone.ModuleId = id;
            var net = GetComponent<NetUnitId>();
            zone.OwnerUnitId = net != null ? net.Id : null;
        }

        public void ApplyServerModules(Dictionary<string, float> server)
        {
            if (server == null) return;
            foreach (var kv in server)
                SetModule(kv.Key, kv.Value);
        }

        public void SetModule(string id, float normalized)
        {
            if (!_hp.ContainsKey(id)) _hp[id] = 1f;
            float v = Mathf.Clamp01(normalized);
            _hp[id] = v;
            OnModuleChanged(id, v);
        }

        public void ApplyHit(string moduleId, float damageNormalized)
        {
            if (!_hp.ContainsKey(moduleId)) _hp[moduleId] = 1f;
            SetModule(moduleId, _hp[moduleId] - damageNormalized);
        }

        void OnModuleChanged(string id, float v)
        {
            switch (id)
            {
                case Engine:
                    if (_vehicle) _vehicle.EngineDead = v <= 0.05f;
                    if (v < 0.4f) LightFire();
                    break;
                case TrackL:
                case TrackR:
                    bool broken = _hp[TrackL] <= 0.05f || _hp[TrackR] <= 0.05f;
                    if (_vehicle) _vehicle.TracksBroken = broken;
                    if (trackSparksL) trackSparksL.SetActive(_hp[TrackL] < 0.35f);
                    if (trackSparksR) trackSparksR.SetActive(_hp[TrackR] < 0.35f);
                    break;
                case Ammo:
                    if (v <= cookOffThreshold) TriggerCookOff();
                    else if (v < 0.5f) LightFire();
                    break;
                case HullFront:
                    if (v < 0.25f) LightFire();
                    break;
            }
        }

        void LightFire()
        {
            if (_fireLit) return;
            _fireLit = true;
            if (fireFx != null) fireFx.Play();
            else
            {
                // Placeholder: orange point light + primitive "flame" cube.
                var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = "FirePlaceholder";
                flame.transform.SetParent(transform, false);
                flame.transform.localPosition = new Vector3(0f, 2.2f, -0.5f);
                flame.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
                Object.Destroy(flame.GetComponent<Collider>());
                var r = flame.GetComponent<Renderer>();
                if (r) r.material.color = new Color(1f, 0.35f, 0.05f);
                var light = flame.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.45f, 0.1f);
                light.range = 8f;
                light.intensity = 2.5f;
            }
        }

        void TriggerCookOff()
        {
            if (IsCookedOff) return;
            IsCookedOff = true;
            LightFire();
            if (cookOffFx != null) cookOffFx.Play();
            else
            {
                // Burst of primitives as stand-in VFX.
                for (int i = 0; i < 8; i++)
                {
                    var bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bit.name = "CookOffBit";
                    bit.transform.position = transform.position + Vector3.up * 1.5f;
                    bit.transform.localScale = Vector3.one * 0.25f;
                    Object.Destroy(bit.GetComponent<Collider>());
                    var rb = bit.AddComponent<Rigidbody>();
                    rb.velocity = Random.onUnitSphere * 8f + Vector3.up * 6f;
                    Object.Destroy(bit, 2.5f);
                }
            }
            Debug.Log($"[ModuleDamage] COOK-OFF on {name}");
        }

        public string HudText()
        {
            return $"Лоб {Pct(HullFront)}  Двиг {Pct(Engine)}  БК {Pct(Ammo)}\nГус.Л {Pct(TrackL)}  Гус.П {Pct(TrackR)}"
                   + (IsCookedOff ? "\nВЗРЫВ БК" : "");
        }

        string Pct(string id) => Mathf.RoundToInt(_hp[id] * 100f) + "%";
    }
}
