using System;
using System.Collections;
using System.Collections.Generic;
using FancyScrollView.Example09;
using NewGame.Socket;
using UnityEngine;
using UnityEngine.UI;

public class LobbyListManager : MonoBehaviour
{
    public enum LobbyPlayerCount
    {
        TwoPlayers = 2,
        FourPlayers = 4,
    }

    [Serializable]
    public class LobbyEntry
    {
        public LobbyItemView view;

        public string lobbyId;
        public long winningCoin;
        public long winningDiamond;
        public long entryCoin;
        public bool isLocked;
    }

    [Header("Lobbies")]
    [SerializeField] private List<LobbyEntry> lobbies = new List<LobbyEntry>();

    [Header("Dynamic (Optional)")]
    [SerializeField] private bool populateFromGameWalletApi = false;
    [SerializeField] private LobbyPlayerCount populatePlayerCount = LobbyPlayerCount.TwoPlayers;
    [SerializeField] private LobbyItemView lobbyItemPrefab;
    [SerializeField] private Transform lobbyItemsRoot;

    [Header("Static (Optional)")]
    [SerializeField] private bool populateFromStaticConfigs = false;

    [Serializable]
    private class StaticLobbyConfig
    {
        public string lobbyId;
        public long winningCoin;
        public long winningDiamond;
        public long entryCoin;
        public bool isLocked;
    }

    [SerializeField] private List<StaticLobbyConfig> staticLobbyConfigs = new List<StaticLobbyConfig>();

    private bool initialPopulateFromGameWalletApi;
    private bool initialPopulateFromStaticConfigs;

    [Header("Open Board On Entry (Optional)")]
    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private GameObject gameplayScreenRoot;
    [SerializeField] private GameObject boardScreen2P;
    [SerializeField] private GameObject boardScreen4P;
    [SerializeField] private LobbyModeSelector modeSelector;

    [Header("Loader (Optional)")]
    [SerializeField] private GameObject loaderPanel;
    [SerializeField] private bool waitForSocketBeforeProceed = true;
    [SerializeField] private float socketWaitTimeoutSeconds = 10f;

    [Header("Player Finding (Optional)")]
    [SerializeField] private GameObject playerFindingScreen;
    [SerializeField] private bool openPlayerFindingWhenVsBot = true;
    [SerializeField] private GameManager gameManager;

    [Header("Friends (Optional)")]
    [SerializeField] private bool openFriendsScreenByIndex = true;
    [SerializeField] private int friendsScreenIndex = 3;

    [Header("Insufficient Balance (Optional)")]
    [SerializeField] private GameObject insufficientBalancePopup;
    [SerializeField] private Button insufficientBalanceStoreButton;
    [SerializeField] private PopupHandler popupHandler;
    [SerializeField] private bool openStoreByName = true;
    [SerializeField] private string storeScreenName = "ShopPanel";
    [SerializeField] private GameObject storeScreen;

    private readonly Dictionary<LobbyItemView, Action> clickHandlers = new Dictionary<LobbyItemView, Action>();
    private readonly List<LobbyItemView> spawnedViews = new List<LobbyItemView>();

    private bool isHandlingClick;

    public event Action<LobbyEntry> OnLobbyEntryClicked;

    public IReadOnlyList<LobbyEntry> Lobbies => lobbies;

    private void Awake()
    {
        initialPopulateFromGameWalletApi = populateFromGameWalletApi;
        initialPopulateFromStaticConfigs = populateFromStaticConfigs;
    }

    public void SetPopulateFromGameWalletApiOverride(bool? overrideValue)
    {
        populateFromGameWalletApi = overrideValue ?? initialPopulateFromGameWalletApi;
    }

    public void SetPopulateFromStaticConfigsOverride(bool? overrideValue)
    {
        populateFromStaticConfigs = overrideValue ?? initialPopulateFromStaticConfigs;
    }

    public void PopulateNow()
    {
        if (populateFromGameWalletApi)
        {
            PopulateFromLastApi();
        }
        else if (populateFromStaticConfigs)
        {
            PopulateFromStaticConfigsInternal();
        }

        RefreshAll();
        HookClicks();
    }

