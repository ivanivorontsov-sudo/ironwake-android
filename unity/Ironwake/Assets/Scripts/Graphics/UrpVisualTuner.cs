using UnityEngine;
using UnityEngine.Rendering;

namespace Ironwake.Graphics
{
    /// <summary>
    /// Enables bloom / vignette / color adjust via Volume when URP Volume stack is available.
    /// Falls back to QualitySettings + camera HDR (see README).
    /// </summary>
    public sealed class UrpVisualTuner : MonoBehaviour
    {
        [SerializeField] bool preferHdr = true;
        [SerializeField] float bloomIntensity = 0.35f;
        [SerializeField] float vignetteIntensity = 0.28f;
        [SerializeField] float contrast = 8f;
        [SerializeField] float saturation = 5f;

        bool _applied;

        public void Apply()
        {
            if (_applied) return;
            _applied = true;
            ApplyQuality();
            ApplyCameraHdr();
            TryBuildVolume();
        }

        void Start() => Apply();

        void ApplyQuality()
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 2);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        void ApplyCameraHdr()
        {
            foreach (var cam in Object.FindObjectsOfType<Camera>())
            {
                cam.allowHDR = preferHdr;
                cam.allowMSAA = true;
                cam.backgroundColor = new Color(0.5f, 0.6f, 0.7f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                if (cam.farClipPlane < 400f) cam.farClipPlane = 450f;
            }
        }

        void TryBuildVolume()
        {
            var go = new GameObject("IW_GlobalVolume");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;

            bool any = false;
            any |= TryAddNamed(profile, "Bloom", bloomIntensity, 0.9f);
            any |= TryAddNamed(profile, "Vignette", vignetteIntensity, 0.35f);
            any |= TryAddNamed(profile, "ColorAdjustments", contrast, saturation);

            if (!any)
            {
                Debug.Log("[UrpVisualTuner] URP overrides not resolved at runtime — QualitySettings + camera HDR active. " +
                          "In Editor: add Global Volume with Bloom / Vignette / Color Adjustments for full look.");
            }
            else
            {
                Debug.Log("[UrpVisualTuner] Global Volume overrides applied.");
            }
        }

        bool TryAddNamed(VolumeProfile profile, string shortName, float a, float b)
        {
            var t = FindUrpOverride(shortName);
            if (t == null) return false;

            // VolumeProfile.Add(Type) via reflection for mobile/editor compatibility
            var add = typeof(VolumeProfile).GetMethod("Add", new[] { typeof(System.Type), typeof(bool) });
            object compObj = null;
            if (add != null)
            {
                compObj = add.Invoke(profile, new object[] { t, true });
            }
            else
            {
                // ScriptableObject.CreateInstance + list inject fallback
                compObj = ScriptableObject.CreateInstance(t);
                var componentsField = typeof(VolumeProfile).GetField("components",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (componentsField != null && componentsField.GetValue(profile) is System.Collections.IList list)
                    list.Add(compObj);
            }

            if (compObj is VolumeComponent comp)
            {
                comp.active = true;
                if (shortName == "Bloom")
                {
                    SetFloatParam(comp, "intensity", a);
                    SetFloatParam(comp, "threshold", b);
                    SetFloatParam(comp, "scatter", 0.6f);
                }
                else if (shortName == "Vignette")
                {
                    SetFloatParam(comp, "intensity", a);
                    SetFloatParam(comp, "smoothness", b);
                }
                else if (shortName == "ColorAdjustments")
                {
                    SetFloatParam(comp, "contrast", a);
                    SetFloatParam(comp, "saturation", b);
                    SetFloatParam(comp, "postExposure", 0.15f);
                }
                return true;
            }
            return false;
        }

        static System.Type FindUrpOverride(string shortName)
        {
            string[] candidates =
            {
                $"UnityEngine.Rendering.Universal.{shortName}, Unity.RenderPipelines.Universal.Runtime",
                $"UnityEngine.Rendering.{shortName}, Unity.RenderPipelines.Core.Runtime"
            };
            foreach (var c in candidates)
            {
                var t = System.Type.GetType(c);
                if (t != null) return t;
            }
            foreach (var ass in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in ass.GetTypes())
                    {
                        if (t.Name == shortName && typeof(VolumeComponent).IsAssignableFrom(t))
                            return t;
                    }
                }
                catch { /* ignore */ }
            }
            return null;
        }

        static void SetFloatParam(VolumeComponent comp, string field, float value)
        {
            var f = comp.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f == null) return;
            var param = f.GetValue(comp);
            if (param == null) return;
            var ov = param.GetType().GetProperty("overrideState");
            var val = param.GetType().GetProperty("value");
            ov?.SetValue(param, true);
            if (val != null && val.PropertyType == typeof(float))
                val.SetValue(param, value);
        }
    }
}
