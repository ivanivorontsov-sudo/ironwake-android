using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ironwake.Combat;
using Ironwake.Sim;
using Ironwake.Net;

namespace Ironwake.Controls
{
    /// <summary>
    /// Virtual joystick (move) + fire button + aim drag for mobile battle.
    /// Also feeds keyboard/mouse in editor.
    /// </summary>
    public sealed class MobileBattleInput : MonoBehaviour
    {
        [SerializeField] float aimSensitivity = 2.2f;
        [SerializeField] bool showOnDesktop = true;

        VehicleController _vc;
        LocalBattleSim _sim;
        RectTransform _joyKnob;
        int _joyFinger = -1;
        int _aimFinger = -1;
        Vector2 _joyCenter;
        Vector2 _aimLast;
        float _throttle, _steer;
        float _lookX, _lookY;
        bool _fireHeld;
        bool _brake;
        bool _firePulse;

        public void Bind(VehicleController vc, LocalBattleSim sim = null)
        {
            _vc = vc;
            _sim = sim;
            BuildUi();
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("MobileBattleInput");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var joyBase = MakePanel(canvasGo.transform, "JoyBase", new Vector2(0.14f, 0.2f), new Vector2(220, 220),
                new Color(0.1f, 0.12f, 0.08f, 0.45f));
            _joyKnob = MakePanel(joyBase, "JoyKnob", new Vector2(0.5f, 0.5f), new Vector2(80, 80),
                new Color(0.75f, 0.7f, 0.45f, 0.75f));

            MakePanel(canvasGo.transform, "AimZone", new Vector2(0.75f, 0.45f), new Vector2(700, 700),
                new Color(0f, 0f, 0f, 0.01f));

            var fire = MakeButton(canvasGo.transform, "ОГОНЬ", new Vector2(0.88f, 0.18f), new Vector2(130, 130),
                new Color(0.55f, 0.18f, 0.14f, 0.85f));
            var fireTrig = fire.gameObject.AddComponent<PointerHold>();
            fireTrig.OnDown = () => { _fireHeld = true; _firePulse = true; };
            fireTrig.OnUp = () => _fireHeld = false;

            MakeButton(canvasGo.transform, "ТОРМОЗ", new Vector2(0.72f, 0.12f), new Vector2(110, 56),
                new Color(0.25f, 0.25f, 0.2f, 0.8f)).onClick.AddListener(() => _brake = true);

            MakeButton(canvasGo.transform, "КАМЕРА", new Vector2(0.5f, 0.94f), new Vector2(140, 48),
                new Color(0.2f, 0.25f, 0.18f, 0.8f)).onClick.AddListener(() => _vc?.ToggleCam());

            MakeLabel(canvasGo.transform, "Джойстик · свайп прицел · ОГОНЬ", new Vector2(0.5f, 0.02f));

            if (!showOnDesktop && Application.isEditor && !Application.isMobilePlatform)
                canvasGo.SetActive(false);
        }

        void Update()
        {
            if (_vc == null) return;
            ReadTouches();
            ReadKeyboardFallback();

            _vc.SetMove(_steer, _throttle);
            if (_brake) { _vc.SetBrake(true); _brake = false; }
            if (Mathf.Abs(_lookX) > 0.001f || Mathf.Abs(_lookY) > 0.001f)
                _vc.SetLook(_lookX, _lookY);

            if (_firePulse || _fireHeld)
                _vc.Fire();
            // LocalBattleSim input is pushed from VehicleController.PushLocalSimInput

            _lookX = 0f;
            _lookY = 0f;
            _firePulse = false;
        }