    private void OnEnable()
    {
        if (populateFromGameWalletApi)
        {
            PopulateFromLastApi();
        }
        else if (populateFromStaticConfigs)
        {
            PopulateFromStaticConfigsInternal();
        }
        RefreshAll();
        HookClicks();

        if (insufficientBalanceStoreButton != null)
        {
            insufficientBalanceStoreButton.onClick.RemoveListener(OpenStoreFromInsufficientBalancePopup);
            insufficientBalanceStoreButton.onClick.AddListener(OpenStoreFromInsufficientBalancePopup);
        }
    }

    private void OnDisable()
    {
        UnhookClicks();

        if (insufficientBalanceStoreButton != null)
        {
            insufficientBalanceStoreButton.onClick.RemoveListener(OpenStoreFromInsufficientBalancePopup);
        }

        ClearSpawnedViewsAndResetGenerated();
    }

    private void ClearSpawnedViewsAndResetGenerated()
    {
        if (spawnedViews == null || spawnedViews.Count == 0) return;

        for (int i = spawnedViews.Count - 1; i >= 0; i--)
        {
            if (spawnedViews[i] != null)
            {
                Destroy(spawnedViews[i].gameObject);
            }
        }

        spawnedViews.Clear();

        if (lobbies != null)
        {
            lobbies.Clear();
        }
    }

    private bool HasSufficientCoins(LobbyEntry entry, out long requiredEntryCoins, out int currentCoins)
    {
        requiredEntryCoins = 0;
        currentCoins = 0;
        if (entry == null) return true;

        requiredEntryCoins = Math.Max(0L, entry.entryCoin);
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
        if (offline)
        {
            int currentStars = PlayerWallet.Instance != null
                ? PlayerWallet.Instance.OfflineStars
                : PlayerWallet.GetOfflineStarsForCurrentUser();
            currentCoins = currentStars;
            return currentStars >= requiredEntryCoins;
        }

        UserSession.LoadFromPrefs();

        int walletCoins = -1;
        if (PlayerWallet.Instance != null)
        {
            walletCoins = Mathf.Max(0, PlayerWallet.Instance.Coins);
        }

        int sessionCoins = Mathf.Max(0, UserSession.Coins);
        if (walletCoins >= 0)
        {
            currentCoins = walletCoins;
            if (currentCoins <= 0 && sessionCoins > 0)
            {
                currentCoins = sessionCoins;
            }
        }
        else
        {
            currentCoins = sessionCoins;
        }

        return currentCoins >= requiredEntryCoins;
    }

    private void ShowInsufficientBalancePopup()
    {
        if (insufficientBalancePopup == null) return;

        if (popupHandler == null)
        {
            popupHandler = FindObjectOfType<PopupHandler>(true);
        }

        if (popupHandler != null)
        {
            popupHandler.OpenPopup(insufficientBalancePopup);
            return;
        }

        insufficientBalancePopup.SetActive(true);
    }

    private void CloseInsufficientBalancePopup()
    {
        if (insufficientBalancePopup == null) return;

        if (popupHandler == null)
        {
            popupHandler = FindObjectOfType<PopupHandler>(true);
        }

        if (popupHandler != null)
        {
            popupHandler.ClosePopup(insufficientBalancePopup);
            return;
        }

        insufficientBalancePopup.SetActive(false);
    }

    private void OpenStoreFromInsufficientBalancePopup()
    {
        CloseInsufficientBalancePopup();

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (screenManager == null) return;

        if (storeScreen != null)
        {
            screenManager.OpenScreen(storeScreen);
            return;
        }

        if (openStoreByName && !string.IsNullOrWhiteSpace(storeScreenName))
        {
            screenManager.OpenScreenByName(storeScreenName);
        }
    }

    public void RefreshAll()
    {
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
        for (int i = 0; i < lobbies.Count; i++)
        {
            LobbyEntry e = lobbies[i];
            if (e == null || e.view == null) continue;

            e.view.SetAll(
                null,
                null,
                e.winningCoin,
                e.winningDiamond,
                e.entryCoin,
                e.isLocked
            );

            e.view.SetOfflineCurrency(offline);

            e.view.SetLobbyId(e.lobbyId);
        }
    }

