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
    /// </summary>
    public static class AppBootstrap
    {
        const string BootFlag = "Ironwake.AppBootstrap.Done";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            // Avoid double-boot if domain reload mid-play
            EnsureCameraAndLight();
            EnsureEventSystem();

            string scene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(scene))
                scene = "Hangar";

            if (scene.IndexOf("Battle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                EnsureComponent<BattleBootstrap>("BattleBootstrap");
            else
                EnsureComponent<HangarUI>("HangarUI");
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
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
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
                sun.color = new Color(1f, 0.96f, 0.9f);
                sun.intensity = 1.15f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.shadows = LightShadows.Soft;
            }

            // Visible ambient so unlit/default materials aren't pure black
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.4f);
        }

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // Input System package may be present — prefer both modules if available
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