        void ReadTouches()
        {
            int touchCount = UnityEngine.Input.touchCount;
            if (touchCount == 0)
            {
                if (_joyFinger < 0)
                {
                    _throttle = Mathf.MoveTowards(_throttle, 0f, Time.deltaTime * 3f);
                    _steer = Mathf.MoveTowards(_steer, 0f, Time.deltaTime * 3f);
                    if (_joyKnob) _joyKnob.anchoredPosition = Vector2.zero;
                }
            }

            for (int i = 0; i < touchCount; i++)
            {
                Touch t = UnityEngine.Input.GetTouch(i);
                HandlePointer(t.fingerId, t.position, t.phase);
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (UnityEngine.Input.GetMouseButtonDown(0))
                HandlePointer(100, UnityEngine.Input.mousePosition, TouchPhase.Began);
            else if (UnityEngine.Input.GetMouseButton(0))
                HandlePointer(100, UnityEngine.Input.mousePosition, TouchPhase.Moved);
            else if (UnityEngine.Input.GetMouseButtonUp(0))
                HandlePointer(100, UnityEngine.Input.mousePosition, TouchPhase.Ended);
#endif
        }

        void HandlePointer(int id, Vector2 screen, TouchPhase phase)
        {
            bool left = screen.x < Screen.width * 0.45f;

            if (phase == TouchPhase.Began)
            {
                if (left && _joyFinger < 0)
                {
                    _joyFinger = id;
                    _joyCenter = screen;
                }
                else if (!left && _aimFinger < 0)
                {
                    _aimFinger = id;
                    _aimLast = screen;
                }
            }
            else if (phase == TouchPhase.Moved || phase == TouchPhase.Stationary)
            {
                if (id == _joyFinger)
                {
                    Vector2 delta = screen - _joyCenter;
                    float maxR = 90f;
                    Vector2 clamped = Vector2.ClampMagnitude(delta, maxR);
                    if (_joyKnob) _joyKnob.anchoredPosition = clamped;
                    _steer = Mathf.Clamp(clamped.x / maxR, -1f, 1f);
                    _throttle = Mathf.Clamp(clamped.y / maxR, -1f, 1f);
                }
                else if (id == _aimFinger)
                {
                    Vector2 d = (screen - _aimLast) / Mathf.Max(1f, Screen.height);
                    _lookX += d.x * aimSensitivity * 60f;
                    _lookY += d.y * aimSensitivity * 40f;
                    _aimLast = screen;
                }
            }
            else if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
            {
                if (id == _joyFinger)
                {
                    _joyFinger = -1;
                    if (_joyKnob) _joyKnob.anchoredPosition = Vector2.zero;
                }
                if (id == _aimFinger) _aimFinger = -1;
            }
        }

        void ReadKeyboardFallback()
        {
            float thr = 0f, st = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) thr += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) thr -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) st -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) st += 1f;
            if (Mathf.Abs(thr) > 0.01f || Mathf.Abs(st) > 0.01f)
            {
                _throttle = thr;
                _steer = st;
            }
            _lookX += UnityEngine.Input.GetAxis("Mouse X");
            _lookY += UnityEngine.Input.GetAxis("Mouse Y");
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) _brake = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetMouseButtonDown(1))
                _firePulse = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.V))
                _vc?.ToggleCam();
        }

        static RectTransform MakePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        static Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 size, Color col)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            var tx = MakeLabel(go.transform, label, new Vector2(0.5f, 0.5f));
            tx.alignment = TextAnchor.MiddleCenter;
            tx.rectTransform.anchorMin = Vector2.zero;
            tx.rectTransform.anchorMax = Vector2.one;
            tx.rectTransform.offsetMin = tx.rectTransform.offsetMax = Vector2.zero;
            return btn;
        }

        static Text MakeLabel(Transform parent, string text, Vector2 anchor)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = 16;
            t.color = new Color(0.9f, 0.88f, 0.8f);
            t.alignment = TextAnchor.MiddleCenter;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(420, 28);
            return t;
        }

        sealed class PointerHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public System.Action OnDown;
            public System.Action OnUp;
            public void OnPointerDown(PointerEventData eventData) => OnDown?.Invoke();
            public void OnPointerUp(PointerEventData eventData) => OnUp?.Invoke();
        }
    }
}