    public void PopulateFromLastApi()
    {
        if (!populateFromGameWalletApi) return;

        var games = populatePlayerCount == LobbyPlayerCount.FourPlayers
            ? GameWalletApi.LastFourPlayersGames
            : GameWalletApi.LastTwoPlayersGames;

        PopulateFromGames(games);
    }

    private void PopulateFromStaticConfigsInternal()
    {
        if (!populateFromStaticConfigs) return;
        if (lobbyItemPrefab == null || lobbyItemsRoot == null) return;
        if (staticLobbyConfigs == null) return;

        UnhookClicks();

        for (int i = spawnedViews.Count - 1; i >= 0; i--)
        {
            if (spawnedViews[i] != null)
            {
                Destroy(spawnedViews[i].gameObject);
            }
        }
        spawnedViews.Clear();
        lobbies.Clear();

        for (int i = 0; i < staticLobbyConfigs.Count; i++)
        {
            StaticLobbyConfig c = staticLobbyConfigs[i];
            if (c == null) continue;

            LobbyItemView view = Instantiate(lobbyItemPrefab, lobbyItemsRoot);
            spawnedViews.Add(view);

            var entry = new LobbyEntry
            {
                view = view,
                lobbyId = c.lobbyId,
                winningCoin = c.winningCoin,
                winningDiamond = c.winningDiamond,
                entryCoin = c.entryCoin,
                isLocked = c.isLocked
            };
            lobbies.Add(entry);
        }

        RefreshAll();
        HookClicks();

        LobbyCarousel carousel = GetComponent<LobbyCarousel>();
        if (carousel == null)
        {
            carousel = GetComponentInParent<LobbyCarousel>();
        }
        if (carousel != null)
        {
            carousel.Reinitialize(resetToFirst: true);
        }
    }

    private void PopulateFromGames(GameWalletApi.LobbyGame[] games)
    {
        if (lobbyItemPrefab == null || lobbyItemsRoot == null) return;
        if (games == null) return;

        UnhookClicks();

        for (int i = spawnedViews.Count - 1; i >= 0; i--)
        {
            if (spawnedViews[i] != null)
            {
                Destroy(spawnedViews[i].gameObject);
            }
        }
        spawnedViews.Clear();
        lobbies.Clear();

        for (int i = 0; i < games.Length; i++)
        {
            var g = games[i];
            if (g == null) continue;

            LobbyItemView view = Instantiate(lobbyItemPrefab, lobbyItemsRoot);
            spawnedViews.Add(view);

            var entry = new LobbyEntry
            {
                view = view,
                lobbyId = g._id,
                winningCoin = g.coinsWon,
                winningDiamond = g.diamondsWon,
                entryCoin = g.entryCoinsUsed,
                isLocked = g.isLock
            };
            lobbies.Add(entry);
        }

        RefreshAll();
        HookClicks();

        LobbyCarousel carousel = GetComponent<LobbyCarousel>();
        if (carousel == null)
        {
            carousel = GetComponentInParent<LobbyCarousel>();
        }
        if (carousel != null)
        {
            carousel.Reinitialize(resetToFirst: true);
        }
    }

    private void HookClicks()
    {
        UnhookClicks();
        for (int i = 0; i < lobbies.Count; i++)
        {
            LobbyEntry e = lobbies[i];
            if (e == null || e.view == null) continue;

            LobbyEntry captured = e;
            Action handler = () => HandleLobbyClicked(captured);
            clickHandlers[e.view] = handler;
            e.view.OnEntryClicked += handler;
        }
    }

