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
    /// Hangar: Сталь / Разведка / Награды (commendations), vehicle list from
    /// GET /catalog/vehicles, select vehicle, Start Battle → Battle scene.
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
        int _commendations;
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

            StartCoroutine(BootSequence());
        }

        void EnsureClient()
        {
            if (IronwakeClient.Instance != null) return;
            var go = new GameObject("IronwakeClient");
            go.AddComponent<IronwakeClient>();
        }

        IEnumerator BootSequence()
        {
            SetStatus("Ангар · подключение…");
            bool? ok = null;
            yield return IronwakeClient.Instance.HealthCheck((success, _) => ok = success);

            bool catOk = false;
            yield return catalog.FetchFromServer(IronwakeClient.Instance.BaseUrl, s => catOk = s);
            if (catalog.vehicles.Count > 0)
            {
                string pref = PlayerPrefs.GetString("iw.vehicle", "");
                _selected = catalog.Get(pref) ?? catalog.vehicles[0];
            }
            PopulateList();
            RefreshVehicle();

            // Guest wallet defaults; pull /user when profile exists
            UserProfile profile = null;
            yield return IronwakeClient.Instance.FetchUser(IronwakeClient.Instance.UserId, u => profile = u);
            if (profile != null)
            {
                _steel = profile.Steel;
                _intel = profile.Intel;
                _commendations = profile.Commendations;
                if (!string.IsNullOrEmpty(profile.Callsign)) callsign = profile.Callsign;
                RefreshWallet();
            }

            SetStatus(ok == true
                ? (catOk ? "Ангар · каталог с сервера · HTTP poll (WS blocked on Beget)" : "Ангар · сервер жив · локальный каталог")
                : "Ангар · сервер недоступен (локальный режим)");
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
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            statusText = MakeText(canvasGo.transform, "Status", new Vector2(20, -20), 18, TextAnchor.UpperLeft);
            statusText.rectTransform.sizeDelta = new Vector2(900, 32);
            SetStatus("Ангар · подключение…");

            steelText = MakeText(canvasGo.transform, "Steel", new Vector2(-300, -20), 16, TextAnchor.UpperRight);
            intelText = MakeText(canvasGo.transform, "Intel", new Vector2(-160, -20), 16, TextAnchor.UpperRight);
            rewardsText = MakeText(canvasGo.transform, "Rewards", new Vector2(-20, -20), 16, TextAnchor.UpperRight);

            vehicleTitle = MakeText(canvasGo.transform, "VTitle", new Vector2(20, -80), 28, TextAnchor.UpperLeft);
            vehicleTitle.rectTransform.sizeDelta = new Vector2(600, 40);
            vehicleDesc = MakeText(canvasGo.transform, "VDesc", new Vector2(20, -130), 16, TextAnchor.UpperLeft);
            vehicleDesc.rectTransform.sizeDelta = new Vector2(700, 80);
            vehicleDesc.color = new Color(0.7f, 0.7f, 0.65f);

            var listGo = new GameObject("VehicleList");
            listGo.transform.SetParent(canvasGo.transform, false);
            var listRt = listGo.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0, 0.2f);
            listRt.anchorMax = new Vector2(0.48f, 0.72f);
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
            int idx = 0;
            foreach (var v in catalog.vehicles)
            {
                var captured = v;
                int row = idx;
                var btn = MakeButton(vehicleListRoot, v.displayName, new Vector2(0.5f, 0.5f), () =>
                {
                    _selected = captured;
                    RefreshVehicle();
                });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 36);
                rt.anchoredPosition = new Vector2(0, -40f * row);
                btn.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.13f);
                var label = btn.GetComponentInChildren<Text>();
                if (label)
                {
                    label.color = new Color(0.9f, 0.88f, 0.82f);
                    label.fontSize = 14;
                    string cost = v.starter ? "старт" : $"{v.costSteel}⚙/{v.costIntel}🔍";
                    label.text = $"{v.classLabel}: {v.displayName}  [{cost}]";
                }
                idx++;
            }
        }

        void RefreshWallet()
        {
            if (steelText) steelText.text = $"Сталь {_steel}";
            if (intelText) intelText.text = $"Разведка {_intel}";
            if (rewardsText) rewardsText.text = $"Награды {_commendations}";
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
            SetStatus(ok ? IronwakeClient.Instance.LastStatus : "Не удалось войти — бой локально");
            PlayerPrefs.SetString("iw.vehicle", _selected.id);
            PlayerPrefs.SetString("iw.callsign", callsign);
            if (!string.IsNullOrEmpty(battleSceneName) && Application.CanStreamedLevelBeLoaded(battleSceneName))
                SceneManager.LoadScene(battleSceneName);
            else
                BootBattleHere();
        }

        void BootBattleHere()
        {
            foreach (var c in Object.FindObjectsOfType<Canvas>()) Destroy(c.gameObject);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 40f;
            ground.GetComponent<Renderer>().material.color = new Color(0.36f, 0.41f, 0.28f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var boot = new GameObject("BattleBootstrap").AddComponent<BattleBootstrap>();
            Destroy(gameObject);
        }

        void OnShop()
        {
            if (_selected == null) return;
            if (_selected.starter) { SetStatus("Стартовая техника уже доступна"); return; }
            if (_steel < _selected.costSteel || _intel < _selected.costIntel)
            {
                SetStatus($"Нужно {_selected.costSteel} Стали и {_selected.costIntel} Разведки");
                return;
            }
            _steel -= _selected.costSteel;
            _intel -= _selected.costIntel;
            RefreshWallet();
            SetStatus($"Куплено: {_selected.displayName}");
        }

        void OnAchievements()
        {
            SetStatus("Достижения: Первая кровь · Хет-трик · Железная стена · Охотник за модулями (с /achievements)");
        }
    }

    /// <summary>Minimal on-screen battle controls. V = camera, fire button, module strip via presenter.</summary>
    public sealed class BattleHudStub : MonoBehaviour
    {
        VehicleController _vc;
        ModuleDamagePresenter _mods;
        Text _status;
        Text _modHud;
        Text _hint;

        public void Bind(VehicleController vc)
        {
            _vc = vc;
            _mods = vc.GetComponent<ModuleDamagePresenter>();
            var canvasGo = new GameObject("BattleCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            _status = CreateLabel(canvasGo.transform, "ST", new Vector2(12, -12));
            _modHud = CreateLabel(canvasGo.transform, "MOD", new Vector2(12, -56));
            _modHud.rectTransform.sizeDelta = new Vector2(720, 48);
            _modHud.fontSize = 13;
            _hint = CreateLabel(canvasGo.transform, "HINT", new Vector2(12, -110));
            _hint.fontSize = 13;
            _hint.color = new Color(0.75f, 0.78f, 0.7f);
            _hint.text = "WASD ход · мышь башня · ЛКМ/Пробел огонь · V камера · без респавна";
            MakeBtn(canvasGo.transform, "ОГОНЬ", new Vector2(0.88f, 0.12f), () => _vc.Fire());
            MakeBtn(canvasGo.transform, "КАМЕРА V", new Vector2(0.5f, 0.92f), () => _vc.ToggleCam());
            MakeBtn(canvasGo.transform, "АНГАР", new Vector2(0.1f, 0.92f), () =>
            {
                if (Application.CanStreamedLevelBeLoaded("Hangar"))
                    SceneManager.LoadScene("Hangar");
            });
            _mods?.EnsureUiStrip(canvasGo.transform);
        }

        void Update()
        {
            if (_vc == null) return;
            float thr = 0f, st = 0f, lx = 0f, ly = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) thr += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) thr -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) st -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) st += 1f;

            if (Input.touchCount > 0)
            {
                foreach (Touch t in Input.touches)
                {
                    Vector2 n = new Vector2(t.position.x / Screen.width, t.position.y / Screen.height);
                    Vector2 d = (n - new Vector2(n.x < 0.45f ? 0.2f : 0.8f, 0.2f)) * 4f;
                    if (n.x < 0.45f) { st = Mathf.Clamp(d.x, -1, 1); thr = Mathf.Clamp(d.y, -1, 1); }
                    else { lx = Mathf.Clamp(d.x, -1, 1); ly = Mathf.Clamp(d.y, -1, 1); }
                }
            }
            _vc.SetMove(st, thr);
            if (Mathf.Abs(lx) > 0.01f || Mathf.Abs(ly) > 0.01f)
                _vc.SetLook(lx, ly);
            if (Input.GetKey(KeyCode.LeftShift)) _vc.SetBrake(true);

            var client = IronwakeClient.Instance;
            if (_status)
            {
                string transport = client == null ? "offline" :
                    (client.UsingWebSocket ? "ws" : "http-poll");
                string spec = _vc.IsSpectator ? " · СПЕКТАТОР" : "";
                _status.text = (client != null ? client.LastStatus : "offline") + $" [{transport}]{spec}";
            }
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
            rt.sizeDelta = new Vector2(720, 40);
            return t;
        }

        static void MakeBtn(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction act)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = label.StartsWith("ОГОНЬ") ? new Color(0.55f, 0.22f, 0.18f) : new Color(0.25f, 0.28f, 0.22f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(act);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = label.StartsWith("ОГОНЬ") ? new Vector2(90, 90) : new Vector2(120, 40);
            var child = new GameObject("t");
            child.transform.SetParent(go.transform, false);
            var t = child.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = 14;
            var crt = t.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = crt.offsetMax = Vector2.zero;
        }
    }
}
