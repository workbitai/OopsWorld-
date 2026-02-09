using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace NewGame.API
{
    public class ApiManager : MonoBehaviour
    {
        public static ApiManager Instance;

        [Header("Request Settings")]
        [SerializeField] private int requestTimeoutSeconds = 12;

        [Header("Internet Overlay (Optional)")]
        [SerializeField] private bool showNoInternetOverlay = true;
        [SerializeField] private float internetOverlayCheckIntervalSeconds = 0.5f;
        [SerializeField] private string noInternetOverlayMessage = "No internet connection. Please check and try again.";

        [SerializeField] private GameLoaderPanelAnimator internetOverlayPanel;
        private Coroutine internetOverlayRoutine;
        private bool internetOverlayShown;

        [Header("API Base URL")]
        [Tooltip("Example: http://192.168.1.7:4000/api  (change this one field when server changes)")]
        [SerializeField] private string apiBaseUrl = "http://192.168.1.7:4000/api";

        [Header("API Paths (Relative)")]
        [SerializeField] private string loginApiPath = "/Users/Login";
        [SerializeField] private string gameWalletSelectPath = "/GameWallet/Select";
        [SerializeField] private string userUpdateApiPath = "/Users/Update";
        [SerializeField] private string userCreditUpdateApiPath = "/Users/CreditUpdate";
        [SerializeField] private string userDebitUpdateApiPath = "/Users/DebitUpdate";

        [Header("API URLs (Legacy Fallback)")]
        [SerializeField] private string loginApiUrl = "http://192.168.1.12:4000/api/Users/Login";
        [SerializeField] private string gameWalletSelectUrl = "http://192.168.1.12:4000/api/GameWallet/Select";
        [SerializeField] private string userUpdateApiUrl = "http://192.168.1.7:4000/api/Users/Update";
        [SerializeField] private string userCreditUpdateApiUrl = "http://192.168.1.7:4000/api/Users/CreditUpdate";
        [SerializeField] private string userDebitUpdateApiUrl = "http://192.168.1.7:4000/api/Users/DebitUpdate";

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            showNoInternetOverlay = false;
            StopInternetOverlayMonitor();
            HideNoInternetOverlayIfNeeded();
        }

        private void OnEnable()
        {
            showNoInternetOverlay = false;
            StartInternetOverlayMonitorIfNeeded();
        }

        private void OnDisable()
        {
            StopInternetOverlayMonitor();
        }

        private void StartInternetOverlayMonitorIfNeeded()
        {
            if (!Application.isPlaying) return;
            if (!showNoInternetOverlay) return;
            if (internetOverlayRoutine != null) return;
            internetOverlayRoutine = StartCoroutine(InternetOverlayLoop());
        }

        private void StopInternetOverlayMonitor()
        {
            if (internetOverlayRoutine == null) return;
            StopCoroutine(internetOverlayRoutine);
            internetOverlayRoutine = null;
        }

        private IEnumerator InternetOverlayLoop()
        {
            while (true)
            {
                float interval = Mathf.Max(0.1f, internetOverlayCheckIntervalSeconds);
                yield return new WaitForSecondsRealtime(interval);

                if (!showNoInternetOverlay)
                {
                    HideNoInternetOverlayIfNeeded();
                    continue;
                }

                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    ShowNoInternetOverlayIfNeeded();
                }
                else
                {
                    HideNoInternetOverlayIfNeeded();
                }
            }
        }

        private void ShowNoInternetOverlayIfNeeded()
        {
            if (internetOverlayShown) return;

            if (internetOverlayPanel == null)
            {
                internetOverlayPanel = FindObjectOfType<GameLoaderPanelAnimator>(true);
            }
            if (internetOverlayPanel == null) return;

            internetOverlayShown = true;
            internetOverlayPanel.gameObject.SetActive(true);
            internetOverlayPanel.ShowError(noInternetOverlayMessage);
        }

        private void HideNoInternetOverlayIfNeeded()
        {
            if (!internetOverlayShown) return;

            if (internetOverlayPanel == null)
            {
                internetOverlayPanel = FindObjectOfType<GameLoaderPanelAnimator>(true);
            }

            if (internetOverlayPanel != null && internetOverlayPanel.gameObject.activeSelf)
            {
                internetOverlayPanel.Hide();
            }

            internetOverlayShown = false;
        }

        public string GetLoginApiUrl()
        {
            return BuildUrl(loginApiPath, loginApiUrl);
        }

        public string GetGameWalletSelectUrl()
        {
            return BuildUrl(gameWalletSelectPath, gameWalletSelectUrl);
        }

        public string GetUserUpdateApiUrl()
        {
            return BuildUrl(userUpdateApiPath, userUpdateApiUrl);
        }

        public string GetUserCreditUpdateApiUrl()
        {
            return BuildUrl(userCreditUpdateApiPath, userCreditUpdateApiUrl);
        }

        public string GetUserDebitUpdateApiUrl()
        {
            return BuildUrl(userDebitUpdateApiPath, userDebitUpdateApiUrl);
        }

        private string BuildUrl(string relativePath, string legacyFallback)
        {
            // If base is not set, fall back to existing full URL.
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                return legacyFallback;
            }

            string baseTrim = apiBaseUrl.Trim();
            if (baseTrim.EndsWith("/")) baseTrim = baseTrim.TrimEnd('/');

            string rel = relativePath ?? string.Empty;
            rel = rel.Trim();
            if (rel.Length > 0 && !rel.StartsWith("/")) rel = "/" + rel;

            return baseTrim + rel;
        }

        // 🔹 COMMON POST API
        public void Post(
            string url,
            string jsonBody,
            Action<string> onSuccess,
            Action<string> onError = null
        )
        {
            StartCoroutine(PostCoroutine(url, jsonBody, onSuccess, onError));
        }

        private IEnumerator PostCoroutine(
            string url,
            string jsonBody,
            Action<string> onSuccess,
            Action<string> onError
        )
        {
            using (UnityWebRequest request =
                   new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                int timeout = Mathf.Max(1, requestTimeoutSeconds);
                request.timeout = timeout;

                string token = PlayerPrefs.GetString(UserSession.JwtTokenKey, string.Empty);
                if (!string.IsNullOrEmpty(token))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                }

                Debug.Log($"<color=cyan><b>API POST</b></color> → {url}");
                Debug.Log($"<color=yellow>BODY</color> → {jsonBody}");

                float startedAt = Time.realtimeSinceStartup;
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        request.Abort();
                        break;
                    }

                    if (Time.realtimeSinceStartup - startedAt > timeout)
                    {
                        request.Abort();
                        break;
                    }

                    yield return null;
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"<color=green><b>API SUCCESS</b></color> → {request.downloadHandler.text}");
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    string err = request.error;
                    if (string.IsNullOrWhiteSpace(err) && Time.realtimeSinceStartup - startedAt > timeout)
                    {
                        err = "Request timed out. Please try again.";
                    }
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        err = "No internet connection. Please check and try again.";
                    }
                    else if (!string.IsNullOrWhiteSpace(err) && err.IndexOf("destination host", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        err = "Cannot reach server. Please try again.";
                    }

                    Debug.LogError($"<color=red><b>API ERROR</b></color> → {err}");
                    onError?.Invoke(err);
                }
            }
        }
    }
}
