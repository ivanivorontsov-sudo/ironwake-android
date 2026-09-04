using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Ironwake.Meta
{
    /// <summary>
    /// Placeholder for Android Google Sign-In → server POST /auth/google.
    ///
    /// Setup (Android):
    /// 1. Firebase / Google Cloud Console → OAuth 2.0 Client ID (Web application)
    ///    — this Web client ID is what the server verifies (audience).
    /// 2. Also create an Android OAuth client with your keystore SHA-1
    ///    (package: com.ironwake.combat).
    /// 3. Install "Google Sign-In Unity Plugin" or Google Play Games Plugin v2,
    ///    request an ID token for the Web client ID.
    /// 4. Call <see cref="ExchangeIdToken"/> with the ID token string.
    /// 5. Server endpoint: POST http://biker9td.beget.tech/auth/google
    ///    body: { "credential": "<id_token>" } → { user: { id, callsign, ... } }
    ///
    /// Until the plugin is wired, <see cref="GuestLogin"/> keeps anonymous userIds
    /// (compatible with current HTTP room join).
    /// </summary>
    public sealed class GoogleAuthPlaceholder : MonoBehaviour
    {
        [SerializeField] string baseUrl = "http://biker9td.beget.tech";
        [SerializeField] string webClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

        public string WebClientId => webClientId;
        public string UserId { get; private set; }
        public string Callsign { get; private set; } = "OPERATOR";
        public bool IsGuest { get; private set; } = true;

        public event Action<bool, string> OnAuthFinished;

        public void GuestLogin()
        {
            IsGuest = true;
            UserId = "g" + Guid.NewGuid().ToString("N").Substring(0, 10);
            Callsign = "OPERATOR";
            PlayerPrefs.SetString("iw.userId", UserId);
            PlayerPrefs.SetString("iw.callsign", Callsign);
            OnAuthFinished?.Invoke(true, "guest");
            Debug.Log("[GoogleAuth] guest " + UserId);
        }

        /// <summary>
        /// Exchange a Google ID token with the IRONWAKE server.
        /// Wire this after the native Google Sign-In plugin returns a token.
        /// </summary>
        public void ExchangeIdToken(string idToken)
        {
            StartCoroutine(ExchangeCoroutine(idToken));
        }

        IEnumerator ExchangeCoroutine(string idToken)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                OnAuthFinished?.Invoke(false, "empty token");
                yield break;
            }
            string url = baseUrl.TrimEnd('/') + "/auth/google";
            byte[] body = Encoding.UTF8.GetBytes("{\"credential\":\"" + Escape(idToken) + "\"}");
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[GoogleAuth] " + req.error);
                OnAuthFinished?.Invoke(false, req.error);
                yield break;
            }
            // Expect { "user": { "id": "...", "callsign": "..." } } — parse lightly.
            string json = req.downloadHandler.text;
            IsGuest = false;
            if (TryPick(json, "id", out var id)) UserId = id;
            if (TryPick(json, "callsign", out var cs) || TryPick(json, "name", out cs)) Callsign = cs;
            PlayerPrefs.SetString("iw.userId", UserId ?? "");
            PlayerPrefs.SetString("iw.callsign", Callsign ?? "OPERATOR");
            OnAuthFinished?.Invoke(true, json);
            Debug.Log("[GoogleAuth] ok " + UserId);
        }

        /// <summary>
        /// Editor / CI stub: pretends Google returned a token (will fail against live
        /// server unless GOOGLE_CLIENT_ID is unset / bypassed). Prefer GuestLogin for tests.
        /// </summary>
        public void SimulateEditorSignIn()
        {
            Debug.LogWarning("[GoogleAuth] SimulateEditorSignIn — use GuestLogin for live room tests. " +
                             "WebClientId=" + webClientId);
            GuestLogin();
        }

        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static bool TryPick(string json, string key, out string value)
        {
            value = null;
            string needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0) return false;
            int q1 = json.IndexOf('"', i + 1);
            if (q1 < 0) return false;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return false;
            value = json.Substring(q1 + 1, q2 - q1 - 1);
            return true;
        }
    }
}