    private void UnhookClicks()
    {
        foreach (var kvp in clickHandlers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.OnEntryClicked -= kvp.Value;
            }
        }
        clickHandlers.Clear();
    }

    private void HandleLobbyClicked(LobbyEntry entry)
    {
        if (entry == null) return;
        if (entry.isLocked) return;

        if (isHandlingClick) return;
        StartCoroutine(HandleLobbyClickedFlow(entry));
    }

    private IEnumerator HandleLobbyClickedFlow(LobbyEntry entry)
    {
        isHandlingClick = true;

        bool offlineNow = Application.internetReachability == NetworkReachability.NotReachable;
        if (!HasSufficientCoins(entry, out long required, out int current))
        {
            Debug.LogWarning($"Insufficient balance. required={required}, current={current}");
            if (offlineNow)
            {
                NoInternetStrip.BlockIfOffline("Not enough Stars to enter this lobby.");
            }
            else
            {
                ShowInsufficientBalancePopup();
            }
            isHandlingClick = false;
            yield break;
        }

        if (loaderPanel != null)
        {
            loaderPanel.SetActive(true);
            GameLoaderPanelAnimator anim = loaderPanel.GetComponent<GameLoaderPanelAnimator>();
            if (anim != null)
            {
                anim.ShowLoader();
            }
        }

        int selectedPlayers = 2;
        if (modeSelector != null && modeSelector.SelectedPlayerCount != 0)
        {
            selectedPlayers = modeSelector.SelectedPlayerCount;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager != null && gameManager.IsLocalOfflineFriendsMode)
        {
            // Offline expert mode: keep the same offline lobby flow (no socket), but start vs-bot expert match.
            if (gameManager.IsOfflineExpertMode)
            {
                if (gameManager != null)
                {
                    gameManager.SetSelectedLobbyRewards(entry.winningCoin, entry.winningDiamond);

                    long debitLong = Math.Max(0L, entry.entryCoin);
                    int debitAmount = debitLong > int.MaxValue ? int.MaxValue : (int)debitLong;
                    if (debitAmount > 0)
                    {
                        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
                        if (offline)
                        {
                            int currentStars = PlayerWallet.Instance != null
                                ? PlayerWallet.Instance.OfflineStars
                                : PlayerWallet.GetOfflineStarsForCurrentUser();
                            int nextStars = Mathf.Max(0, currentStars - debitAmount);
                            if (PlayerWallet.Instance != null) PlayerWallet.Instance.SetOfflineStars(nextStars);
                            else PlayerWallet.SetOfflineStarsForCurrentUser(nextStars);
                        }
                        else
                        {
                            bool debitDone = false;
                            string debitError = null;

                            GameWalletApi.DebitUpdateCoins(
                                debitAmount,
                                onSuccess: () => { debitDone = true; },
                                onError: err => { debitError = err; debitDone = true; }
                            );

                            while (!debitDone)
                            {
                                yield return null;
                            }

                            if (!string.IsNullOrWhiteSpace(debitError))
                            {
                                Debug.LogWarning($"Debit failed: {debitError}");
                                if (loaderPanel != null)
                                {
                                    GameLoaderPanelAnimator anim = loaderPanel.GetComponent<GameLoaderPanelAnimator>();
                                    if (anim != null) anim.ShowError(debitError);
                                    else loaderPanel.SetActive(false);
                                }
                                isHandlingClick = false;
                                yield break;
                            }
                        }
                    }

                    gameManager.StartOfflineVsBotExpertGame(selectedPlayers);
                }

                if (loaderPanel != null) loaderPanel.SetActive(false);

                GameObject expertBoardTarget = selectedPlayers == 4 ? boardScreen4P : boardScreen2P;
                if (expertBoardTarget != null)
                {
                    GameObject rootToOpen = gameplayScreenRoot != null
                        ? gameplayScreenRoot
                        : (expertBoardTarget.transform != null && expertBoardTarget.transform.parent != null
                            ? expertBoardTarget.transform.parent.gameObject
                            : expertBoardTarget);

                    if (screenManager != null) screenManager.OpenScreen(rootToOpen);
                    else rootToOpen.SetActive(true);

                    if (rootToOpen != expertBoardTarget) expertBoardTarget.SetActive(true);
                }

                isHandlingClick = false;
                yield break;
            }

            if (gameManager != null)
            {
                gameManager.SetSelectedLobbyRewards(entry.winningCoin, entry.winningDiamond);
                long debitLong = Math.Max(0L, entry.entryCoin);
                int debitAmount = debitLong > int.MaxValue ? int.MaxValue : (int)debitLong;

                if (debitAmount > 0)
                {
                    bool offline = Application.internetReachability == NetworkReachability.NotReachable;
                    if (offline)
                    {
                        int currentStars = PlayerWallet.Instance != null
                            ? PlayerWallet.Instance.OfflineStars
                            : PlayerWallet.GetOfflineStarsForCurrentUser();
                        int nextStars = Mathf.Max(0, currentStars - debitAmount);
                        if (PlayerWallet.Instance != null) PlayerWallet.Instance.SetOfflineStars(nextStars);
                        else PlayerWallet.SetOfflineStarsForCurrentUser(nextStars);
                    }
                    else
                    {
                        bool debitDone = false;
                        string debitError = null;

                        GameWalletApi.DebitUpdateCoins(
                            debitAmount,
                            onSuccess: () => { debitDone = true; },
                            onError: err => { debitError = err; debitDone = true; }
                        );

                        while (!debitDone)
                        {
                            yield return null;
                        }

                        if (!string.IsNullOrWhiteSpace(debitError))
                        {
                            Debug.LogWarning($"Debit failed: {debitError}");
                            if (loaderPanel != null)
                            {
                                GameLoaderPanelAnimator anim = loaderPanel.GetComponent<GameLoaderPanelAnimator>();
                                if (anim != null) anim.ShowError(debitError);
                                else loaderPanel.SetActive(false);
                            }
                            isHandlingClick = false;
                            yield break;
                        }
                    }
                }

                gameManager.StartLocalOfflineGame(selectedPlayers);
            }

            if (loaderPanel != null) loaderPanel.SetActive(false);

            GameObject boardTarget = selectedPlayers == 4 ? boardScreen4P : boardScreen2P;
            if (boardTarget != null)
            {
                GameObject rootToOpen = gameplayScreenRoot != null
                    ? gameplayScreenRoot
                    : (boardTarget.transform != null && boardTarget.transform.parent != null
                        ? boardTarget.transform.parent.gameObject
                        : boardTarget);

                if (screenManager != null) screenManager.OpenScreen(rootToOpen);
                else rootToOpen.SetActive(true);

                if (rootToOpen != boardTarget) boardTarget.SetActive(true);
            }

            isHandlingClick = false;
            yield break;
        }

        SocketConnection socket = SocketConnection.Instance != null
            ? SocketConnection.Instance
            : FindObjectOfType<SocketConnection>();

        if (socket != null)
        {
            if (socket.IsSuspended)
            {
                socket.SetSuspended(false);
            }

            if (socket.CurrentState != SocketState.Connected)
            {
                socket.StartGameSocket();
            }

            float timeout = Mathf.Max(0.1f, socketWaitTimeoutSeconds);
            float t = 0f;
            while (t < timeout)
            {
                if (socket.CurrentState == SocketState.Connected) break;
                if (socket.CurrentState == SocketState.Error) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (socket == null || socket.CurrentState != SocketState.Connected)
        {
            Debug.LogWarning($"Socket not connected, aborting lobby navigation | Socket State = {(socket != null ? socket.CurrentState.ToString() : "<null>")}");
            if (loaderPanel != null)
            {
                GameLoaderPanelAnimator anim = loaderPanel.GetComponent<GameLoaderPanelAnimator>();
                if (anim != null)
                {
                    anim.ShowError("Connection failed. Please check internet and try again.");
                }
                else
                {
                    loaderPanel.SetActive(false);
                }
            }
            isHandlingClick = false;
            yield break;
        }

        // Online lobby entry fee should be debited via API before joining game.
        long onlineDebitLong = Math.Max(0L, entry.entryCoin);
        int onlineDebitAmount = onlineDebitLong > int.MaxValue ? int.MaxValue : (int)onlineDebitLong;
        if (onlineDebitAmount > 0)
        {
            bool debitDone = false;
            string debitError = null;

            GameWalletApi.DebitUpdateCoins(
                onlineDebitAmount,
                onSuccess: () => { debitDone = true; },
                onError: err => { debitError = err; debitDone = true; }
            );

            while (!debitDone)
            {
                yield return null;
            }

            if (!string.IsNullOrWhiteSpace(debitError))
            {
                Debug.LogWarning($"Debit failed: {debitError}");
                if (loaderPanel != null)
                {
                    GameLoaderPanelAnimator anim = loaderPanel.GetComponent<GameLoaderPanelAnimator>();
                    if (anim != null) anim.ShowError(debitError);
                    else loaderPanel.SetActive(false);
                }
                isHandlingClick = false;
                yield break;
            }
        }

        if (gameManager != null)
        {
            gameManager.SetSelectedLobbyRewards(entry.winningCoin, entry.winningDiamond);
        }

        string joinUserId = string.Empty;
        string joinLobbyId = entry != null ? entry.lobbyId : string.Empty;

        bool receivedWaiting = false;
        bool receivedGameStart = false;

        void OpenPlayerFinding()
        {
            if (playerFindingScreen == null) return;

            if (screenManager != null)
            {
                screenManager.OpenScreen(playerFindingScreen);
            }
            else
            {
                playerFindingScreen.SetActive(true);
            }

            if (gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }
            if (gameManager != null)
            {
                gameManager.OnPlayerFindingScreenOpened();
            }
        }

        void OpenGameplay(object startPayload)
        {
            GameObject target = selectedPlayers == 4 ? boardScreen4P : boardScreen2P;
            if (target == null) return;

            GameObject rootToOpen = gameplayScreenRoot != null
                ? gameplayScreenRoot
                : (target.transform != null && target.transform.parent != null
                    ? target.transform.parent.gameObject
                    : target);

            if (screenManager != null) screenManager.OpenScreen(rootToOpen);
            else rootToOpen.SetActive(true);

            if (rootToOpen != target) target.SetActive(true);

            if (gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }
            if (gameManager != null)
            {
                gameManager.ApplySocketGameStartData(startPayload);
                if (screenManager == null)
                {
                    gameManager.OnGameplayScreenOpened();
                }
            }
        }

        if (socket != null)
        {
            if (socket.IsSuspended)
            {
                socket.SetSuspended(false);
            }

            socket.ListenReplace("waiting", _ =>
            {
                if (receivedWaiting) return;
                receivedWaiting = true;
                if (loaderPanel != null) loaderPanel.SetActive(false);
                OpenPlayerFinding();
                isHandlingClick = false;
            });

            socket.ListenReplace("gameStart", payload =>
            {
                if (receivedGameStart) return;
                receivedGameStart = true;
                if (loaderPanel != null) loaderPanel.SetActive(false);
                OpenGameplay(payload);
                isHandlingClick = false;
            });

            if (socket.CurrentState == SocketState.Connected)
            {
                UserSession.LoadFromPrefs();
                joinUserId = UserSession.UserId;

                if (string.IsNullOrEmpty(joinUserId))
                {
                    Debug.LogWarning("joinGame not sent | user_id is empty");
                }
                else if (string.IsNullOrEmpty(joinLobbyId))
                {
                    Debug.LogWarning("joinGame not sent | gamelobby_id is empty");
                }
                else
                {
                    SocketEventSender.SendJoinGame(joinUserId, selectedPlayers, joinLobbyId);
                }
            }
        }

        float waitTimeout = Mathf.Max(0.1f, socketWaitTimeoutSeconds);
        float waitT = 0f;
        while (waitT < waitTimeout)
        {
            if (receivedWaiting) break;
            if (receivedGameStart) break;
            waitT += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!receivedWaiting && !receivedGameStart)
        {
            Debug.LogWarning("No waiting/gameStart received within timeout, aborting navigation");
            if (socket != null)
            {
                socket.Unlisten("waiting");
                socket.Unlisten("gameStart");
            }
            if (loaderPanel != null)
            {
                GameLoaderPanelAnimator anim = loaderPanel.GetComponent<GameLoaderPanelAnimator>();
                if (anim != null)
                {
                    anim.ShowError("Server not responding. Please try again.");
                }
                else
                {
                    loaderPanel.SetActive(false);
                }
            }
            isHandlingClick = false;
            yield break;
        }

        if (socket != null)
        {
            socket.Unlisten("waiting");
            socket.Unlisten("gameStart");
        }

        yield break;
    }

    private bool IsVsBotMode()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        return gameManager != null && gameManager.IsVsBotMode;
    }
}
