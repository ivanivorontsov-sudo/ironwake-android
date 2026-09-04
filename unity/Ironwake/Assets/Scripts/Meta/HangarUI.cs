using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Ironwake.Net;
using Ironwake.Vehicles;
using Ironwake.Combat;

namespace Ironwake.Meta
{
    /// <summary>
    /// Hangar meta UI: currencies Сталь / Разведка / Награды, garage, shop stubs, achievements.
    /// Uses uGUI; wire buttons in Hangar scene or auto-build a primitive canvas at runtime.
    /// </summary>
    public sealed class HangarUI : MonoBehaviour
    {
        [SerializeField] VehicleCatalog catalog;
        [SerializeField] string battleSceneName = "Battle";
        [SerializeField] string callsign = "OPERATOR";

        [Header("Optional wired UI")]
        [SerializeField] Text steelText;
        [SerializeField] Text intelText;
        [SerializeField] Text rewardsText;
        [SerializeField] Text statusText;
        [SerializeField] Text vehicleTitle;
        [SerializeField] Text vehicleDesc;
        [SerializeField] Transform vehicleListRoot;
        [SerializeField] Button battleButton;
        [SerializeField] Button shopButton;
        [SerializeField] Button achievementsButton;

        int _steel = 25000;
        int _intel = 150;
        int _rewards;
        VehicleDef _selected;
        bool _builtRuntimeUi;

        void Start()
        {
            if (catalog == null) catalog = VehicleCatalog.CreateDefaultRuntime();
            if (catalog.vehicles.Count > 0) _selected = catalog.vehicles[0];

            EnsureClient();
            if (steelText == null) BuildRuntimeCanvas();
            RefreshWallet();
            RefreshVehicle();
            PopulateList();

            if (battleButton) battleButton.onClick.AddListener(OnBattle);
            if (shopButton) shopButton.onClick.AddListener(OnShop);
            if (achievementsButton) achievementsButton.onClick.AddListener(OnAchievements);

            StartCoroutine(PingHealth());
        }

        void EnsureClient()
        {
            if (IronwakeClient.Instance != null) return;
            var go = new GameObject("IronwakeClient");
            go.AddComponent<IronwakeClient>();
        }

        IEnumerator PingHealth()
        {
            bool? ok = null;
            string detail = null;
            yield return IronwakeClient.Instance.HealthCheck((success, d) => { ok = success; detail = d; });
            SetStatus(ok == true ? "Ангар · сервер жив" : "Ангар · сервер недоступен (локальный режим)");
        }

        void BuildRuntimeCanvas()
        {
            if (_builtRuntimeUi) return;
            _builtRuntimeUi = true;
            var canvasGo = new GameObject("HangarCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            statusText = MakeText(canvasGo.transform, "Status", new Vector2(20, -20), 18, TextAnchor.UpperLeft);
            statusText.rectTransform.sizeDelta = new Vector2(800, 32);
            SetStatus("Ангар · подключение…");

            steelText = MakeText(canvasGo.transform, "Steel", new Vector2(-280, -20), 16, TextAnchor.UpperRight);
            intelText = MakeText(canvasGo.transform, "Intel", new Vector2(-150, -20), 16, TextAnchor.UpperRight);
            rewardsText = MakeText(canvasGo.transform, "Rewards", new Vector2(-20, -20), 16, TextAnchor.UpperRight);

            vehicleTitle = MakeText(canvasGo.transform, "VTitle", new Vector2(20, -80), 28, TextAnchor.UpperLeft);
            vehicleTitle.rectTransform.sizeDelta = new Vector2(600, 40);
            vehicleDesc = MakeText(canvasGo.transform, "VDesc", new Vector2(20, -130), 16, TextAnchor.UpperLeft);
            vehicleDesc.rectTransform.sizeDelta = new Vector2(700, 80);
            vehicleDesc.color = new Color(0.7f, 0.7f, 0.65f);

            var listGo = new GameObject("VehicleList");
            listGo.transform.SetParent(canvasGo.transform, false);
            var listRt = listGo.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0, 0.35f);
            listRt.anchorMax = new Vector2(0.45f, 0.75f);
            listRt.offsetMin = new Vector2(20, 0);
            listRt.offsetMax = new Vector2(-10, 0);
            vehicleListRoot = listGo.transform;

            battleButton = MakeButton(canvasGo.transform, "В БОЙ", new Vector2(0.5f, 0.08f), OnBattle);
            shopButton = MakeButton(canvasGo.transform, "МАГАЗИН", new Vector2(0.2f, 0.08f), OnShop);
            achievementsButton = MakeButton(canvasGo.transform, "ДОСТИЖЕНИЯ", new Vector2(0.8f, 0.08f), OnAchievements);
        }

