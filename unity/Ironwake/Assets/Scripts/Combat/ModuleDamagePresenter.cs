using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;

namespace Ironwake.Combat
{
    /// <summary>
    /// Presentation for authoritative modules:
    /// hull_f/s/r, turret, gun, engine, ammo, track_l/r, fuel, optics.
    /// Visual: color/emission on hit zones, track break tilt/sparks, fire + cook-off.
    /// UI strip of module icons (UGUI if present, else OnGUI fallback).
    /// </summary>
    public sealed class ModuleDamagePresenter : MonoBehaviour
    {
        public static readonly string[] ModuleIds = ModuleMap.Keys;

        [SerializeField] ParticleSystem fireFx;
        [SerializeField] ParticleSystem cookOffFx;
        [SerializeField] bool drawOnGui = true;
        [SerializeField] bool buildUguiStrip = true;

        readonly Dictionary<string, float> _hp = new Dictionary<string, float>();
        readonly Dictionary<string, Renderer> _zoneRenderers = new Dictionary<string, Renderer>();
        readonly Dictionary<string, Transform> _zoneRoots = new Dictionary<string, Transform>();

        VehicleController _vehicle;
        bool _fireLit;
        GameObject _firePlaceholder;
        GameObject _uguiRoot;
        UnityEngine.UI.Text[] _iconLabels;
        public bool IsCookedOff { get; private set; }
        public IReadOnlyDictionary<string, float> Modules => _hp;

        static readonly Dictionary<string, string> RuShort = new Dictionary<string, string>
        {
            { "hull_f", "Лоб" }, { "hull_s", "Борт" }, { "hull_r", "Корма" },
            { "turret", "Башн" }, { "gun", "Оруд" }, { "engine", "Двиг" },
            { "ammo", "БК" }, { "track_l", "ГусЛ" }, { "track_r", "ГусП" },
            { "fuel", "Топл" }, { "optics", "Приц" },
        };

        void Awake()
        {
            _vehicle = GetComponent<VehicleController>();
            foreach (var k in ModuleIds) _hp[k] = 1f;
            EnsureZoneColliders();
            if (buildUguiStrip) TryBuildUguiStrip();
        }

        void EnsureZoneColliders()
        {
            CreateZone("hull_f", new Vector3(0f, 1f, 2.2f), new Vector3(2.2f, 1f, 0.55f), new Color(0.55f, 0.6f, 0.45f));
            CreateZone("hull_s", new Vector3(1.35f, 1f, 0f), new Vector3(0.4f, 1f, 3.6f), new Color(0.5f, 0.55f, 0.4f));
            CreateZone("hull_r", new Vector3(0f, 1f, -2.1f), new Vector3(2.0f, 1f, 0.55f), new Color(0.45f, 0.5f, 0.38f));
            CreateZone("turret", new Vector3(0f, 1.9f, 0.2f), new Vector3(1.5f, 0.7f, 1.6f), new Color(0.6f, 0.58f, 0.42f));
            CreateZone("gun", new Vector3(0f, 1.85f, 2.4f), new Vector3(0.35f, 0.35f, 2.2f), new Color(0.35f, 0.35f, 0.32f));
            CreateZone("engine", new Vector3(0f, 1f, -1.6f), new Vector3(1.6f, 0.9f, 1.1f), new Color(0.4f, 0.35f, 0.3f));
            CreateZone("ammo", new Vector3(0f, 1.1f, 0.3f), new Vector3(1.1f, 0.75f, 1.1f), new Color(0.55f, 0.4f, 0.25f));
            CreateZone("track_l", new Vector3(-1.35f, 0.35f, 0f), new Vector3(0.4f, 0.55f, 4.2f), new Color(0.25f, 0.25f, 0.22f));
            CreateZone("track_r", new Vector3(1.35f, 0.35f, 0f), new Vector3(0.4f, 0.55f, 4.2f), new Color(0.25f, 0.25f, 0.22f));
            CreateZone("fuel", new Vector3(0.8f, 0.9f, -1.2f), new Vector3(0.7f, 0.6f, 0.9f), new Color(0.35f, 0.3f, 0.2f));
            CreateZone("optics", new Vector3(0f, 2.25f, 0.6f), new Vector3(0.5f, 0.25f, 0.5f), new Color(0.2f, 0.35f, 0.45f));
        }

