using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Ironwake.Meta;
using Ironwake.Combat;

namespace Ironwake.Bootstrap
{
    /// <summary>
    /// Empty YAML scenes ship without MonoBehaviours — this boots Hangar/Battle at runtime
    /// so Android APKs are not a black screen.
    /// Hangar main UI is OnGUI-only (HangarUI); do not spawn duplicate uGUI hangar buttons here.
    /// </summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            EnsureCameraAndLight();
            EnsureEventSystem();

            string scene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(scene))
                scene = "Hangar";

            if (scene.IndexOf("Battle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureComponent<BattleBootstrap>("BattleBootstrap");
            }
            else
            {
                // Exactly one HangarUI — never build a parallel uGUI hangar canvas here.
                EnsureSingleHangarUI();
            }
        }

        static void EnsureSingleHangarUI()
        {
            var existing = Object.FindObjectsOfType<HangarUI>();
            if (existing != null && existing.Length > 1)
            {
                for (int i = 1; i < existing.Length; i++)
                {
                    if (existing[i] != null)
                        Object.Destroy(existing[i].gameObject);
                }
                Debug.Log("[IRONWAKE] AppBootstrap pruned duplicate HangarUI instances");
                return;
            }
            if (existing != null && existing.Length == 1) return;

            var go = new GameObject("HangarUI");
            go.AddComponent<HangarUI>();
            Debug.Log($"[IRONWAKE] AppBootstrap attached HangarUI on scene '{SceneManager.GetActiveScene().name}'");
        }

        static void EnsureCameraAndLight()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.55f, 0.68f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            if (cam.transform.position.sqrMagnitude < 0.01f)
            {
                cam.transform.position = new Vector3(0f, 8f, -14f);
                cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            }

            if (Object.FindObjectOfType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.25f;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.58f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.14f);
            RenderSettings.ambientIntensity = 1.1f;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        static void EnsureComponent<T>(string objectName) where T : Component
        {
            if (Object.FindObjectOfType<T>() != null) return;
            var go = new GameObject(objectName);
            go.AddComponent<T>();
            Debug.Log($"[IRONWAKE] AppBootstrap attached {typeof(T).Name} on scene '{SceneManager.GetActiveScene().name}'");
        }
    }
}
