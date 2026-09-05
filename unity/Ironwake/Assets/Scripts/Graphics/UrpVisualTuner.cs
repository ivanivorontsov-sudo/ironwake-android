using UnityEngine;

namespace Ironwake.Graphics
{
    /// <summary>
    /// Lightweight visual tuner for Built-in RP (no URP Volume stack).
    /// </summary>
    public sealed class UrpVisualTuner : MonoBehaviour
    {
        [SerializeField] bool preferHdr = true;

        bool _applied;

        public void Apply()
        {
            if (_applied) return;
            _applied = true;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 2);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            foreach (var cam in Object.FindObjectsOfType<Camera>())
            {
                cam.allowHDR = preferHdr;
                cam.allowMSAA = true;
                cam.backgroundColor = new Color(0.5f, 0.6f, 0.7f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                if (cam.farClipPlane < 400f) cam.farClipPlane = 450f;
            }
        }

        void Start() => Apply();
    }
}