        static Text MakeText(Transform parent, string name, Vector2 anchored, int size, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.color = new Color(0.9f, 0.88f, 0.82f);
            t.alignment = align;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = align == TextAnchor.UpperRight ? new Vector2(1, 1) : new Vector2(0, 1);
            rt.pivot = align == TextAnchor.UpperRight ? new Vector2(1, 1) : new Vector2(0, 1);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(140, 28);
            return t;
        }

        static Button MakeButton(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.76f, 0.71f, 0.54f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(160, 48);
            rt.anchoredPosition = Vector2.zero;
            var tx = MakeText(go.transform, "Label", Vector2.zero, 18, TextAnchor.MiddleCenter);
            tx.text = label;
            tx.color = new Color(0.1f, 0.09f, 0.06f);
            tx.alignment = TextAnchor.MiddleCenter;
            tx.rectTransform.anchorMin = Vector2.zero;
            tx.rectTransform.anchorMax = Vector2.one;
            tx.rectTransform.offsetMin = tx.rectTransform.offsetMax = Vector2.zero;
            return btn;
        }

        void PopulateList()
        {
            if (vehicleListRoot == null || catalog == null) return;
            for (int i = vehicleListRoot.childCount - 1; i >= 0; i--)
                Destroy(vehicleListRoot.GetChild(i).gameObject);
            foreach (var v in catalog.vehicles)
            {
                var captured = v;
                var btn = MakeButton(vehicleListRoot, v.displayName, new Vector2(0.5f, 0.5f), () =>
                {
                    _selected = captured;
                    RefreshVehicle();
                });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 40);
                rt.anchoredPosition = new Vector2(0, -44f * catalog.vehicles.IndexOf(v));
                btn.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.13f);
                var label = btn.GetComponentInChildren<Text>();
                if (label) { label.color = new Color(0.9f, 0.88f, 0.82f); label.text = $"{v.classLabel}: {v.displayName}"; }
            }
        }

        void RefreshWallet()
        {
            if (steelText) steelText.text = $"Сталь {_steel}";
            if (intelText) intelText.text = $"Разведка {_intel}";
            if (rewardsText) rewardsText.text = $"Награды {_rewards}";
        }

        void RefreshVehicle()
        {
            if (_selected == null) return;
            if (vehicleTitle) vehicleTitle.text = _selected.displayName;
            if (vehicleDesc) vehicleDesc.text = $"{_selected.classLabel}. {_selected.description}";
            PlayerPrefs.SetString("iw.vehicle", _selected.id);
            PlayerPrefs.SetString("iw.callsign", callsign);
        }

        void SetStatus(string s)
        {
            if (statusText) statusText.text = s;
        }

        void OnBattle()
        {
            if (_selected == null) return;
            StartCoroutine(JoinAndLoad());
        }

        IEnumerator JoinAndLoad()
        {
            SetStatus("Вход в комнату…");
            bool ok = false;
            yield return IronwakeClient.Instance.Join(callsign, _selected.id, "laststand", s => ok = s);
            SetStatus(ok ? IronwakeClient.Instance.LastStatus : "Не удалось войти — бой всё равно локально");
            // Pass selection via PlayerPrefs; BattleBootstrap reads it.
            PlayerPrefs.SetString("iw.vehicle", _selected.id);
            if (!string.IsNullOrEmpty(battleSceneName))
                SceneManager.LoadScene(battleSceneName);
            else
            {
                // Fallback: spawn battle in-place for scaffold testing without scene assets.
                BootBattleHere();
            }
        }

        void BootBattleHere()
        {
            var existing = FindObjectOfType<HangarUI>();
            foreach (var c in FindObjectsOfType<Canvas>()) Destroy(c.gameObject);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 40f;
            ground.GetComponent<Renderer>().material.color = new Color(0.36f, 0.41f, 0.28f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var vc = VehicleController.SpawnPrimitive(_selected, new Vector3(8f, 2.2f, 22f));
            vc.SetCallsign(callsign);
            var hud = new GameObject("BattleHud").AddComponent<BattleHudStub>();
            hud.Bind(vc);
            Destroy(existing != null ? existing.gameObject : gameObject);
        }

        void OnShop()
        {
            // Stub economy sink
            if (_steel < 500) { SetStatus("Недостаточно Стали"); return; }
            _steel -= 500;
            _intel += 10;
            RefreshWallet();
            SetStatus("Магазин: куплен ящик разведки (−500 Стали)");
        }

        void OnAchievements()
        {
            SetStatus("Достижения: First Blood · Last Stand · Module Hunter (скоро с сервера)");
        }
    }

    /// <summary>Minimal on-screen battle controls for scaffold / editor play.</summary>
    public sealed class BattleHudStub : MonoBehaviour
    {
        VehicleController _vc;
        ModuleDamagePresenter _mods;
        Text _status;
        Text _modHud;

        public void Bind(VehicleController vc)
        {
            _vc = vc;
            _mods = vc.GetComponent<ModuleDamagePresenter>();
            var canvasGo = new GameObject("BattleCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            _status = CreateLabel(canvasGo.transform, "ST", new Vector2(12, -12));
            _modHud = CreateLabel(canvasGo.transform, "MOD", new Vector2(12, -80));
            _modHud.rectTransform.sizeDelta = new Vector2(280, 100);
            MakeBtn(canvasGo.transform, "ОГОНЬ", new Vector2(0.88f, 0.12f), () => _vc.Fire());
            MakeBtn(canvasGo.transform, "КАМЕРА", new Vector2(0.5f, 0.92f), () => _vc.ToggleCam());
            MakeBtn(canvasGo.transform, "АНГАР", new Vector2(0.1f, 0.92f), () => SceneManager.LoadScene("Hangar"));
        }

        void Update()
        {
            if (_vc == null) return;
            float mx = 0, mz = 0, lx = 0, ly = 0;
            // On-screen virtual stick approximation via keyboard always available
            mx = Input.GetAxisRaw("Horizontal");
            mz = -Input.GetAxisRaw("Vertical");
            if (Input.touchCount > 0)
            {
                // Simple split: left half move, right half look
                foreach (Touch t in Input.touches)
                {
                    Vector2 n = new Vector2(t.position.x / Screen.width, t.position.y / Screen.height);
                    Vector2 d = (n - new Vector2(n.x < 0.45f ? 0.2f : 0.8f, 0.2f)) * 4f;
                    if (n.x < 0.45f) { mx = Mathf.Clamp(d.x, -1, 1); mz = Mathf.Clamp(-d.y, -1, 1); }
                    else { lx = Mathf.Clamp(d.x, -1, 1); ly = Mathf.Clamp(d.y, -1, 1); }
                }
            }
            _vc.SetMove(mx, mz);
            _vc.SetLook(lx, ly);
            var client = IronwakeClient.Instance;
            if (_status) _status.text = client != null ? client.LastStatus : "offline";
            if (_modHud && _mods) _modHud.text = _mods.HudText();
        }

        static Text CreateLabel(Transform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 16;
            t.color = Color.white;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(520, 40);
            return t;
        }

        static void MakeBtn(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction act)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = label == "ОГОНЬ" ? new Color(0.55f, 0.22f, 0.18f) : new Color(0.25f, 0.28f, 0.22f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(act);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = label == "ОГОНЬ" ? new Vector2(90, 90) : new Vector2(120, 40);
            var tx = go.AddComponent<Text>();
            // Text on same GO as Image is awkward — child label:
            Object.Destroy(tx);
            var child = new GameObject("t");
            child.transform.SetParent(go.transform, false);
            var t = child.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = 16;
            var crt = t.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = crt.offsetMax = Vector2.zero;
        }
    }
}
