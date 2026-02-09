using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BestHTTP.SocketIO3;
using BestHTTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NewGame.Socket
{
    public enum SocketState
    {
        None,               // Initial / idle
        CheckingInternet,   // Checking network
        Connecting,         // Socket trying to connect
        Connected,          // Socket connected
        Disconnected,       // Socket was connected & got disconnected
        Error               // Socket error
    }

    public class SocketConnection : MonoBehaviour
    {
        public static SocketConnection Instance;

        [Header("Socket Config")]
        [SerializeField] private string socketUrl;

        [SerializeField] private bool autoStartOnStart = false;

        [Header("Live Socket State")]
        [SerializeField] private SocketState currentState = SocketState.None;
        public SocketState CurrentState => currentState;

        public event Action<SocketState> OnStateChanged;

        [SerializeField] private bool hasReceivedGameStart;
        public bool HasReceivedGameStart => hasReceivedGameStart;

        public SocketManager socketManager;

        private bool wasSocketEverConnected = false;
        private bool isConnecting;

        [Header("Debug (Optional)")]
        [SerializeField] private bool logSocketEvents = true;
        [SerializeField] private int maxLoggedEvents = 30;
        [SerializeField] private List<string> recentSocketEvents = new List<string>();
        [SerializeField] private List<string> autoListenEventNames = new List<string>();
        private readonly HashSet<string> activeAutoListeners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Header("UI (Optional)")]
        [SerializeField] private GameLoaderPanelAnimator connectionPanel;
        [SerializeField] private bool showPanelOnDisconnect = true;
        [SerializeField] private bool showPanelOnError = true;
        [SerializeField] private string disconnectedMessage = "Socket disconnected. Please check internet and try again.";
        [SerializeField] private string errorMessagePrefix = "Connection error: ";

        [Header("Internet Monitor (Optional)")]
        [SerializeField] private bool forceDisconnectWhenNoInternet = true;
        [SerializeField] private bool showPanelWhenNoInternet = true;
        [SerializeField] private float noInternetGraceSeconds = 2f;
        [SerializeField] private float internetCheckIntervalSeconds = 1f;
        [SerializeField] private string noInternetMessage = "No internet connection. Please check and try again.";

        [SerializeField] private bool autoReconnectWhenInternetReturns = false;
        [SerializeField] private float autoReconnectCooldownSeconds = 2f;
        [SerializeField] private bool showReconnectingLoader = true;
        [SerializeField] private string reconnectingBaseText = "Reconnecting";
        [SerializeField] private bool hidePanelOnReconnectSuccess = true;

        [Header("Reconnection")]
        [SerializeField] private bool enableSocketIoReconnection = false;

        [Header("Background (Optional)")]
        [SerializeField] private bool keepConnectedInBackground = true;
        [SerializeField] private bool suppressDisconnectUiInBackground = true;

        private Coroutine internetMonitorRoutine;
        private Coroutine suspendedInternetReturnHideRoutine;
        private bool panelShownBySocket;
        private float lastAutoReconnectTime;
        private bool suppressNextDisconnectPanel;
        private bool suppressNextDisconnectLog;
        private bool isSuspended = true;

        public bool IsSuspended => isSuspended;

        public bool IsConnected =>
            socketManager != null &&
            socketManager.Socket != null &&
            socketManager.Socket.IsOpen;

        private static string NormalizeSocketUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            url = url.Trim();
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            if (!url.Contains("socket.io"))
            {
                url += "socket.io/";
            }

            return url;
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            showPanelWhenNoInternet = false;
            showPanelOnDisconnect = false;
            showPanelOnError = false;
            HideConnectionPanelIfVisible();

            if (autoListenEventNames != null && autoListenEventNames.Count == 0)
            {
                autoListenEventNames.Add("waiting");
                autoListenEventNames.Add("gameStart");
                autoListenEventNames.Add("cardOpen");
            }
        }

        private void Start()
        {
            if (autoStartOnStart)
            {
                StartGameSocket();
            }

            StartInternetMonitorIfNeeded();
        }

        private void OnEnable()
        {
            showPanelWhenNoInternet = false;
            showPanelOnDisconnect = false;
            showPanelOnError = false;
            HideConnectionPanelIfVisible();
            StartInternetMonitorIfNeeded();
        }

        private void OnDisable()
        {
            StopInternetMonitor();
        }

        private void OnApplicationPause(bool pause)
        {
            if (!Application.isPlaying) return;
            if (!keepConnectedInBackground) return;

            if (pause)
            {
                StopInternetMonitor();
                if (suppressDisconnectUiInBackground)
                {
                    suppressNextDisconnectPanel = true;
                    suppressNextDisconnectLog = true;
                    panelShownBySocket = false;
                }
                return;
            }

            StartInternetMonitorIfNeeded();

            if (autoReconnectWhenInternetReturns &&
                !isSuspended &&
                !IsConnected &&
                !isConnecting &&
                Application.internetReachability != NetworkReachability.NotReachable)
            {
                Connect();
            }
        }

        private void StartInternetMonitorIfNeeded()
        {
            if (!Application.isPlaying) return;
            if (isSuspended) return;
            if (!forceDisconnectWhenNoInternet) return;
            if (internetMonitorRoutine != null) return;

            internetMonitorRoutine = StartCoroutine(InternetMonitorLoop());
        }

        private void StopInternetMonitor()
        {
            if (internetMonitorRoutine == null) return;
            StopCoroutine(internetMonitorRoutine);
            internetMonitorRoutine = null;
        }

        private void StartSuspendedInternetReturnHideWatcherIfNeeded()
        {
            if (!Application.isPlaying) return;
            if (!isSuspended) return;
            if (suspendedInternetReturnHideRoutine != null) return;

            if (Application.internetReachability != NetworkReachability.NotReachable) return;
            if (connectionPanel == null)
            {
                connectionPanel = ResolveConnectionPanel();
            }
            if (connectionPanel == null || !connectionPanel.gameObject.activeSelf) return;

            suspendedInternetReturnHideRoutine = StartCoroutine(SuspendedWaitForInternetThenHidePanelLoop());
        }

        private void StopSuspendedInternetReturnHideWatcher()
        {
            if (suspendedInternetReturnHideRoutine == null) return;
            StopCoroutine(suspendedInternetReturnHideRoutine);
            suspendedInternetReturnHideRoutine = null;
        }

        private IEnumerator SuspendedWaitForInternetThenHidePanelLoop()
        {
            while (isSuspended && Application.internetReachability == NetworkReachability.NotReachable)
            {
                float interval = Mathf.Max(0.1f, internetCheckIntervalSeconds);
                yield return new WaitForSecondsRealtime(interval);
            }

            if (isSuspended && Application.internetReachability != NetworkReachability.NotReachable)
            {
                HideConnectionPanelIfVisible();
            }

            suspendedInternetReturnHideRoutine = null;
        }

        private IEnumerator InternetMonitorLoop()
        {
            float notReachableT = 0f;
            bool wasNotReachable = false;

            while (true)
            {
                float interval = Mathf.Max(0.1f, internetCheckIntervalSeconds);
                yield return new WaitForSecondsRealtime(interval);

                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    wasNotReachable = true;
                    notReachableT += interval;

                    if (notReachableT >= Mathf.Max(0f, noInternetGraceSeconds))
                    {
                        if (currentState != SocketState.Connected && currentState != SocketState.Connecting && currentState != SocketState.CheckingInternet)
                        {
                            notReachableT = 0f;
                            continue;
                        }

                        if (showPanelWhenNoInternet)
                        {
                            ShowConnectionPanelError(noInternetMessage);
                        }

                        DisconnectManually();
                        notReachableT = 0f;
                    }
                }
                else
                {
                    notReachableT = 0f;

                    if (wasNotReachable)
                    {
                        wasNotReachable = false;

                        HideConnectionPanelIfVisible();
                    }
                }
            }
        }

        // 🎮 CALL ONLY WHEN GAME STARTS
        public void StartGameSocket()
        {
            if (isSuspended)
            {
                return;
            }

            SetState(SocketState.CheckingInternet);
            hasReceivedGameStart = false;

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                LogRed("NO INTERNET - Waiting...");
                SetState(SocketState.None); // ✅ NOT Disconnected
                return;
            }

            Connect();
        }

        #region CONNECTION

        private void Connect()
        {
            if (isSuspended)
            {
                return;
            }

            if (isConnecting)
            {
                LogYellow("Socket connect already in progress");
                return;
            }

            if (IsConnected)
            {
                LogYellow("Socket already connected");
                return;
            }

            string connectUrl = NormalizeSocketUrl(socketUrl);
            if (string.IsNullOrEmpty(connectUrl))
            {
                LogRed("Socket URL is empty");
                SetState(SocketState.Error);
                return;
            }

            SetState(SocketState.Connecting);
            LogBlue("Connecting socket → " + connectUrl);
            isConnecting = true;

            if (socketManager != null)
            {
                try
                {
                    if (socketManager.Socket != null && socketManager.Socket.IsOpen)
                    {
                        socketManager.Socket.Disconnect();
                    }
                }
                catch { }
                socketManager = null;
            }

            UserSession.LoadFromPrefs();
            string jwtToken = UserSession.JwtToken;

            SocketOptions options = new SocketOptions
            {
                ConnectWith = BestHTTP.SocketIO3.Transports.TransportTypes.Polling,
                Reconnection = enableSocketIoReconnection,
                ReconnectionAttempts = int.MaxValue,
                ReconnectionDelay = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(10),
                AutoConnect = true
            };

            if (!string.IsNullOrEmpty(jwtToken))
            {
                string tokenPreview = jwtToken;
                if (tokenPreview.Length > 18)
                {
                    tokenPreview = jwtToken.Substring(0, 10) + "..." + jwtToken.Substring(jwtToken.Length - 6);
                }

                Debug.Log($"<color=#00e5ff><b>SOCKET AUTH</b></color> → token_loaded=true | len={jwtToken.Length} | preview={tokenPreview}");

                options.HTTPRequestCustomizationCallback = (manager, request) =>
                {
                    if (request == null) return;
                    request.SetHeader("token", jwtToken);
                    request.SetHeader("Authorization", "Bearer " + jwtToken);
                    Debug.Log($"<color=#7CFC00><b>SOCKET HEADER</b></color> → token=<jwt> (len={jwtToken.Length} | preview={tokenPreview})");
                };
            }

            socketManager = new SocketManager(new Uri(connectUrl), options);

            socketManager.Socket.On(SocketIOEventTypes.Connect, OnConnected);
            socketManager.Socket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
            socketManager.Socket.On<object>(SocketIOEventTypes.Error, OnError);

            socketManager.Open();
        }

        public void DisconnectManually()
        {
            if (socketManager != null)
            {
                socketManager.Socket.Disconnect();
                socketManager = null;
            }

            hasReceivedGameStart = false;

            if (wasSocketEverConnected)
                SetState(SocketState.Disconnected);
            else
                SetState(SocketState.None);
        }

        public void DisconnectManuallySilent()
        {
            suppressNextDisconnectPanel = true;
            suppressNextDisconnectLog = true;
            panelShownBySocket = false;
            DisconnectManually();
        }

        public void ResetToNoneSilent()
        {
            suppressNextDisconnectPanel = true;
            suppressNextDisconnectLog = true;
            panelShownBySocket = false;
            isConnecting = false;
            hasReceivedGameStart = false;
            activeAutoListeners.Clear();
            wasSocketEverConnected = false;

            if (socketManager != null)
            {
                try
                {
                    socketManager.Socket.Disconnect();
                }
                catch { }
                socketManager = null;
            }

            SetState(SocketState.None);
        }

        #endregion

        #region SOCKET CALLBACKS

        private void OnConnected()
        {
            wasSocketEverConnected = true;
            isConnecting = false;
            SetState(SocketState.Connected);
            LogGreen("SOCKET CONNECTED");

            EnsureAutoListeners();

            if (hidePanelOnReconnectSuccess && panelShownBySocket)
            {
                panelShownBySocket = false;
                if (connectionPanel != null)
                {
                    connectionPanel.Hide();
                }
            }
        }

        private void EnsureAutoListeners()
        {
            if (socketManager == null || socketManager.Socket == null) return;
            if (autoListenEventNames == null || autoListenEventNames.Count == 0) return;

            for (int i = 0; i < autoListenEventNames.Count; i++)
            {
                string eventName = autoListenEventNames[i];
                if (string.IsNullOrWhiteSpace(eventName)) continue;

                eventName = eventName.Trim();
                if (activeAutoListeners.Contains(eventName)) continue;
                activeAutoListeners.Add(eventName);

                Listen(eventName, _ => { });
            }
        }

        private void OnDisconnected()
        {
            isConnecting = false;
            activeAutoListeners.Clear();
            hasReceivedGameStart = false;

            bool suppressPanel = suppressNextDisconnectPanel;
            suppressNextDisconnectPanel = false;

            bool suppressLog = suppressNextDisconnectLog;
            suppressNextDisconnectLog = false;
            if (wasSocketEverConnected)
            {
                SetState(SocketState.Disconnected);
                if (!suppressLog)
                {
                    LogYellow("SOCKET DISCONNECTED");
                }

                if (showPanelOnDisconnect && !suppressPanel)
                {
                    ShowConnectionPanelError(disconnectedMessage);
                }
            }
            else
            {
                SetState(SocketState.None);
                if (!suppressLog)
                {
                    LogYellow("Socket closed before connection");
                }
            }
        }

        private void OnError(object error)
        {
            isConnecting = false;
            activeAutoListeners.Clear();
            hasReceivedGameStart = false;
            SetState(SocketState.Error);
            string managerInfo = socketManager != null
                ? $" | ManagerState={socketManager.State} | Url={socketManager.Uri}"
                : string.Empty;
            RecordSocketEvent("RECV", "error", error);
            LogRed("SOCKET ERROR → " + FormatSocketError(error) + managerInfo);

            if (showPanelOnError)
            {
                string msg = FormatSocketError(error);
                msg = string.IsNullOrWhiteSpace(msg) ? "Unknown error" : msg;
                ShowConnectionPanelError(errorMessagePrefix + msg);
            }
        }

        private void ShowConnectionPanelError(string message)
        {
            if (connectionPanel == null)
            {
                connectionPanel = ResolveConnectionPanel();
            }

            if (connectionPanel == null) return;

            panelShownBySocket = true;

            if (!connectionPanel.gameObject.activeSelf)
            {
                connectionPanel.gameObject.SetActive(true);
            }

            connectionPanel.ShowError(message);
        }

        private void ShowConnectionPanelLoader(string loadingBaseText)
        {
            if (connectionPanel == null)
            {
                connectionPanel = ResolveConnectionPanel();
            }

            if (connectionPanel == null) return;

            panelShownBySocket = true;

            if (!connectionPanel.gameObject.activeSelf)
            {
                connectionPanel.gameObject.SetActive(true);
            }

            connectionPanel.ShowLoader(loadingBaseText);
        }

        private void HideConnectionPanelIfVisible()
        {
            if (connectionPanel == null)
            {
                connectionPanel = ResolveConnectionPanel();
            }

            if (connectionPanel == null) return;
            if (!connectionPanel.gameObject.activeSelf) return;

            panelShownBySocket = false;
            connectionPanel.Hide();
        }

        private static GameLoaderPanelAnimator ResolveConnectionPanel()
        {
            GameLoaderPanelAnimator[] all = Resources.FindObjectsOfTypeAll<GameLoaderPanelAnimator>();
            if (all == null || all.Length == 0) return null;

            Scene activeScene = SceneManager.GetActiveScene();
            GameLoaderPanelAnimator fallback = null;

            for (int i = 0; i < all.Length; i++)
            {
                GameLoaderPanelAnimator anim = all[i];
                if (anim == null) continue;

                GameObject go = anim.gameObject;
                if (go == null) continue;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.hideFlags != HideFlags.None) continue;

                if (go.scene == activeScene)
                {
                    return anim;
                }

                if (fallback == null)
                {
                    fallback = anim;
                }
            }

            return fallback;
        }

        private static string FormatSocketError(object error)
        {
            if (error == null) return "<null>";

            if (error is Exception ex)
            {
                return ex.Message;
            }

            if (error is IDictionary<string, object> dict)
            {
                if (TryGetDictString(dict, "message", out string msg)) return msg;
                if (TryGetDictString(dict, "error", out string err)) return err;
                if (TryGetDictString(dict, "reason", out string reason)) return reason;
            }

            if (error is IDictionary rawDict)
            {
                object msg;
                if (rawDict.Contains("message"))
                {
                    msg = rawDict["message"];
                    if (msg != null) return msg.ToString();
                }
                if (rawDict.Contains("error"))
                {
                    msg = rawDict["error"];
                    if (msg != null) return msg.ToString();
                }
            }

            return error.ToString();
        }

        private static bool TryGetDictString(IDictionary<string, object> dict, string key, out string value)
        {
            value = string.Empty;
            if (dict == null || string.IsNullOrEmpty(key)) return false;
            if (!dict.TryGetValue(key, out object raw) || raw == null) return false;
            value = raw.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        #endregion

        #region SEND / LISTEN

        public void SendWithAck(string eventName, object payload = null)
        {
            if (isSuspended) return;
            if (!IsConnected)
            {
                LogRed("SEND FAILED (Not Connected) → " + eventName);
                return;
            }

            RecordSocketEvent("SEND", eventName, payload);

            socketManager.Socket
                .ExpectAcknowledgement<object>(ack =>
                {
                    RecordSocketEvent("ACK", eventName, ack);
                })
                .Emit(eventName, payload);
        }

        public void SendWithAck(string eventName, object payload, Action<object> onAck)
        {
            if (isSuspended) return;
            if (!IsConnected)
            {
                LogRed("SEND FAILED (Not Connected) → " + eventName);
                return;
            }

            RecordSocketEvent("SEND", eventName, payload);

            socketManager.Socket
                .ExpectAcknowledgement<object>(ack =>
                {
                    RecordSocketEvent("ACK", eventName, ack);
                    onAck?.Invoke(ack);
                })
                .Emit(eventName, payload);
        }

        public void Send(string eventName, object payload = null)
        {
            if (isSuspended) return;
            if (!IsConnected)
            {
                LogRed("SEND FAILED (Not Connected) → " + eventName);
                return;
            }

            socketManager.Socket.Emit(eventName, payload);
            RecordSocketEvent("SEND", eventName, payload);
            LogCyan("SEND → " + eventName);
        }

        public void Listen(string eventName, Action<object> callback)
        {
            if (isSuspended) return;
            if (socketManager == null)
            {
                LogRed("LISTEN FAILED (Socket not ready) → " + eventName);
                return;
            }

            socketManager.Socket.On<object>(eventName, data =>
            {
                RecordSocketEvent("RECV", eventName, data);
                if (string.Equals(eventName, "gameStart", StringComparison.OrdinalIgnoreCase))
                {
                    hasReceivedGameStart = true;
                }
                callback?.Invoke(data);
            });
            LogPurple("LISTEN → " + eventName);
        }

        public void ListenReplace(string eventName, Action<object> callback)
        {
            if (isSuspended) return;
            if (socketManager == null || socketManager.Socket == null)
            {
                LogRed("LISTEN FAILED (Socket not ready) → " + eventName);
                return;
            }

            try
            {
                socketManager.Socket.Off(eventName);
            }
            catch { }

            Listen(eventName, callback);
        }

        public void Unlisten(string eventName)
        {
            if (isSuspended) return;
            if (socketManager == null || socketManager.Socket == null) return;
            if (string.IsNullOrWhiteSpace(eventName)) return;

            try
            {
                socketManager.Socket.Off(eventName);
            }
            catch { }
        }

        private void RecordSocketEvent(string direction, string eventName, object data)
        {
            if (!logSocketEvents) return;

            string entry = $"{DateTime.Now:HH:mm:ss.fff} {direction} {eventName} {FormatForLog(data)}";
            recentSocketEvents.Add(entry);

            int cap = Mathf.Max(1, maxLoggedEvents);
            while (recentSocketEvents.Count > cap)
            {
                recentSocketEvents.RemoveAt(0);
            }

            string dirColor = string.Equals(direction, "RECV", StringComparison.OrdinalIgnoreCase) ? "#00ff00" : "#ffa500";
            Debug.Log($"<color={dirColor}><b>SOCKET {direction}</b></color> → <color=yellow>{eventName}</color> | {FormatForLog(data)}");
        }

        private static string FormatForLog(object obj)
        {
            return SerializeForLog(obj, 0);
        }

        private const int LogSerializeMaxDepth = 6;
        private const int LogSerializeMaxItems = 50;

        private static string SerializeForLog(object obj, int depth)
        {
            if (obj == null) return "null";
            if (depth > LogSerializeMaxDepth) return "<max_depth>";

            if (obj is string s) return "\"" + EscapeString(s) + "\"";
            if (obj is bool b) return b ? "true" : "false";
            if (obj is char c) return "\"" + EscapeString(c.ToString()) + "\"";
            if (obj is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            {
                return obj.ToString();
            }

            if (obj is IDictionary<string, object> dict)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                int count = 0;
                foreach (var kv in dict)
                {
                    if (count++ >= LogSerializeMaxItems)
                    {
                        sb.Append(first ? string.Empty : ", ");
                        sb.Append("\"...\": \"<truncated>\"");
                        break;
                    }

                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"");
                    sb.Append(EscapeString(kv.Key));
                    sb.Append("\": ");
                    sb.Append(SerializeForLog(kv.Value, depth + 1));
                }
                sb.Append("}");
                return sb.ToString();
            }

            if (obj is IDictionary rawDict)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                int count = 0;
                foreach (DictionaryEntry kv in rawDict)
                {
                    if (count++ >= LogSerializeMaxItems)
                    {
                        sb.Append(first ? string.Empty : ", ");
                        sb.Append("\"...\": \"<truncated>\"");
                        break;
                    }

                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"");
                    sb.Append(EscapeString(kv.Key != null ? kv.Key.ToString() : "<null>"));
                    sb.Append("\": ");
                    sb.Append(SerializeForLog(kv.Value, depth + 1));
                }
                sb.Append("}");
                return sb.ToString();
            }

            if (obj is IEnumerable enumerable && obj is not string)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("[");
                bool first = true;
                int count = 0;
                foreach (var item in enumerable)
                {
                    if (count++ >= LogSerializeMaxItems)
                    {
                        sb.Append(first ? string.Empty : ", ");
                        sb.Append("\"<truncated>\"");
                        break;
                    }

                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(SerializeForLog(item, depth + 1));
                }
                sb.Append("]");
                return sb.ToString();
            }

            return "\"" + EscapeString(obj.ToString()) + "\"";
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        #endregion

        #region STATE & LOGS

        private void SetState(SocketState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            Debug.Log($"<b><color=white>SOCKET STATE → {newState}</color></b>");
            OnStateChanged?.Invoke(newState);
        }

        public void SetSuspended(bool suspended)
        {
            if (isSuspended == suspended) return;
            isSuspended = suspended;

            if (isSuspended)
            {
                StopInternetMonitor();
                if (Application.internetReachability != NetworkReachability.NotReachable)
                {
                    HideConnectionPanelIfVisible();
                }
                else
                {
                    StartSuspendedInternetReturnHideWatcherIfNeeded();
                }
                ResetToNoneSilent();
            }
            else
            {
                StopSuspendedInternetReturnHideWatcher();
                StartInternetMonitorIfNeeded();
            }
        }

        private void LogGreen(string msg) =>
            Debug.Log($"<color=#00ff7f><b>{msg}</b></color>");

        private void LogRed(string msg) =>
            Debug.LogError($"<color=#ff4c4c><b>{msg}</b></color>");

        private void LogYellow(string msg) =>
            Debug.Log($"<color=#ffd700><b>{msg}</b></color>");

        private void LogBlue(string msg) =>
            Debug.Log($"<color=#4fc3f7><b>{msg}</b></color>");

        private void LogCyan(string msg) =>
            Debug.Log($"<color=#00ffff><b>{msg}</b></color>");

        private void LogPurple(string msg) =>
            Debug.Log($"<color=#d500f9><b>{msg}</b></color>");

        #endregion
    }
}
