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
    /// Hangar main screen via OnGUI only (reliable on Android Built-in RP).
    /// No overlapping uGUI battle buttons.
    /// </summary>
    public sealed class HangarUI : MonoBehaviour
    {
        [SerializeField] VehicleCatalog catalog;
        [SerializeField] string battleSceneName = "Battle";
        [SerializeField] string callsign = "OPERATOR";

        [Header("Optional wired UI (unused for hangar main — OnGUI only)")]
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
        int _vehicleIndex;
        string _statusLine = "Ангар · загрузка…";
        bool _onlineBusy;
        GameObject _runtimeCanvas;

        static HangarUI _instance;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[IRONWAKE] Duplicate HangarUI destroyed");
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Start()
        {
            if (_instance != this) return;

            // Hide any leftover uGUI hangar chrome so OnGUI is the only main screen.
            HideOrDestroyRuntimeCanvas();

            if (catalog == null) catalog = VehicleCatalog.CreateDefaultRuntime();
            if (catalog.vehicles != null && catalog.vehicles.Count > 0)
            {
                string pref = PlayerPrefs.GetString("iw.vehicle", "");
                _selected = catalog.Get(pref) ?? catalog.vehicles[0];
                _vehicleIndex = Mathf.Max(0, catalog.vehicles.IndexOf(_selected));
            }

            EnsureClient();
            RefreshVehicle();
            StartCoroutine(BootSequence());
        }

        void HideOrDestroyRuntimeCanvas()
        {
            // Do not build uGUI hangar buttons — OnGUI owns the main screen.
            if (_runtimeCanvas != null)
            {
                _runtimeCanvas.SetActive(false);
                Destroy(_runtimeCanvas);
                _runtimeCanvas = null;
            }
            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (c != null && c.name == "HangarCanvas")
                    Destroy(c.gameObject);
            }
            // Clear any inspector-wired hangar buttons so they cannot fire / draw.
            if (battleButton) battleButton.gameObject.SetActive(false);
            if (shopButton) shopButton.gameObject.SetActive(false);
            if (achievementsButton) achievementsButton.gameObject.SetActive(false);
        }

        void EnsureClient()
        {
            if (IronwakeClient.Instance != null) return;
            var go = new GameObject("IronwakeClient");
            go.AddComponent<IronwakeClient>();
        }

        void OnGUI()
        {
            if (_instance != this) return;

            float dpi = Mathf.Max(1f, Screen.dpi / 160f);
            float pad = 16f * dpi;
            float bh = Mathf.Max(56f, Screen.height * 0.085f);
            float fontTitle = Mathf.RoundToInt(22 * dpi);
            float fontBtn = Mathf.RoundToInt(20 * dpi);
            float fontBody = Mathf.RoundToInt(16 * dpi);

            var box = new GUIStyle(GUI.skin.box)
            {
                fontSize = (int)fontTitle,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)fontBody,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            body.normal.textColor = new Color(0.92f, 0.9f, 0.82f);
            var btn = new GUIStyle(GUI.skin.button)
            {
                fontSize = (int)fontBtn,
                fontStyle = FontStyle.Bold
            };
            var smallBtn = new GUIStyle(GUI.skin.button) { fontSize = (int)fontBody };

            // Title + status
            float topH = bh * 1.35f;
            GUI.Box(new Rect(pad, pad, Screen.width - pad * 2f, topH),
                "  IRONWAKE · АНГАР\n  " + _statusLine, box);

            // Wallet
            float y = pad + topH + pad * 0.5f;
            string wallet = $"Сталь {_steel}   ·   Разведка {_intel}   ·   Награды {_commendations}";
            GUI.Label(new Rect(pad, y, Screen.width - pad * 2f, bh * 0.55f), wallet, body);
            y += bh * 0.6f;

            // Vehicle prev / name / next
            float navW = Mathf.Max(72f, Screen.width * 0.12f);
            float midW = Screen.width - pad * 2f - navW * 2f - pad * 2f;
            if (GUI.Button(new Rect(pad, y, navW, bh), "◀", smallBtn))
                CycleVehicle(-1);
            string vName = _selected != null
                ? $"{_selected.classLabel}: {_selected.displayName}"
                : "Нет техники";
            GUI.Box(new Rect(pad + navW + pad, y, midW, bh), "  " + vName, box);
            if (GUI.Button(new Rect(pad + navW + pad + midW + pad, y, navW, bh), "▶", smallBtn))
                CycleVehicle(1);
            y += bh + pad * 0.4f;

            if (_selected != null)
            {
                GUI.Label(new Rect(pad, y, Screen.width - pad * 2f, bh * 0.7f),
                    _selected.description ?? "", body);
            }

            // Bottom: ONE local + ONE online — no uGUI duplicates
            float bw = (Screen.width - pad * 3f) * 0.5f;
            float by = Screen.height - bh - pad;
            GUI.enabled = !_onlineBusy;
            if (GUI.Button(new Rect(pad, by, bw, bh), "ЛОКАЛЬНЫЙ БОЙ", btn))
                OnLocalBattle();
            string onlineLabel = _onlineBusy ? "ОНЛАЙН…" : "ОНЛАЙН";
            if (GUI.Button(new Rect(pad * 2f + bw, by, bw, bh), onlineLabel, btn))
                OnOnlineBattle();
            GUI.enabled = true;
        }

        void CycleVehicle(int delta)
        {
            if (catalog == null || catalog.vehicles == null || catalog.vehicles.Count == 0) return;
            _vehicleIndex = (_vehicleIndex + delta + catalog.vehicles.Count) % catalog.vehicles.Count;
            _selected = catalog.vehicles[_vehicleIndex];
            RefreshVehicle();
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
                _vehicleIndex = Mathf.Max(0, catalog.vehicles.IndexOf(_selected));
            }
            RefreshVehicle();

            UserProfile profile = null;
            yield return IronwakeClient.Instance.FetchUser(IronwakeClient.Instance.UserId, u => profile = u);
            if (profile != null)
            {
                _steel = profile.Steel;
                _intel = profile.Intel;
                _commendations = profile.Commendations;
                if (!string.IsNullOrEmpty(profile.Callsign)) callsign = profile.Callsign;
            }

            SetStatus(ok == true
                ? (catOk ? "Ангар · мета с сервера · бой LocalSim на устройстве" : "Ангар · сервер жив · локальный каталог")
                : "Ангар · офлайн · Local Battle доступен");
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
            _statusLine = s ?? "";
            if (statusText) statusText.text = s;
        }

        void EnsureSelected()
        {
            if (catalog == null) catalog = VehicleCatalog.CreateDefaultRuntime();
            if (catalog.vehicles == null || catalog.vehicles.Count == 0)
                catalog.vehicles = VehicleCatalog.BuildFallbackList();
            if (_selected == null)
            {
                _selected = catalog.vehicles[0];
                _vehicleIndex = 0;
            }
        }

        void OnLocalBattle()
        {
            if (_onlineBusy) return;
            EnsureSelected();
            PlayerPrefs.SetString("iw.battleMode", "local");
            PlayerPrefs.SetString("iw.vehicle", _selected.id);
            PlayerPrefs.SetString("iw.callsign", callsign);
            PlayerPrefs.Save();
            if (IronwakeClient.Instance != null)
                PlayerPrefs.SetString("iw.userId", IronwakeClient.Instance.UserId);
            SetStatus("Локальный бой · запуск…");
            Debug.Log("[IRONWAKE] OnLocalBattle → BootBattleHere");
            BootBattleHere();
        }

        void OnOnlineBattle()
        {
            if (_onlineBusy) return;
            EnsureSelected();
            StartCoroutine(JoinAndLoadOnline());
        }

        IEnumerator JoinAndLoadOnline()
        {
            _onlineBusy = true;
            PlayerPrefs.SetString("iw.battleMode", "online");
            SetStatus("Онлайн · вход в комнату… (таймаут 10с)");

            bool finished = false;
            bool ok = false;
            StartCoroutine(RunJoin(success =>
            {
                ok = success;
                finished = true;
            }));

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!finished && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!finished)
            {
                SetStatus("Онлайн таймаут 10с — запускаю локальный бой");
                PlayerPrefs.SetString("iw.battleMode", "local");
                PlayerPrefs.SetString("iw.vehicle", _selected.id);
                PlayerPrefs.SetString("iw.callsign", callsign);
                if (IronwakeClient.Instance != null)
                    PlayerPrefs.SetString("iw.userId", IronwakeClient.Instance.UserId);
                PlayerPrefs.Save();
                _onlineBusy = false;
                BootBattleHere();
                yield break;
            }

            SetStatus(ok ? IronwakeClient.Instance.LastStatus : "Онлайн недоступен — запускаю локальный бой");
            if (!ok) PlayerPrefs.SetString("iw.battleMode", "local");
            PlayerPrefs.SetString("iw.vehicle", _selected.id);
            PlayerPrefs.SetString("iw.callsign", callsign);
            if (IronwakeClient.Instance != null)
                PlayerPrefs.SetString("iw.userId", IronwakeClient.Instance.UserId);
            PlayerPrefs.Save();
            _onlineBusy = false;

            if (PlayerPrefs.GetString("iw.battleMode", "local") == "online" &&
                Application.CanStreamedLevelBeLoaded(battleSceneName))
                LoadBattle();
            else
                BootBattleHere();
        }

        IEnumerator RunJoin(System.Action<bool> done)
        {
            yield return IronwakeClient.Instance.Join(callsign, _selected.id, "laststand", done);
        }

        void LoadBattle()
        {
            try
            {
                if (!string.IsNullOrEmpty(battleSceneName) && Application.CanStreamedLevelBeLoaded(battleSceneName))
                {
                    Debug.Log("[IRONWAKE] LoadScene " + battleSceneName);
                    SceneManager.LoadScene(battleSceneName);
                    return;
                }
                if (SceneManager.sceneCountInBuildSettings >= 2)
                {
                    Debug.Log("[IRONWAKE] LoadScene build index 1");
                    SceneManager.LoadScene(1);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[IRONWAKE] LoadScene failed: " + ex.Message);
            }
            BootBattleHere();
        }

        void BootBattleHere()
        {
            Debug.Log("[IRONWAKE] BootBattleHere (in-place LocalSim)");
            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (c != null && c.name == "HangarCanvas")
                    Destroy(c.gameObject);
            }
            if (Object.FindObjectOfType<BattleBootstrap>() == null)
                new GameObject("BattleBootstrap").AddComponent<BattleBootstrap>();
            StartCoroutine(RemoveHangarNextFrame());
        }

        IEnumerator RemoveHangarNextFrame()
        {
            yield return null;
            Destroy(gameObject);
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