        void CreateZone(string id, Vector3 localPos, Vector3 size, Color baseColor)
        {
            var go = new GameObject("Zone_" + id);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var zone = go.AddComponent<ModuleHitZone>();
            zone.ModuleId = id;
            var net = GetComponent<NetUnitId>();
            zone.OwnerUnitId = net != null ? net.Id : null;

            // Visible hit-zone proxy (dim cube) for color/emission feedback
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Vis_" + id;
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = size * 0.92f;
            Object.Destroy(vis.GetComponent<Collider>());
            var r = vis.GetComponent<Renderer>();
            if (r)
            {
                r.material.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
                // Transparent-ish default; damage shifts toward red + emission
                TryEnableEmission(r.material, Color.black, 0f);
            }
            _zoneRenderers[id] = r;
            _zoneRoots[id] = go.transform;
        }

        static void TryEnableEmission(Material mat, Color c, float intensity)
        {
            if (mat == null) return;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * intensity);
            }
        }

        public void ApplyServerModules(Dictionary<string, float> server)
        {
            if (server == null) return;
            foreach (var kv in server)
                SetModule(kv.Key, kv.Value);
        }

        public void SetModule(string id, float normalized)
        {
            id = NormalizeId(id);
            if (!_hp.ContainsKey(id)) _hp[id] = 1f;
            float v = Mathf.Clamp01(normalized);
            float prev = _hp[id];
            _hp[id] = v;
            OnModuleChanged(id, v, prev);
            RefreshZoneVisual(id, v);
            RefreshUiStrip();
        }

        static string NormalizeId(string id)
        {
            if (id == "hull_front") return "hull_f";
            if (id == "hull_side") return "hull_s";
            if (id == "hull_rear") return "hull_r";
            return id;
        }

        public void ApplyHit(string moduleId, float damageNormalized)
        {
            moduleId = NormalizeId(moduleId);
            if (!_hp.ContainsKey(moduleId)) _hp[moduleId] = 1f;
            SetModule(moduleId, _hp[moduleId] - damageNormalized);
            FlashZone(moduleId);
        }

        public void ForceFireVisual(bool on)
        {
            if (on) LightFire();
            else if (_firePlaceholder) _firePlaceholder.SetActive(false);
        }

        public void TriggerCookOffExternal() => TriggerCookOff();

        void OnModuleChanged(string id, float v, float prev)
        {
            switch (id)
            {
                case "engine":
                    if (_vehicle) _vehicle.EngineDead = v <= 0.05f;
                    if (v < 0.4f) LightFire();
                    break;
                case "track_l":
                case "track_r":
                    bool broken = Get("track_l") <= 0.05f || Get("track_r") <= 0.05f;
                    if (_vehicle) _vehicle.TracksBroken = broken;
                    ApplyTrackBreakVisual(id, v);
                    break;
                case "ammo":
                    if (v <= 0.15f) TriggerCookOff();
                    else if (v < 0.5f) LightFire();
                    break;
                case "fuel":
                    if (v < 0.35f) LightFire();
                    break;
                case "hull_f":
                case "hull_s":
                case "hull_r":
                    if (v < 0.25f) LightFire();
                    break;
                case "gun":
                case "optics":
                case "turret":
                    break;
            }
            if (prev > 0.05f && v <= 0.05f)
                Debug.Log($"[ModuleDamage] BREAK {id} on {name}");
        }

        float Get(string id) => _hp.TryGetValue(id, out float v) ? v : 1f;

        void RefreshZoneVisual(string id, float v)
        {
            if (!_zoneRenderers.TryGetValue(id, out var r) || r == null) return;
            // Healthy olive → damaged red; broken = dark + emission pulse
            Color healthy = new Color(0.45f, 0.5f, 0.35f);
            Color damaged = new Color(0.85f, 0.2f, 0.08f);
            Color broken = new Color(0.15f, 0.05f, 0.02f);
            Color c = v > 0.05f ? Color.Lerp(damaged, healthy, v) : broken;
            r.material.color = c;
            float emit = v < 0.35f ? (0.35f - v) * 4f : 0f;
            TryEnableEmission(r.material, damaged, emit);
        }

        void FlashZone(string id)
        {
            if (!_zoneRenderers.TryGetValue(id, out var r) || r == null) return;
            TryEnableEmission(r.material, Color.yellow, 2.5f);
        }

        void ApplyTrackBreakVisual(string id, float v)
        {
            if (!_zoneRoots.TryGetValue(id, out var t) || t == null) return;
            if (v > 0.35f)
            {
                t.localRotation = Quaternion.identity;
                return;
            }
            // Tilt / sag broken track + spark proxy
            float sag = (0.35f - v) * 25f;
            float side = id == "track_l" ? -1f : 1f;
            t.localRotation = Quaternion.Euler(0f, 0f, side * sag);
            if (v <= 0.05f && t.Find("Spark") == null)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "Spark";
                spark.transform.SetParent(t, false);
                spark.transform.localPosition = Vector3.up * 0.4f;
                spark.transform.localScale = Vector3.one * 0.2f;
                Object.Destroy(spark.GetComponent<Collider>());
                var sr = spark.GetComponent<Renderer>();
                if (sr)
                {
                    sr.material.color = new Color(1f, 0.7f, 0.2f);
                    TryEnableEmission(sr.material, new Color(1f, 0.5f, 0.1f), 2f);
                }
            }
        }

        void LightFire()
        {
            if (_fireLit) return;
            _fireLit = true;
            if (fireFx != null) { fireFx.Play(); return; }
            var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flame.name = "FirePlaceholder";
            flame.transform.SetParent(transform, false);
            flame.transform.localPosition = new Vector3(0f, 2.2f, -0.5f);
            flame.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
            Object.Destroy(flame.GetComponent<Collider>());
            var r = flame.GetComponent<Renderer>();
            if (r)
            {
                r.material.color = new Color(1f, 0.35f, 0.05f);
                TryEnableEmission(r.material, new Color(1f, 0.4f, 0.05f), 1.5f);
            }
            var light = flame.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.1f);
            light.range = 8f;
            light.intensity = 2.5f;
            _firePlaceholder = flame;
        }

        void TriggerCookOff()
        {
            if (IsCookedOff) return;
            IsCookedOff = true;
            LightFire();
            if (cookOffFx != null) cookOffFx.Play();
            else
            {
                for (int i = 0; i < 10; i++)
                {
                    var bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bit.name = "CookOffBit";
                    bit.transform.position = transform.position + Vector3.up * 1.5f;
                    bit.transform.localScale = Vector3.one * 0.22f;
                    Object.Destroy(bit.GetComponent<Collider>());
                    var rb = bit.AddComponent<Rigidbody>();
                    rb.velocity = Random.onUnitSphere * 9f + Vector3.up * 7f;
                    var r = bit.GetComponent<Renderer>();
                    if (r) r.material.color = new Color(1f, 0.55f, 0.1f);
                    Object.Destroy(bit, 2.2f);
                }
                var flash = new GameObject("CookOffFlash").AddComponent<Light>();
                flash.type = LightType.Point;
                flash.color = Color.yellow;
                flash.intensity = 8f;
                flash.range = 20f;
                flash.transform.position = transform.position + Vector3.up * 2f;
                Object.Destroy(flash.gameObject, 0.25f);
            }
            Debug.Log($"[ModuleDamage] COOK-OFF on {name}");
        }

        public string HudText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Модули: ");
            foreach (var k in ModuleIds)
            {
                int pct = Mathf.RoundToInt(Get(k) * 100f);
                string tag = RuShort.TryGetValue(k, out var ru) ? ru : k;
                sb.Append(tag).Append(' ').Append(pct).Append("%  ");
            }
            if (IsCookedOff) sb.Append("\nВЗРЫВ БК");
            if (_fireLit) sb.Append("  · ОГОНЬ");
            return sb.ToString();
        }

        void TryBuildUguiStrip()
        {
            // Runtime strip under screen bottom — optional; OnGUI always available.
            if (UnityEngine.Object.FindObjectOfType<Canvas>() == null) return;
            // Defer until battle HUD exists; BattleBootstrap may call EnsureUi.
        }

        public void EnsureUiStrip(Transform canvasRoot)
        {
            if (canvasRoot == null || _uguiRoot != null) return;
            _uguiRoot = new GameObject("ModuleStrip");
            _uguiRoot.transform.SetParent(canvasRoot, false);
            var rt = _uguiRoot.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.02f);
            rt.anchorMax = new Vector2(0.95f, 0.11f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _iconLabels = new UnityEngine.UI.Text[ModuleIds.Length];
            float w = 1f / ModuleIds.Length;
            for (int i = 0; i < ModuleIds.Length; i++)
            {
                var go = new GameObject(ModuleIds[i]);
                go.transform.SetParent(_uguiRoot.transform, false);
                var img = go.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.12f, 0.13f, 0.1f, 0.85f);
                var irt = go.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(i * w, 0f);
                irt.anchorMax = new Vector2((i + 1) * w, 1f);
                irt.offsetMin = new Vector2(2, 2);
                irt.offsetMax = new Vector2(-2, -2);
                var labelGo = new GameObject("t");
                labelGo.transform.SetParent(go.transform, false);
                var tx = labelGo.AddComponent<UnityEngine.UI.Text>();
                tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                tx.fontSize = 11;
                tx.alignment = TextAnchor.MiddleCenter;
                tx.color = Color.white;
                tx.text = RuShort[ModuleIds[i]] + "\n100";
                var lrt = tx.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                _iconLabels[i] = tx;
            }
            RefreshUiStrip();
        }

        void RefreshUiStrip()
        {
            if (_iconLabels == null) return;
            for (int i = 0; i < ModuleIds.Length && i < _iconLabels.Length; i++)
            {
                string k = ModuleIds[i];
                float v = Get(k);
                var tx = _iconLabels[i];
                if (tx == null) continue;
                tx.text = RuShort[k] + "\n" + Mathf.RoundToInt(v * 100f);
                tx.color = v > 0.5f ? Color.white : (v > 0.15f ? new Color(1f, 0.75f, 0.3f) : new Color(1f, 0.25f, 0.15f));
                var img = tx.transform.parent.GetComponent<UnityEngine.UI.Image>();
                if (img) img.color = Color.Lerp(new Color(0.45f, 0.08f, 0.05f, 0.9f), new Color(0.12f, 0.13f, 0.1f, 0.85f), v);
            }
        }

        void OnGUI()
        {
            if (!drawOnGui || _uguiRoot != null) return;
            if (_vehicle != null && !_vehicle.IsLocalPlayer) return;
            const float h = 22f;
            float y = Screen.height - (ModuleIds.Length * h + 12f);
            GUI.Box(new Rect(8, y - 8, 210, ModuleIds.Length * h + 16), "Модули");
            for (int i = 0; i < ModuleIds.Length; i++)
            {
                string k = ModuleIds[i];
                float v = Get(k);
                var c = GUI.color;
                GUI.color = v > 0.5f ? Color.white : (v > 0.15f ? Color.yellow : Color.red);
                GUI.Label(new Rect(16, y + i * h, 200, h),
                    $"{RuShort[k]}  [{new string('|', Mathf.RoundToInt(v * 10)).PadRight(10)}] {Mathf.RoundToInt(v * 100)}%");
                GUI.color = c;
            }
        }
    }
}
