using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FancyScrollView.Example09;
using System.Collections.Generic;
using My.UI;
using TMPro;
using NewGame.Socket;

public class PlayerFindingPanelAnimator : MonoBehaviour
{
    private struct RectSnapshot
    {
        public Transform parent;
        public int siblingIndex;
        public Vector3 anchoredPosition3D;
        public Vector3 localScale;
        public Quaternion localRotation;
    }

    private RectSnapshot player1Profile2PSnapshot;
    private RectSnapshot player2Profile2PSnapshot;
    private RectSnapshot player1Profile4PSnapshot;
    private RectSnapshot player2Profile4PSnapshot;
    private RectSnapshot player3Profile4PSnapshot;
    private RectSnapshot player4Profile4PSnapshot;

    private bool hasPlayer1Profile2PSnapshot;
    private bool hasPlayer2Profile2PSnapshot;
    private bool hasPlayer1Profile4PSnapshot;
    private bool hasPlayer2Profile4PSnapshot;
    private bool hasPlayer3Profile4PSnapshot;
    private bool hasPlayer4Profile4PSnapshot;

    [Header("Layouts")]
    [SerializeField] private GameObject twoPlayerVsRoot;
    [SerializeField] private GameObject fourPlayerVsRoot;

    [Header("2P Background Panels")]
    [SerializeField] private RectTransform bluePanel2P;
    [SerializeField] private RectTransform redPanel2P;

    [Header("4P Background Panels (Optional)")]
    [SerializeField] private RectTransform redPanel4P;
    [SerializeField] private RectTransform greenPanel4P;
    [SerializeField] private RectTransform yellowPanel4P;
    [SerializeField] private RectTransform bluePanel4P;

    [Header("4P Panel Hidden Positions (Anchored)")]
    [SerializeField] private Vector2 redPanel4PHiddenPosition = new Vector2(-1200f, 0f);
    [SerializeField] private Vector2 greenPanel4PHiddenPosition = new Vector2(1200f, 0f);
    [SerializeField] private Vector2 yellowPanel4PHiddenPosition = new Vector2(-1200f, 0f);
    [SerializeField] private Vector2 bluePanel4PHiddenPosition = new Vector2(1200f, 0f);

    [Header("2P Profiles")]
    [SerializeField] private RectTransform player1Profile2P;
    [SerializeField] private RectTransform player2Profile2P;

    [Header("4P Profiles")]
    [SerializeField] private RectTransform player1Profile4P;
    [SerializeField] private RectTransform player2Profile4P;
    [SerializeField] private RectTransform player3Profile4P;
    [SerializeField] private RectTransform player4Profile4P;

    [Header("4P Profile Pop")]
    [SerializeField] private bool use4PProfileFinalScaleOverride = true;
    [SerializeField] private float profileFinalScale4P = 0.8f;
    [SerializeField] private GameObject[] particlesToEnableOn4PProfilePop;

    [Header("2P Profile Pop")]
    [SerializeField] private GameObject[] particlesToEnableOn2PProfilePop;

    [Header("2P VS Particles")]
    [SerializeField] private GameObject[] particlesToEnableAfter2PVsEntrance;

    [Header("Spinner (Optional)")]
    [SerializeField] private PlayerFindingProfileSpinner player2Spinner2P;

    [Header("4P Spinners (Optional)")]
    [SerializeField] private PlayerFindingProfileSpinner player2Spinner4P;
    [SerializeField] private PlayerFindingProfileSpinner player3Spinner4P;
    [SerializeField] private PlayerFindingProfileSpinner player4Spinner4P;

    [Header("Search Icon (Optional)")]
    [SerializeField] private RectTransform searchIcon;
    [SerializeField] private RectTransform searchIconPlayer2_4P;
    [SerializeField] private RectTransform searchIconPlayer3_4P;
    [SerializeField] private RectTransform searchIconPlayer4_4P;
    [SerializeField] private float searchOrbitDuration = 1.2f;
    [SerializeField] private float searchOrbitRadius = 10f;
    [SerializeField] private bool searchOrbitClockwise = true;

    [Header("VS Images (Optional)")]
    [SerializeField] private RectTransform vsBig;
    [SerializeField] private RectTransform vsSmall;
    [SerializeField] private RectTransform vsBig4P;
    [SerializeField] private RectTransform vsSmall4P;

    [Header("VS Positions (Anchored)")]
    [SerializeField] private Vector2 vsBigHiddenPosition = new Vector2(-900f, 0f);
    [SerializeField] private Vector2 vsSmallHiddenPosition = new Vector2(900f, 0f);

    [Header("VS Timing")]
    [SerializeField] private float vsMoveDuration = 0.35f;
    [SerializeField] private float vsScaleDuration = 0.25f;
    [SerializeField] private Ease vsEase = Ease.OutBack;

    [Header("User Names (Roots)")]
    [SerializeField] private GameObject player1UserNameRoot2P;
    [SerializeField] private GameObject player2UserNameRoot2P;

    [Header("4P User Names (Roots)")]
    [SerializeField] private GameObject player1UserNameRoot4P;
    [SerializeField] private GameObject player2UserNameRoot4P;
    [SerializeField] private GameObject player3UserNameRoot4P;
    [SerializeField] private GameObject player4UserNameRoot4P;

    [Header("Finish / Transition")]
    [SerializeField] private Image panelBackgroundImage;
    [SerializeField] private GameObject playerFindingScreenRoot;
    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private GameObject returnHomeScreen;
    [SerializeField] private string returnHomeScreenName = "LobbyPanel";
    [SerializeField] private GameLoaderPanelAnimator gameLoaderPanel;
    [SerializeField] private float disconnectErrorHoldSeconds = 1.5f;
    [SerializeField] private string disconnectErrorMessage = "Connection lost. Returning to home...";
    [SerializeField] private string returningHomeBaseText = "Returning";
    [SerializeField] private GameObject gameplayScreen;
    [SerializeField] private GameObject gameplayScreenVsBot;
    [SerializeField] private GameObject gameplayScreenVsPlayer;
    [SerializeField] private bool openGameplayViaScreenManagerAfterTransition = true;
    [SerializeField] private bool requireGameStartEventToOpenGameplay = true;
    [SerializeField] private bool openGameplayImmediateForPlayWithOops = false;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Image gameplayBackgroundImage;
    [SerializeField] private Color gameplayBackgroundHoldColor = new Color32(0x6F, 0x69, 0x69, 0xFF);
    [SerializeField] private Color gameplayBackgroundFinalColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    [SerializeField] private float gameplayBackgroundFadeToWhiteDuration = 0.45f;
    [HideInInspector, SerializeField] private RectTransform gameplayPlayer1ProfileTarget;
    [HideInInspector, SerializeField] private RectTransform gameplayPlayer2ProfileTarget;
    [HideInInspector, SerializeField] private RectTransform gameplayPlayer3ProfileTarget;
    [HideInInspector, SerializeField] private RectTransform gameplayPlayer4ProfileTarget;
    [SerializeField] private RectTransform gameplayPlayer1ProfileTarget2P;
    [SerializeField] private RectTransform gameplayPlayer2ProfileTarget2P;
    [SerializeField] private RectTransform gameplayPlayer1ProfileTarget4P;
    [SerializeField] private RectTransform gameplayPlayer2ProfileTarget4P;
    [SerializeField] private RectTransform gameplayPlayer3ProfileTarget4P;
    [SerializeField] private RectTransform gameplayPlayer4ProfileTarget4P;
    [SerializeField] private float profileMoveDelaySeconds = 0.2f;
    [SerializeField] private float profileMoveToGameplayDuration = 0.45f;
    [SerializeField] private float profileMoveCurveHeight = 220f;
    [SerializeField] private Ease profileMoveToGameplayEase = Ease.OutCubic;
    [SerializeField] private CardDeckAnimator cardDeckAnimator;

    [Header("4P Spinner Stop Timing")]
    [SerializeField] private float fourPStopFirstDelaySeconds = 0.6f;
    [SerializeField] private float fourPStopBetweenDelaySeconds = 0.45f;

    [Header("Particles (keep off during open)")]
    [SerializeField] private GameObject[] particlesToDisable;

    [Header("Positions (Anchored)")]
    [SerializeField] private Vector2 bluePanelHiddenPosition = new Vector2(0f, -900f);
    [SerializeField] private Vector2 bluePanelShownPosition = Vector2.zero;
    [SerializeField] private Vector2 redPanelHiddenPosition = new Vector2(0f, 900f);
    [SerializeField] private Vector2 redPanelShownPosition = Vector2.zero;
    [SerializeField] private Vector2 centerPosition = Vector2.zero;
    [SerializeField] private Vector2 player1LeftPosition = new Vector2(-260f, 0f);
    [SerializeField] private Vector2 player2RightPosition = new Vector2(260f, 0f);

    [Header("Timing")]
    [SerializeField] private float panelMoveDuration = 0.45f;
    [SerializeField] private float profilePopDuration = 0.25f;
    [SerializeField] private float splitDuration = 0.75f;
    [SerializeField] private Ease moveEase = Ease.OutQuad;
    [SerializeField] private Ease profilePopEase = Ease.OutBack;
    [SerializeField] private float delayBeforeSplit = 0.35f;

    private Vector2 player1StartPos;
    private Vector2 player2StartPos;

    private Sequence seq;
    private Tween searchOrbitTween;
    private Vector2 searchIconBaseAnchoredPos;

    private Tween searchOrbitTween4P_P2;
    private Tween searchOrbitTween4P_P3;
    private Tween searchOrbitTween4P_P4;
    private Vector2 searchIconBaseAnchoredPos4P_P2;
    private Vector2 searchIconBaseAnchoredPos4P_P3;
    private Vector2 searchIconBaseAnchoredPos4P_P4;

    private Vector2 vsBigShownPosition;
    private Vector2 vsSmallShownPosition;
    private Vector2 vsBigShownPosition4P;
    private Vector2 vsSmallShownPosition4P;

    private Vector2 redPanel4PShownPosition;
    private Vector2 greenPanel4PShownPosition;
    private Vector2 yellowPanel4PShownPosition;
    private Vector2 bluePanel4PShownPosition;

    private Vector3 player1Profile2PBaseScale = Vector3.one;
    private Vector3 player2Profile2PBaseScale = Vector3.one;

    private Vector3 player1Profile4PBaseScale = Vector3.one;
    private Vector3 player2Profile4PBaseScale = Vector3.one;
    private Vector3 player3Profile4PBaseScale = Vector3.one;
    private Vector3 player4Profile4PBaseScale = Vector3.one;

    private bool is4PActive = false;
    private int remaining4PSpinners = 0;

    private readonly HashSet<Sprite> used4PProfileSprites = new HashSet<Sprite>();

    private Coroutine fourPStopCoroutine;

    private bool pendingReturnHomeAfterReconnect;
    private bool isReturningHome;
    private Coroutine returnHomeRoutine;

    private bool hasAppliedGameStartState;

    private void CaptureSnapshot(RectTransform rt, ref RectSnapshot snapshot, ref bool hasSnapshot)
    {
        if (rt == null)
        {
            hasSnapshot = false;
            return;
        }

        snapshot.parent = rt.parent;
        snapshot.siblingIndex = rt.GetSiblingIndex();
        snapshot.anchoredPosition3D = rt.anchoredPosition3D;
        snapshot.localScale = rt.localScale;
        snapshot.localRotation = rt.localRotation;
        hasSnapshot = true;
    }

    private void RestoreSnapshot(RectTransform rt, RectSnapshot snapshot, bool hasSnapshot)
    {
        if (!hasSnapshot || rt == null) return;

        rt.SetParent(snapshot.parent, worldPositionStays: false);
        rt.SetSiblingIndex(snapshot.siblingIndex);
        rt.anchoredPosition3D = snapshot.anchoredPosition3D;
        rt.localScale = snapshot.localScale;
        rt.localRotation = snapshot.localRotation;
    }

    private void RestoreProfilesToDefaults()
    {
        RestoreSnapshot(player1Profile2P, player1Profile2PSnapshot, hasPlayer1Profile2PSnapshot);
        RestoreSnapshot(player2Profile2P, player2Profile2PSnapshot, hasPlayer2Profile2PSnapshot);

        RestoreSnapshot(player1Profile4P, player1Profile4PSnapshot, hasPlayer1Profile4PSnapshot);
        RestoreSnapshot(player2Profile4P, player2Profile4PSnapshot, hasPlayer2Profile4PSnapshot);
        RestoreSnapshot(player3Profile4P, player3Profile4PSnapshot, hasPlayer3Profile4PSnapshot);
        RestoreSnapshot(player4Profile4P, player4Profile4PSnapshot, hasPlayer4Profile4PSnapshot);
    }

    private void Awake()
    {
        if (player1Profile2P != null) player1StartPos = player1Profile2P.anchoredPosition;
        if (player2Profile2P != null) player2StartPos = player2Profile2P.anchoredPosition;

        if (player1Profile2P != null) player1Profile2PBaseScale = player1Profile2P.localScale;
        if (player2Profile2P != null) player2Profile2PBaseScale = player2Profile2P.localScale;

        if (searchIcon != null)
        {
            searchIconBaseAnchoredPos = searchIcon.anchoredPosition;
        }

        if (searchIconPlayer2_4P != null) searchIconBaseAnchoredPos4P_P2 = searchIconPlayer2_4P.anchoredPosition;
        if (searchIconPlayer3_4P != null) searchIconBaseAnchoredPos4P_P3 = searchIconPlayer3_4P.anchoredPosition;
        if (searchIconPlayer4_4P != null) searchIconBaseAnchoredPos4P_P4 = searchIconPlayer4_4P.anchoredPosition;

        if (vsBig != null) vsBigShownPosition = vsBig.anchoredPosition;
        if (vsSmall != null) vsSmallShownPosition = vsSmall.anchoredPosition;
        if (vsBig4P != null) vsBigShownPosition4P = vsBig4P.anchoredPosition;
        if (vsSmall4P != null) vsSmallShownPosition4P = vsSmall4P.anchoredPosition;

        if (redPanel4P != null) redPanel4PShownPosition = redPanel4P.anchoredPosition;
        if (greenPanel4P != null) greenPanel4PShownPosition = greenPanel4P.anchoredPosition;
        if (yellowPanel4P != null) yellowPanel4PShownPosition = yellowPanel4P.anchoredPosition;
        if (bluePanel4P != null) bluePanel4PShownPosition = bluePanel4P.anchoredPosition;

        if (player1Profile4P != null) player1Profile4PBaseScale = player1Profile4P.localScale;
        if (player2Profile4P != null) player2Profile4PBaseScale = player2Profile4P.localScale;
        if (player3Profile4P != null) player3Profile4PBaseScale = player3Profile4P.localScale;
        if (player4Profile4P != null) player4Profile4PBaseScale = player4Profile4P.localScale;

        CaptureSnapshot(player1Profile2P, ref player1Profile2PSnapshot, ref hasPlayer1Profile2PSnapshot);
        CaptureSnapshot(player2Profile2P, ref player2Profile2PSnapshot, ref hasPlayer2Profile2PSnapshot);
        CaptureSnapshot(player1Profile4P, ref player1Profile4PSnapshot, ref hasPlayer1Profile4PSnapshot);
        CaptureSnapshot(player2Profile4P, ref player2Profile4PSnapshot, ref hasPlayer2Profile4PSnapshot);
        CaptureSnapshot(player3Profile4P, ref player3Profile4PSnapshot, ref hasPlayer3Profile4PSnapshot);
        CaptureSnapshot(player4Profile4P, ref player4Profile4PSnapshot, ref hasPlayer4Profile4PSnapshot);
    }

    private void ApplyLocalProfileToUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        string name = gm.PlayerName;
        if (!string.IsNullOrWhiteSpace(name))
        {
            ApplyNameToRoot(player1UserNameRoot2P, name);
            ApplyNameToRoot(player1UserNameRoot4P, name);
        }

        Sprite avatar = gm.PlayerAvatarSprite;
        if (avatar != null)
        {
            ApplyAvatarToProfile(player1Profile2P != null ? player1Profile2P.gameObject : null, avatar);
            ApplyAvatarToProfile(player1Profile4P != null ? player1Profile4P.gameObject : null, avatar);
        }
    }

    private void ApplyNameToRoot(GameObject root, string value)
    {
        if (root == null) return;

        TMP_Text txt = root.GetComponentInChildren<TMP_Text>(true);
        if (txt == null) return;

        txt.text = value;
    }

    private void ApplyAvatarToProfile(GameObject profileRoot, Sprite sprite)
    {
        if (profileRoot == null) return;
        if (sprite == null) return;

        Image img = FindBestAvatarImage(profileRoot);
        if (img == null) return;

        img.sprite = sprite;
    }

    private Image FindBestAvatarImage(GameObject profileRoot)
    {
        if (profileRoot == null) return null;

        Image[] images = profileRoot.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0) return null;

        Image best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null) continue;

            string n = img.gameObject.name;
            string ln = !string.IsNullOrEmpty(n) ? n.ToLowerInvariant() : string.Empty;

            int score = 0;

            if (ln.Contains("name") || ln.Contains("username") || ln.Contains("user") || ln.Contains("box") || ln.Contains("text"))
            {
                score -= 1000;
            }

            if (ln.Contains("ring") || ln.Contains("frame") || ln.Contains("border") || ln.Contains("mask"))
            {
                score -= 200;
            }

            if (ln.Contains("avatar") || ln.Contains("icon") || ln.Contains("profile"))
            {
                score += 200;
            }

            bool underAvatarBorder = false;
            Transform t = img.transform;
            while (t != null)
            {
                string tn = t.name;
                string ltn = !string.IsNullOrEmpty(tn) ? tn.ToLowerInvariant() : string.Empty;
                if (ltn.Contains("avatarborder"))
                {
                    underAvatarBorder = true;
                    break;
                }
                t = t.parent;
            }

            if (underAvatarBorder) score += 500;
            if (img.sprite == null) score += 10;
            if (img.raycastTarget) score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                best = img;
            }
        }

        return best;
    }

    private void OnEnable()
    {
        hasAppliedGameStartState = false;

        RestoreProfilesToDefaults();
        HookSpinner();

        if (player2Spinner2P != null) player2Spinner2P.SetLoopUntilStopped(true);
        if (player2Spinner4P != null) player2Spinner4P.SetLoopUntilStopped(true);
        if (player3Spinner4P != null) player3Spinner4P.SetLoopUntilStopped(true);
        if (player4Spinner4P != null) player4Spinner4P.SetLoopUntilStopped(true);

        if (SocketConnection.Instance != null)
        {
            SocketConnection.Instance.ListenReplace("gameStart", data => OnGameStartReceived(data));
            SocketConnection.Instance.OnStateChanged -= OnSocketStateChanged;
            SocketConnection.Instance.OnStateChanged += OnSocketStateChanged;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
        if (gameManager != null)
        {
            gameManager.SetGameplaySettingsButtonExternalLock(true);
            gameManager.ApplyDefaultPieceSpritesNow();
        }

        ApplyLocalProfileToUI();
        PlayOpen();
    }

    private void OnDisable()
    {
        UnhookSpinner();
        Kill();

        if (SocketConnection.Instance != null)
        {
            SocketConnection.Instance.Unlisten("gameStart");
            SocketConnection.Instance.OnStateChanged -= OnSocketStateChanged;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
        if (gameManager != null)
        {
            gameManager.SetGameplaySettingsButtonExternalLock(false);
        }
    }

    private void OnSocketStateChanged(SocketState state)
    {
        if (isReturningHome) return;

        var socket = SocketConnection.Instance;
        if (socket == null) return;

        if (requireGameStartEventToOpenGameplay && !socket.HasReceivedGameStart)
        {
            if (state == SocketState.Disconnected || state == SocketState.Error)
            {
                if (returnHomeRoutine == null)
                {
                    returnHomeRoutine = StartCoroutine(ReturnHomeAfterDisconnectRoutine());
                }
            }
        }
    }

    private IEnumerator ReturnHomeAfterDisconnectRoutine()
    {
        if (isReturningHome) yield break;
        isReturningHome = true;
        pendingReturnHomeAfterReconnect = false;

        var socket = SocketConnection.Instance;
        if (socket != null)
        {
            socket.OnStateChanged -= OnSocketStateChanged;
            socket.Unlisten("gameStart");
        }

        if (gameLoaderPanel == null)
        {
            gameLoaderPanel = FindObjectOfType<GameLoaderPanelAnimator>(true);
        }

        if (gameLoaderPanel != null)
        {
            gameLoaderPanel.gameObject.SetActive(true);
            gameLoaderPanel.ShowError(disconnectErrorMessage);

            float hold = Mathf.Max(0f, disconnectErrorHoldSeconds);
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            gameLoaderPanel.ShowLoader(returningHomeBaseText);
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (screenManager != null)
        {
            if (returnHomeScreen != null)
            {
                screenManager.OpenScreen(returnHomeScreen);
            }
            else if (!string.IsNullOrWhiteSpace(returnHomeScreenName))
            {
                screenManager.OpenScreenByName(returnHomeScreenName);
            }
        }

        if (playerFindingScreenRoot != null)
        {
            playerFindingScreenRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        if (socket != null)
        {
            socket.ResetToNoneSilent();
        }

        if (gameLoaderPanel != null)
        {
            gameLoaderPanel.Hide();
        }

        returnHomeRoutine = null;
    }

    private void OnGameStartReceived(object data)
    {
        if (!hasAppliedGameStartState)
        {
            hasAppliedGameStartState = true;
            if (gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }
            if (gameManager != null)
            {
                gameManager.ApplySocketGameStartData(data);
            }
        }

        // PlayWithOops requires a simple flow: waiting -> PlayerFinding, gameStart -> Gameplay.
        // Do not block on spinner/return animations.
        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            if (openGameplayImmediateForPlayWithOops)
            {
                OpenGameplayImmediateForPlayWithOops();
                return;
            }
        }

        if (is4PActive)
        {
            if (fourPStopCoroutine == null)
            {
                fourPStopCoroutine = StartCoroutine(Stop4PSpinnersSequentialRoutine());
            }
            return;
        }

        if (player2Spinner2P != null)
        {
            player2Spinner2P.SetRandomSpriteImmediate();
            player2Spinner2P.StopAndComplete();
        }
    }

    private void OpenGameplayImmediateForPlayWithOops()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        GameObject targetGameplayScreen = gameplayScreen;
        if (gameManager != null)
        {
            if (gameManager.IsVsBotMode && gameplayScreenVsBot != null)
            {
                targetGameplayScreen = gameplayScreenVsBot;
            }
            else if (!gameManager.IsVsBotMode && gameplayScreenVsPlayer != null)
            {
                targetGameplayScreen = gameplayScreenVsPlayer;
            }
        }

        if (targetGameplayScreen != null)
        {
            if (screenManager == null)
            {
                screenManager = FindObjectOfType<ScreenManager>();
            }

            if (screenManager != null)
            {
                screenManager.OpenScreen(targetGameplayScreen);
            }
            else
            {
                targetGameplayScreen.SetActive(true);
                if (gameManager != null)
                {
                    gameManager.OnGameplayScreenOpened();
                }
            }
        }

        if (playerFindingScreenRoot != null)
        {
            playerFindingScreenRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        Kill();
    }

    private void HookSpinner()
    {
        if (player2Spinner2P == null) return;
        player2Spinner2P.SpinStateChanged -= OnSpinnerStateChanged;
        player2Spinner2P.SpinStateChanged += OnSpinnerStateChanged;

        player2Spinner2P.SpinCompleted -= OnSpinnerCompleted;
        player2Spinner2P.SpinCompleted += OnSpinnerCompleted;

        if (player2Spinner4P != null)
        {
            player2Spinner4P.SpinCompleted -= OnPlayer2Spinner4PCompleted;
            player2Spinner4P.SpinCompleted += OnPlayer2Spinner4PCompleted;

            player2Spinner4P.SpinStateChanged -= OnPlayer2Spinner4PStateChanged;
            player2Spinner4P.SpinStateChanged += OnPlayer2Spinner4PStateChanged;
        }
        if (player3Spinner4P != null)
        {
            player3Spinner4P.SpinCompleted -= OnPlayer3Spinner4PCompleted;
            player3Spinner4P.SpinCompleted += OnPlayer3Spinner4PCompleted;

            player3Spinner4P.SpinStateChanged -= OnPlayer3Spinner4PStateChanged;
            player3Spinner4P.SpinStateChanged += OnPlayer3Spinner4PStateChanged;
        }
        if (player4Spinner4P != null)
        {
            player4Spinner4P.SpinCompleted -= OnPlayer4Spinner4PCompleted;
            player4Spinner4P.SpinCompleted += OnPlayer4Spinner4PCompleted;

            player4Spinner4P.SpinStateChanged -= OnPlayer4Spinner4PStateChanged;
            player4Spinner4P.SpinStateChanged += OnPlayer4Spinner4PStateChanged;
        }
    }

    private void UnhookSpinner()
    {
        if (player2Spinner2P != null)
        {
            player2Spinner2P.SpinStateChanged -= OnSpinnerStateChanged;
            player2Spinner2P.SpinCompleted -= OnSpinnerCompleted;
        }

        if (player2Spinner4P != null) player2Spinner4P.SpinCompleted -= OnPlayer2Spinner4PCompleted;
        if (player3Spinner4P != null) player3Spinner4P.SpinCompleted -= OnPlayer3Spinner4PCompleted;
        if (player4Spinner4P != null) player4Spinner4P.SpinCompleted -= OnPlayer4Spinner4PCompleted;

        if (player2Spinner4P != null) player2Spinner4P.SpinStateChanged -= OnPlayer2Spinner4PStateChanged;
        if (player3Spinner4P != null) player3Spinner4P.SpinStateChanged -= OnPlayer3Spinner4PStateChanged;
        if (player4Spinner4P != null) player4Spinner4P.SpinStateChanged -= OnPlayer4Spinner4PStateChanged;
    }

    private void OnPlayer2Spinner4PStateChanged(bool isSpinning) => OnSpinnerStateChangedForIcon(isSpinning, searchIconPlayer2_4P, ref searchOrbitTween4P_P2, ref searchIconBaseAnchoredPos4P_P2);
    private void OnPlayer3Spinner4PStateChanged(bool isSpinning) => OnSpinnerStateChangedForIcon(isSpinning, searchIconPlayer3_4P, ref searchOrbitTween4P_P3, ref searchIconBaseAnchoredPos4P_P3);
    private void OnPlayer4Spinner4PStateChanged(bool isSpinning) => OnSpinnerStateChangedForIcon(isSpinning, searchIconPlayer4_4P, ref searchOrbitTween4P_P4, ref searchIconBaseAnchoredPos4P_P4);

    private void OnPlayer2Spinner4PCompleted() => On4PSpinnerCompleted(player2Spinner4P);
    private void OnPlayer3Spinner4PCompleted() => On4PSpinnerCompleted(player3Spinner4P);
    private void OnPlayer4Spinner4PCompleted() => On4PSpinnerCompleted(player4Spinner4P);

    private void On4PSpinnerCompleted(PlayerFindingProfileSpinner s)
    {
        if (!is4PActive) return;
        if (s != null)
        {
            Sprite chosen = s.SetRandomSpriteImmediate(used4PProfileSprites);
            if (chosen != null) used4PProfileSprites.Add(chosen);
        }

        remaining4PSpinners = Mathf.Max(0, remaining4PSpinners - 1);
        if (remaining4PSpinners <= 0)
        {
            PlayReturn();
        }
    }

    private void Stop4PSpinnerNow(PlayerFindingProfileSpinner s)
    {
        if (s == null) return;
        s.SetRandomSpriteImmediate();
        s.StopAndComplete();
    }

    private System.Collections.IEnumerator Stop4PSpinnersSequentialRoutine()
    {
        float first = Mathf.Max(0f, fourPStopFirstDelaySeconds);
        if (first > 0f) yield return new WaitForSecondsRealtime(first);

        Stop4PSpinnerNow(player2Spinner4P);

        float between = Mathf.Max(0f, fourPStopBetweenDelaySeconds);
        if (between > 0f) yield return new WaitForSecondsRealtime(between);

        Stop4PSpinnerNow(player3Spinner4P);

        if (between > 0f) yield return new WaitForSecondsRealtime(between);

        Stop4PSpinnerNow(player4Spinner4P);

        fourPStopCoroutine = null;
    }

    private void OnSpinnerCompleted()
    {
        PlayReturn();
    }

    private void PlayReturn()
    {
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }

        RectTransform activeVsBig = is4PActive ? (vsBig4P != null ? vsBig4P : vsBig) : vsBig;
        RectTransform activeVsSmall = is4PActive ? (vsSmall4P != null ? vsSmall4P : vsSmall) : vsSmall;

        if (particlesToDisable != null)
        {
            for (int i = 0; i < particlesToDisable.Length; i++)
            {
                if (particlesToDisable[i] != null) particlesToDisable[i].SetActive(false);
            }
        }

        if (particlesToEnableOn2PProfilePop != null)
        {
            for (int i = 0; i < particlesToEnableOn2PProfilePop.Length; i++)
            {
                if (particlesToEnableOn2PProfilePop[i] != null) particlesToEnableOn2PProfilePop[i].SetActive(false);
            }
        }

        if (particlesToEnableAfter2PVsEntrance != null)
        {
            for (int i = 0; i < particlesToEnableAfter2PVsEntrance.Length; i++)
            {
                if (particlesToEnableAfter2PVsEntrance[i] != null) particlesToEnableAfter2PVsEntrance[i].SetActive(false);
            }
        }

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        if (vsMoveDuration > 0f)
        {
            if (activeVsBig != null) seq.Append(activeVsBig.DOAnchorPos(vsBigHiddenPosition, vsMoveDuration).SetEase(vsEase));
            if (activeVsSmall != null) seq.Join(activeVsSmall.DOAnchorPos(vsSmallHiddenPosition, vsMoveDuration).SetEase(vsEase));
        }
        else
        {
            seq.AppendCallback(() =>
            {
                if (activeVsBig != null) activeVsBig.anchoredPosition = vsBigHiddenPosition;
                if (activeVsSmall != null) activeVsSmall.anchoredPosition = vsSmallHiddenPosition;
            });
        }

        if (vsScaleDuration > 0f)
        {
            if (activeVsBig != null) seq.Join(activeVsBig.DOScale(Vector3.zero, vsScaleDuration).SetEase(vsEase));
            if (activeVsSmall != null) seq.Join(activeVsSmall.DOScale(Vector3.zero, vsScaleDuration).SetEase(vsEase));
        }
        else
        {
            seq.AppendCallback(() =>
            {
                if (activeVsBig != null) activeVsBig.localScale = Vector3.zero;
                if (activeVsSmall != null) activeVsSmall.localScale = Vector3.zero;
            });
        }

        seq.AppendCallback(() =>
        {
            if (activeVsBig != null) activeVsBig.gameObject.SetActive(false);
            if (activeVsSmall != null) activeVsSmall.gameObject.SetActive(false);
        });

        if (bluePanel2P != null)
        {
            seq.Append(bluePanel2P.DOAnchorPos(bluePanelHiddenPosition, panelMoveDuration).SetEase(moveEase));
        }
        if (redPanel2P != null)
        {
            seq.Join(redPanel2P.DOAnchorPos(redPanelHiddenPosition, panelMoveDuration).SetEase(moveEase));
        }

        seq.AppendCallback(OnReturnFinished);
    }

    private void OnReturnFinished()
    {
        if (requireGameStartEventToOpenGameplay)
        {
            var socket = SocketConnection.Instance;
            if (socket == null || !socket.HasReceivedGameStart)
            {
                return;
            }
        }

        if (particlesToDisable != null)
        {
            for (int i = 0; i < particlesToDisable.Length; i++)
            {
                if (particlesToDisable[i] != null) particlesToDisable[i].SetActive(false);
            }
        }

        if (panelBackgroundImage == null)
        {
            panelBackgroundImage = GetComponent<Image>();
            if (panelBackgroundImage == null && transform.parent != null)
            {
                panelBackgroundImage = transform.parent.GetComponent<Image>();
            }
        }

        if (panelBackgroundImage != null)
        {
            panelBackgroundImage.enabled = false;
        }

        StartCoroutine(TransitionToGameplayRoutine());
    }

    private System.Collections.IEnumerator TransitionToGameplayRoutine()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        GameObject targetGameplayScreen = gameplayScreen;
        if (gameManager != null)
        {
            if (gameManager.IsVsBotMode && gameplayScreenVsBot != null)
            {
                targetGameplayScreen = gameplayScreenVsBot;
            }
            else if (!gameManager.IsVsBotMode && gameplayScreenVsPlayer != null)
            {
                targetGameplayScreen = gameplayScreenVsPlayer;
            }
        }

        if (gameplayBackgroundImage == null && targetGameplayScreen != null)
        {
            var allImages = targetGameplayScreen.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < allImages.Length; i++)
            {
                var img = allImages[i];
                if (img == null) continue;

                string n = img.gameObject.name;
                if (n == "bg" || n == "BackGround" || n == "Background" || n == "Bg")
                {
                    gameplayBackgroundImage = img;
                    break;
                }
            }

            if (gameplayBackgroundImage == null)
            {
                for (int i = 0; i < allImages.Length; i++)
                {
                    var img = allImages[i];
                    if (img == null) continue;
                    if (img.transform.parent == targetGameplayScreen.transform)
                    {
                        gameplayBackgroundImage = img;
                        break;
                    }
                }
            }
        }

        CardDeckAnimator targetCardAnimator = null;
        if (targetGameplayScreen != null)
        {
            // Prevent any auto-start OnEnable card animation from running before profiles settle.
            var allCardAnimators = targetGameplayScreen.GetComponentsInChildren<CardDeckAnimator>(true);
            for (int i = 0; i < allCardAnimators.Length; i++)
            {
                if (allCardAnimators[i] != null) allCardAnimators[i].enabled = false;
            }
        }

        if (targetGameplayScreen != null)
        {
            // Don't open via ScreenManager yet (it may disable this panel). We'll open it after moving profiles.
            targetGameplayScreen.SetActive(true);
        }

        Canvas.ForceUpdateCanvases();

        // As soon as gameplay is visible, hide PlayerFinding username roots and search/spinner objects.
        // (4P: hide 4 usernames + disable the 3 opponent spinners/search objects.)
        if (player1UserNameRoot2P != null) player1UserNameRoot2P.SetActive(false);
        if (player2UserNameRoot2P != null) player2UserNameRoot2P.SetActive(false);
        if (player1UserNameRoot4P != null) player1UserNameRoot4P.SetActive(false);
        if (player2UserNameRoot4P != null) player2UserNameRoot4P.SetActive(false);
        if (player3UserNameRoot4P != null) player3UserNameRoot4P.SetActive(false);
        if (player4UserNameRoot4P != null) player4UserNameRoot4P.SetActive(false);

        if (is4PActive)
        {
            if (player2Spinner4P != null) { player2Spinner4P.Stop(); player2Spinner4P.enabled = false; }
            if (player3Spinner4P != null) { player3Spinner4P.Stop(); player3Spinner4P.enabled = false; }
            if (player4Spinner4P != null) { player4Spinner4P.Stop(); player4Spinner4P.enabled = false; }
        }

        if (searchIcon != null) searchIcon.gameObject.SetActive(false);
        if (searchIconPlayer2_4P != null) searchIconPlayer2_4P.gameObject.SetActive(false);
        if (searchIconPlayer3_4P != null) searchIconPlayer3_4P.gameObject.SetActive(false);
        if (searchIconPlayer4_4P != null) searchIconPlayer4_4P.gameObject.SetActive(false);

        if (gameplayBackgroundImage != null)
        {
            gameplayBackgroundImage.color = gameplayBackgroundHoldColor;
        }

        yield return null;
        yield return null;

        float delay = Mathf.Max(0f, profileMoveDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        RectTransform p1Target = null;
        RectTransform p2Target = null;
        RectTransform p3Target = null;
        RectTransform p4Target = null;
        if (is4PActive)
        {
            p1Target = gameplayPlayer1ProfileTarget4P != null ? gameplayPlayer1ProfileTarget4P : gameplayPlayer1ProfileTarget;
            p2Target = gameplayPlayer2ProfileTarget4P != null ? gameplayPlayer2ProfileTarget4P : gameplayPlayer2ProfileTarget;
            p3Target = gameplayPlayer3ProfileTarget4P != null ? gameplayPlayer3ProfileTarget4P : gameplayPlayer3ProfileTarget;
            p4Target = gameplayPlayer4ProfileTarget4P != null ? gameplayPlayer4ProfileTarget4P : gameplayPlayer4ProfileTarget;
        }
        else
        {
            p1Target = gameplayPlayer1ProfileTarget2P != null ? gameplayPlayer1ProfileTarget2P : gameplayPlayer1ProfileTarget;
            p2Target = gameplayPlayer2ProfileTarget2P != null ? gameplayPlayer2ProfileTarget2P : gameplayPlayer2ProfileTarget;
        }

        bool p1HasTarget = p1Target != null;
        bool p2HasTarget = p2Target != null;
        bool p3HasTarget = p3Target != null;
        bool p4HasTarget = p4Target != null;

        // Re-parent to root canvas so layout/anchors don't override world-space tweens.
        Transform canvasRoot = null;
        Canvas c = GetComponentInParent<Canvas>();
        Canvas moverCanvas = null;
        if (c != null)
        {
            moverCanvas = c.rootCanvas != null ? c.rootCanvas : c;
            canvasRoot = moverCanvas.transform;
        }

        Transform p1OldParent = null;
        Transform p2OldParent = null;
        Transform p3OldParent = null;
        Transform p4OldParent = null;
        int p1OldSibling = 0;
        int p2OldSibling = 0;
        int p3OldSibling = 0;
        int p4OldSibling = 0;

        if (canvasRoot != null)
        {
            if (is4PActive)
            {
                if (player1Profile4P != null)
                {
                    p1OldParent = player1Profile4P.parent;
                    p1OldSibling = player1Profile4P.GetSiblingIndex();
                    player1Profile4P.SetParent(canvasRoot, worldPositionStays: true);
                    player1Profile4P.SetAsLastSibling();
                }
                if (player2Profile4P != null)
                {
                    p2OldParent = player2Profile4P.parent;
                    p2OldSibling = player2Profile4P.GetSiblingIndex();
                    player2Profile4P.SetParent(canvasRoot, worldPositionStays: true);
                    player2Profile4P.SetAsLastSibling();
                }
                if (player3Profile4P != null)
                {
                    p3OldParent = player3Profile4P.parent;
                    p3OldSibling = player3Profile4P.GetSiblingIndex();
                    player3Profile4P.SetParent(canvasRoot, worldPositionStays: true);
                    player3Profile4P.SetAsLastSibling();
                }
                if (player4Profile4P != null)
                {
                    p4OldParent = player4Profile4P.parent;
                    p4OldSibling = player4Profile4P.GetSiblingIndex();
                    player4Profile4P.SetParent(canvasRoot, worldPositionStays: true);
                    player4Profile4P.SetAsLastSibling();
                }
            }
            else
            {
                if (player1Profile2P != null)
                {
                    p1OldParent = player1Profile2P.parent;
                    p1OldSibling = player1Profile2P.GetSiblingIndex();
                    player1Profile2P.SetParent(canvasRoot, worldPositionStays: true);
                    player1Profile2P.SetAsLastSibling();
                }
                if (player2Profile2P != null)
                {
                    p2OldParent = player2Profile2P.parent;
                    p2OldSibling = player2Profile2P.GetSiblingIndex();
                    player2Profile2P.SetParent(canvasRoot, worldPositionStays: true);
                    player2Profile2P.SetAsLastSibling();
                }
            }
        }

        Sequence moveSeq = DOTween.Sequence();
        moveSeq.SetAutoKill(true);

        float dur = Mathf.Max(0.01f, profileMoveToGameplayDuration);
        float curve = Mathf.Max(0f, profileMoveCurveHeight);
        bool anyMoveTweenAdded = false;
        if (is4PActive)
        {
            // Curve directions: P1/P2 right-to-left, P3/P4 left-to-right.
            if (player1Profile4P != null && p1HasTarget)
            {
                moveSeq.Join(CreateCurvedWorldMoveTween(player1Profile4P, GetTargetWorldPositionOnMoverCanvas(p1Target, moverCanvas), -curve, dur));
                anyMoveTweenAdded = true;
            }
            if (player2Profile4P != null && p2HasTarget)
            {
                moveSeq.Join(CreateCurvedWorldMoveTween(player2Profile4P, GetTargetWorldPositionOnMoverCanvas(p2Target, moverCanvas), -curve, dur));
                anyMoveTweenAdded = true;
            }
            if (player3Profile4P != null && p3HasTarget)
            {
                moveSeq.Join(CreateCurvedWorldMoveTween(player3Profile4P, GetTargetWorldPositionOnMoverCanvas(p3Target, moverCanvas), curve, dur));
                anyMoveTweenAdded = true;
            }
            if (player4Profile4P != null && p4HasTarget)
            {
                moveSeq.Join(CreateCurvedWorldMoveTween(player4Profile4P, GetTargetWorldPositionOnMoverCanvas(p4Target, moverCanvas), curve, dur));
                anyMoveTweenAdded = true;
            }
        }
        else
        {
            if (player1Profile2P != null && p1HasTarget)
            {
                moveSeq.Join(CreateCurvedWorldMoveTween(player1Profile2P, GetTargetWorldPositionOnMoverCanvas(p1Target, moverCanvas), -curve, dur));
                anyMoveTweenAdded = true;
            }
            if (player2Profile2P != null && p2HasTarget)
            {
                moveSeq.Join(CreateCurvedWorldMoveTween(player2Profile2P, GetTargetWorldPositionOnMoverCanvas(p2Target, moverCanvas), curve, dur));
                anyMoveTweenAdded = true;
            }
        }

        if (!anyMoveTweenAdded)
        {
            moveSeq.AppendInterval(dur);
        }

        // Scale down AFTER movement so the travel is visible.
        moveSeq.AppendCallback(() =>
        {
            if (is4PActive)
            {
                if (player1Profile4P != null) player1Profile4P.DOKill();
                if (player2Profile4P != null) player2Profile4P.DOKill();
                if (player3Profile4P != null) player3Profile4P.DOKill();
                if (player4Profile4P != null) player4Profile4P.DOKill();
            }
            else
            {
                if (player1Profile2P != null) player1Profile2P.DOKill();
                if (player2Profile2P != null) player2Profile2P.DOKill();
            }
        });

        float scaleDur = Mathf.Max(0.01f, dur * 0.6f);
        if (is4PActive)
        {
            if (player1Profile4P != null) moveSeq.Append(player1Profile4P.DOScale(Vector3.zero, scaleDur).SetEase(profileMoveToGameplayEase));
            if (player2Profile4P != null) moveSeq.Join(player2Profile4P.DOScale(Vector3.zero, scaleDur).SetEase(profileMoveToGameplayEase));
            if (player3Profile4P != null) moveSeq.Join(player3Profile4P.DOScale(Vector3.zero, scaleDur).SetEase(profileMoveToGameplayEase));
            if (player4Profile4P != null) moveSeq.Join(player4Profile4P.DOScale(Vector3.zero, scaleDur).SetEase(profileMoveToGameplayEase));
        }
        else
        {
            if (player1Profile2P != null) moveSeq.Append(player1Profile2P.DOScale(Vector3.zero, scaleDur).SetEase(profileMoveToGameplayEase));
            if (player2Profile2P != null) moveSeq.Join(player2Profile2P.DOScale(Vector3.zero, scaleDur).SetEase(profileMoveToGameplayEase));
        }

        yield return moveSeq.WaitForCompletion();

        // Hard guarantee: profiles must be gone before gameplay card animation starts.
        if (is4PActive)
        {
            if (player1Profile4P != null) { player1Profile4P.localScale = Vector3.zero; player1Profile4P.gameObject.SetActive(false); }
            if (player2Profile4P != null) { player2Profile4P.localScale = Vector3.zero; player2Profile4P.gameObject.SetActive(false); }
            if (player3Profile4P != null) { player3Profile4P.localScale = Vector3.zero; player3Profile4P.gameObject.SetActive(false); }
            if (player4Profile4P != null) { player4Profile4P.localScale = Vector3.zero; player4Profile4P.gameObject.SetActive(false); }
        }
        else
        {
            if (player1Profile2P != null) { player1Profile2P.localScale = Vector3.zero; player1Profile2P.gameObject.SetActive(false); }
            if (player2Profile2P != null) { player2Profile2P.localScale = Vector3.zero; player2Profile2P.gameObject.SetActive(false); }
        }

        if (targetGameplayScreen != null && openGameplayViaScreenManagerAfterTransition && screenManager != null)
        {
            screenManager.OpenScreen(targetGameplayScreen);
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
        if (gameManager != null)
        {
            gameManager.PopGameplayProfilesAfterPlayerFinding();
        }

        if (gameplayBackgroundImage != null)
        {
            float fadeDur = Mathf.Max(0.01f, gameplayBackgroundFadeToWhiteDuration);
            yield return gameplayBackgroundImage
                .DOColor(gameplayBackgroundFinalColor, fadeDur)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .WaitForCompletion();
        }

        // Now it's safe to start the card animation.
        if (targetGameplayScreen != null)
        {
            var animators = targetGameplayScreen.GetComponentsInChildren<CardDeckAnimator>(true);
            if (animators != null && animators.Length > 0)
            {
                // Prefer the animator that belongs to the currently active board.
                CardDeckAnimator activeAnimator = null;

                // 1) Active in hierarchy + name hint (4P/2P)
                string prefer = is4PActive ? "4" : "2";
                for (int i = 0; i < animators.Length; i++)
                {
                    var a = animators[i];
                    if (a == null) continue;
                    if (!a.gameObject.activeInHierarchy) continue;
                    if (a.gameObject.name.Contains(prefer)) { activeAnimator = a; break; }
                }

                // 2) Any active in hierarchy
                if (activeAnimator == null)
                {
                    for (int i = 0; i < animators.Length; i++)
                    {
                        var a = animators[i];
                        if (a == null) continue;
                        if (a.gameObject.activeInHierarchy) { activeAnimator = a; break; }
                    }
                }

                // 3) Fallback: first one (won't run unless enabled)
                if (activeAnimator == null)
                {
                    activeAnimator = animators[0];
                }

                if (activeAnimator != null && activeAnimator.gameObject.activeInHierarchy)
                {
                    activeAnimator.enabled = true;
                    cardDeckAnimator = activeAnimator;
                }
            }
        }

        yield return null;

        if (cardDeckAnimator != null && cardDeckAnimator.isActiveAndEnabled)
        {
            cardDeckAnimator.StartCardAnimation();
        }

        // Disable PlayerFinding AFTER gameplay is visible and card animation has been triggered.
        RestoreProfilesToDefaults();
        if (playerFindingScreenRoot != null)
        {
            playerFindingScreenRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private Tween CreateCurvedWorldMoveTween(RectTransform rt, Vector3 targetWorldPos, float curveYOffset, float duration)
    {
        if (rt == null) return null;

        Vector3 start = rt.position;
        Vector3 end = targetWorldPos;

        Vector3 mid = (start + end) * 0.5f;
        mid.y += curveYOffset;

        Tween t = DOTween.To(() => 0f, a =>
        {
            float u = 1f - a;
            Vector3 p = (u * u * start) + (2f * u * a * mid) + (a * a * end);
            rt.position = p;
        }, 1f, Mathf.Max(0.01f, duration)).SetEase(profileMoveToGameplayEase);

        return t;
    }

    private static Vector3 GetRectWorldCenter(RectTransform rt)
    {
        if (rt == null) return Vector3.zero;
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    private static Vector3 GetTargetWorldPositionOnMoverCanvas(RectTransform target, Canvas moverCanvas)
    {
        if (target == null) return Vector3.zero;

        // If we can't resolve canvases, fall back to raw world center.
        if (moverCanvas == null) return GetRectWorldCenter(target);

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCam = null;
        if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            targetCam = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;
        }

        Vector3 targetWorldCenter = GetRectWorldCenter(target);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCam, targetWorldCenter);

        RectTransform moverRect = moverCanvas.transform as RectTransform;
        if (moverRect == null) return targetWorldCenter;

        Camera moverCam = null;
        if (moverCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            moverCam = moverCanvas.worldCamera != null ? moverCanvas.worldCamera : Camera.main;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(moverRect, screenPoint, moverCam, out Vector3 worldOnMover))
        {
            return worldOnMover;
        }

        return targetWorldCenter;
    }

    private void OnSpinnerStateChanged(bool isSpinning)
    {
        OnSpinnerStateChangedForIcon(isSpinning, searchIcon, ref searchOrbitTween, ref searchIconBaseAnchoredPos);
    }

    private void OnSpinnerStateChangedForIcon(bool isSpinning, RectTransform icon, ref Tween orbitTween, ref Vector2 baseAnchoredPos)
    {
        if (icon == null) return;

        if (isSpinning)
        {
            icon.gameObject.SetActive(true);

            if (orbitTween != null)
            {
                orbitTween.Kill();
                orbitTween = null;
            }

            icon.localRotation = Quaternion.identity;
            baseAnchoredPos = icon.anchoredPosition;
            Vector2 capturedBasePos = baseAnchoredPos;

            float dur = Mathf.Max(0.05f, searchOrbitDuration);
            float radius = Mathf.Max(0f, searchOrbitRadius);
            float dir = searchOrbitClockwise ? 1f : -1f;

            orbitTween = DOTween.To(() => 0f, a =>
            {
                float rad = a * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                icon.anchoredPosition = capturedBasePos + (offset * dir);
            }, 360f, dur)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }
        else
        {
            if (orbitTween != null)
            {
                orbitTween.Kill();
                orbitTween = null;
            }
            icon.localRotation = Quaternion.identity;
            icon.anchoredPosition = baseAnchoredPos;
            icon.gameObject.SetActive(false);
        }
    }

    public void PlayOpen()
    {
        Kill();

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        int playerCount = 2;
        if (gameManager != null)
        {
            playerCount = Mathf.Clamp(gameManager.GetActivePlayerCountPublic(), 2, 4);
        }

        bool is4P = playerCount >= 4;
        is4PActive = is4P;
        if (twoPlayerVsRoot != null) twoPlayerVsRoot.SetActive(!is4P);
        if (fourPlayerVsRoot != null) fourPlayerVsRoot.SetActive(is4P);

        if (particlesToDisable != null)
        {
            for (int i = 0; i < particlesToDisable.Length; i++)
            {
                if (particlesToDisable[i] != null) particlesToDisable[i].SetActive(false);
            }
        }

        if (is4P)
        {
            if (player1Profile2P != null) player1Profile2P.gameObject.SetActive(false);
            if (player2Profile2P != null) player2Profile2P.gameObject.SetActive(false);
            if (bluePanel2P != null) bluePanel2P.gameObject.SetActive(false);
            if (redPanel2P != null) redPanel2P.gameObject.SetActive(false);
            if (redPanel4P != null) { redPanel4P.gameObject.SetActive(true); redPanel4P.anchoredPosition = redPanel4PHiddenPosition; }
            if (greenPanel4P != null) { greenPanel4P.gameObject.SetActive(true); greenPanel4P.anchoredPosition = greenPanel4PHiddenPosition; }
            if (yellowPanel4P != null) { yellowPanel4P.gameObject.SetActive(true); yellowPanel4P.anchoredPosition = yellowPanel4PHiddenPosition; }
            if (bluePanel4P != null) { bluePanel4P.gameObject.SetActive(true); bluePanel4P.anchoredPosition = bluePanel4PHiddenPosition; }
            if (player2Spinner2P != null) player2Spinner2P.Stop();
            if (player2Spinner4P != null) player2Spinner4P.Stop();
            if (player3Spinner4P != null) player3Spinner4P.Stop();
            if (player4Spinner4P != null) player4Spinner4P.Stop();
            if (searchIcon != null) searchIcon.gameObject.SetActive(false);
            if (vsBig != null)
            {
                vsBig.gameObject.SetActive(true);
                vsBig.anchoredPosition = vsBigHiddenPosition;
                vsBig.localScale = Vector3.zero;
            }
            if (vsSmall != null)
            {
                vsSmall.gameObject.SetActive(true);
                vsSmall.anchoredPosition = vsSmallHiddenPosition;
                vsSmall.localScale = Vector3.zero;
            }
            if (player1UserNameRoot2P != null) player1UserNameRoot2P.SetActive(false);
            if (player2UserNameRoot2P != null) player2UserNameRoot2P.SetActive(false);

            if (player1UserNameRoot4P != null) player1UserNameRoot4P.SetActive(false);
            if (player2UserNameRoot4P != null) player2UserNameRoot4P.SetActive(false);
            if (player3UserNameRoot4P != null) player3UserNameRoot4P.SetActive(false);
            if (player4UserNameRoot4P != null) player4UserNameRoot4P.SetActive(false);

            if (player1Profile4P != null) { player1Profile4P.gameObject.SetActive(false); }
            if (player2Profile4P != null) { player2Profile4P.gameObject.SetActive(false); }
            if (player3Profile4P != null) { player3Profile4P.gameObject.SetActive(false); }
            if (player4Profile4P != null) { player4Profile4P.gameObject.SetActive(false); }

            seq = DOTween.Sequence();
            seq.SetAutoKill(true);

            if (redPanel4P != null) seq.Append(redPanel4P.DOAnchorPos(redPanel4PShownPosition, panelMoveDuration).SetEase(moveEase));
            if (greenPanel4P != null) seq.Join(greenPanel4P.DOAnchorPos(greenPanel4PShownPosition, panelMoveDuration).SetEase(moveEase));
            if (yellowPanel4P != null) seq.Join(yellowPanel4P.DOAnchorPos(yellowPanel4PShownPosition, panelMoveDuration).SetEase(moveEase));
            if (bluePanel4P != null) seq.Join(bluePanel4P.DOAnchorPos(bluePanel4PShownPosition, panelMoveDuration).SetEase(moveEase));

            seq.AppendCallback(() =>
            {
                PlayMergeHaptic();
            });

            // V/S entrance for 4P.
            seq.AppendCallback(() =>
            {
                if (vsBig != null) vsBig.gameObject.SetActive(true);
                if (vsSmall != null) vsSmall.gameObject.SetActive(true);
            });

            if (vsMoveDuration > 0f)
            {
                if (vsBig != null) seq.Append(vsBig.DOAnchorPos(vsBigShownPosition, vsMoveDuration).SetEase(vsEase));
                if (vsSmall != null) seq.Join(vsSmall.DOAnchorPos(vsSmallShownPosition, vsMoveDuration).SetEase(vsEase));
            }
            else
            {
                seq.AppendCallback(() =>
                {
                    if (vsBig != null) vsBig.anchoredPosition = vsBigShownPosition;
                    if (vsSmall != null) vsSmall.anchoredPosition = vsSmallShownPosition;
                });
            }

            if (vsScaleDuration > 0f)
            {
                if (vsBig != null) seq.Join(vsBig.DOScale(Vector3.one, vsScaleDuration).SetEase(vsEase));
                if (vsSmall != null) seq.Join(vsSmall.DOScale(Vector3.one, vsScaleDuration).SetEase(vsEase));
            }
            else
            {
                seq.AppendCallback(() =>
                {
                    if (vsBig != null) vsBig.localScale = Vector3.one;
                    if (vsSmall != null) vsSmall.localScale = Vector3.one;
                });
            }

            seq.AppendCallback(() =>
            {
                if (particlesToEnableOn4PProfilePop != null)
                {
                    for (int i = 0; i < particlesToEnableOn4PProfilePop.Length; i++)
                    {
                        if (particlesToEnableOn4PProfilePop[i] != null) particlesToEnableOn4PProfilePop[i].SetActive(true);
                    }
                }

                if (player1Profile4P != null) { player1Profile4P.gameObject.SetActive(true); player1Profile4P.localScale = Vector3.zero; }
                if (player2Profile4P != null) { player2Profile4P.gameObject.SetActive(true); player2Profile4P.localScale = Vector3.zero; }
                if (player3Profile4P != null) { player3Profile4P.gameObject.SetActive(true); player3Profile4P.localScale = Vector3.zero; }
                if (player4Profile4P != null) { player4Profile4P.gameObject.SetActive(true); player4Profile4P.localScale = Vector3.zero; }
            });

            float popDur = Mathf.Max(0.01f, profilePopDuration);
            float s = Mathf.Max(0.01f, profileFinalScale4P);
            Vector3 targetScale = use4PProfileFinalScaleOverride ? new Vector3(s, s, s) : Vector3.one;
            if (!use4PProfileFinalScaleOverride)
            {
                // Base scale per profile if you want to keep each profile's authored scale.
                if (player1Profile4P != null) seq.Join(player1Profile4P.DOScale(player1Profile4PBaseScale, popDur).SetEase(profilePopEase));
                if (player2Profile4P != null) seq.Join(player2Profile4P.DOScale(player2Profile4PBaseScale, popDur).SetEase(profilePopEase));
                if (player3Profile4P != null) seq.Join(player3Profile4P.DOScale(player3Profile4PBaseScale, popDur).SetEase(profilePopEase));
                if (player4Profile4P != null) seq.Join(player4Profile4P.DOScale(player4Profile4PBaseScale, popDur).SetEase(profilePopEase));
            }
            else
            {
                if (player1Profile4P != null) seq.Join(player1Profile4P.DOScale(targetScale, popDur).SetEase(profilePopEase));
                if (player2Profile4P != null) seq.Join(player2Profile4P.DOScale(targetScale, popDur).SetEase(profilePopEase));
                if (player3Profile4P != null) seq.Join(player3Profile4P.DOScale(targetScale, popDur).SetEase(profilePopEase));
                if (player4Profile4P != null) seq.Join(player4Profile4P.DOScale(targetScale, popDur).SetEase(profilePopEase));
            }

            seq.AppendCallback(() =>
            {
                AnimateUserName(player1UserNameRoot4P);
                AnimateUserName(player2UserNameRoot4P);
                AnimateUserName(player3UserNameRoot4P);
                AnimateUserName(player4UserNameRoot4P);

                remaining4PSpinners = 0;
                if (player2Spinner4P != null) remaining4PSpinners++;
                if (player3Spinner4P != null) remaining4PSpinners++;
                if (player4Spinner4P != null) remaining4PSpinners++;

                used4PProfileSprites.Clear();

                if (player2Spinner4P != null) player2Spinner4P.Play();
                if (player3Spinner4P != null) player3Spinner4P.Play();
                if (player4Spinner4P != null) player4Spinner4P.Play();

                if (fourPStopCoroutine != null)
                {
                    StopCoroutine(fourPStopCoroutine);
                    fourPStopCoroutine = null;
                }

                // If no spinners assigned, proceed immediately.
                if (remaining4PSpinners <= 0)
                {
                    PlayReturn();
                }
            });
            return;
        }

        if (player1Profile4P != null) player1Profile4P.gameObject.SetActive(false);
        if (player2Profile4P != null) player2Profile4P.gameObject.SetActive(false);
        if (player3Profile4P != null) player3Profile4P.gameObject.SetActive(false);
        if (player4Profile4P != null) player4Profile4P.gameObject.SetActive(false);

        if (redPanel4P != null) redPanel4P.gameObject.SetActive(false);
        if (greenPanel4P != null) greenPanel4P.gameObject.SetActive(false);
        if (yellowPanel4P != null) yellowPanel4P.gameObject.SetActive(false);
        if (bluePanel4P != null) bluePanel4P.gameObject.SetActive(false);

        if (player1UserNameRoot2P != null) player1UserNameRoot2P.SetActive(false);
        if (player2UserNameRoot2P != null) player2UserNameRoot2P.SetActive(false);

        if (player1UserNameRoot4P != null) player1UserNameRoot4P.SetActive(false);
        if (player2UserNameRoot4P != null) player2UserNameRoot4P.SetActive(false);
        if (player3UserNameRoot4P != null) player3UserNameRoot4P.SetActive(false);
        if (player4UserNameRoot4P != null) player4UserNameRoot4P.SetActive(false);

        if (player2Spinner2P != null)
        {
            player2Spinner2P.Stop();
        }

        if (searchIcon != null)
        {
            searchIcon.localRotation = Quaternion.identity;
            searchIcon.anchoredPosition = searchIconBaseAnchoredPos;
            searchIcon.gameObject.SetActive(false);
        }

        if (vsBig != null)
        {
            vsBig.gameObject.SetActive(false);
            vsBig.anchoredPosition = vsBigHiddenPosition;
            vsBig.localScale = Vector3.zero;
        }

        if (vsSmall != null)
        {
            vsSmall.gameObject.SetActive(false);
            vsSmall.anchoredPosition = vsSmallHiddenPosition;
            vsSmall.localScale = Vector3.zero;
        }

        if (player1Profile2P != null)
        {
            player1Profile2P.localScale = Vector3.zero;
            player1Profile2P.gameObject.SetActive(true);
        }

        if (player2Profile2P != null)
        {
            player2Profile2P.localScale = Vector3.zero;
            player2Profile2P.gameObject.SetActive(true);
        }

        if (bluePanel2P != null)
        {
            bluePanel2P.anchoredPosition = bluePanelHiddenPosition;
            bluePanel2P.gameObject.SetActive(true);
        }

        if (redPanel2P != null)
        {
            redPanel2P.anchoredPosition = redPanelHiddenPosition;
            redPanel2P.gameObject.SetActive(true);
        }

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        if (bluePanel2P != null) seq.Append(bluePanel2P.DOAnchorPos(bluePanelShownPosition, panelMoveDuration).SetEase(moveEase));
        if (redPanel2P != null) seq.Append(redPanel2P.DOAnchorPos(redPanelShownPosition, panelMoveDuration).SetEase(moveEase));

        seq.AppendCallback(() =>
        {
            PlayMergeHaptic();
        });

        float pop = Mathf.Max(0.01f, profilePopDuration);
        if (player1Profile2P != null) seq.Append(player1Profile2P.DOScale(player1Profile2PBaseScale, pop).SetEase(profilePopEase));
        if (player2Profile2P != null) seq.Join(player2Profile2P.DOScale(player2Profile2PBaseScale, pop).SetEase(profilePopEase));

        seq.AppendCallback(() =>
        {
            AnimateUserName(player1UserNameRoot2P);
            AnimateUserName(player2UserNameRoot2P);

            if (particlesToEnableOn2PProfilePop != null)
            {
                for (int i = 0; i < particlesToEnableOn2PProfilePop.Length; i++)
                {
                    if (particlesToEnableOn2PProfilePop[i] != null) particlesToEnableOn2PProfilePop[i].SetActive(true);
                }
            }

            if (player2Spinner2P != null)
            {
                player2Spinner2P.Play();
            }
        });

        if (vsBig != null || vsSmall != null)
        {
            seq.AppendCallback(() =>
            {
                if (vsBig != null)
                {
                    vsBig.gameObject.SetActive(true);
                    vsBig.anchoredPosition = vsBigHiddenPosition;
                    vsBig.localScale = Vector3.zero;
                }
                if (vsSmall != null)
                {
                    vsSmall.gameObject.SetActive(true);
                    vsSmall.anchoredPosition = vsSmallHiddenPosition;
                    vsSmall.localScale = Vector3.zero;
                }
            });

            if (vsMoveDuration > 0f)
            {
                if (vsBig != null) seq.Append(vsBig.DOAnchorPos(vsBigShownPosition, vsMoveDuration).SetEase(vsEase));
                if (vsSmall != null) seq.Join(vsSmall.DOAnchorPos(vsSmallShownPosition, vsMoveDuration).SetEase(vsEase));
            }
            else
            {
                seq.AppendCallback(() =>
                {
                    if (vsBig != null) vsBig.anchoredPosition = vsBigShownPosition;
                    if (vsSmall != null) vsSmall.anchoredPosition = vsSmallShownPosition;
                });
            }

            if (vsScaleDuration > 0f)
            {
                if (vsBig != null) seq.Join(vsBig.DOScale(Vector3.one, vsScaleDuration).SetEase(vsEase));
                if (vsSmall != null) seq.Join(vsSmall.DOScale(Vector3.one, vsScaleDuration).SetEase(vsEase));
            }
            else
            {
                seq.AppendCallback(() =>
                {
                    if (vsBig != null) vsBig.localScale = Vector3.one;
                    if (vsSmall != null) vsSmall.localScale = Vector3.one;
                });
            }

            seq.AppendCallback(() =>
            {
                if (particlesToEnableAfter2PVsEntrance != null)
                {
                    for (int i = 0; i < particlesToEnableAfter2PVsEntrance.Length; i++)
                    {
                        if (particlesToEnableAfter2PVsEntrance[i] != null) particlesToEnableAfter2PVsEntrance[i].SetActive(true);
                    }
                }
            });
        }
    }

    private void AnimateUserName(GameObject root)
    {
        if (root == null) return;

        root.SetActive(true);
        Transform t = root.transform;
        t.DOKill();
        t.localScale = Vector3.zero;
        t.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
    }

    private void PlayMergeHaptic()
    {
        if (HapticsManager.Instance == null) return;
        HapticsManager.Instance.Pattern(new long[] { 0, 35, 25, 75 }, new int[] { 160, 0, 255 });
    }

    private void Kill()
    {
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }

        if (searchOrbitTween != null)
        {
            searchOrbitTween.Kill();
            searchOrbitTween = null;
        }

        if (searchOrbitTween4P_P2 != null) { searchOrbitTween4P_P2.Kill(); searchOrbitTween4P_P2 = null; }
        if (searchOrbitTween4P_P3 != null) { searchOrbitTween4P_P3.Kill(); searchOrbitTween4P_P3 = null; }
        if (searchOrbitTween4P_P4 != null) { searchOrbitTween4P_P4.Kill(); searchOrbitTween4P_P4 = null; }

        if (searchIcon != null)
        {
            searchIcon.localRotation = Quaternion.identity;
            searchIcon.anchoredPosition = searchIconBaseAnchoredPos;
        }

        if (searchIconPlayer2_4P != null)
        {
            searchIconPlayer2_4P.localRotation = Quaternion.identity;
            searchIconPlayer2_4P.anchoredPosition = searchIconBaseAnchoredPos4P_P2;
            searchIconPlayer2_4P.gameObject.SetActive(false);
        }
        if (searchIconPlayer3_4P != null)
        {
            searchIconPlayer3_4P.localRotation = Quaternion.identity;
            searchIconPlayer3_4P.anchoredPosition = searchIconBaseAnchoredPos4P_P3;
            searchIconPlayer3_4P.gameObject.SetActive(false);
        }
        if (searchIconPlayer4_4P != null)
        {
            searchIconPlayer4_4P.localRotation = Quaternion.identity;
            searchIconPlayer4_4P.anchoredPosition = searchIconBaseAnchoredPos4P_P4;
            searchIconPlayer4_4P.gameObject.SetActive(false);
        }

        if (vsBig != null) vsBig.DOKill();
        if (vsSmall != null) vsSmall.DOKill();
        if (vsBig4P != null) vsBig4P.DOKill();
        if (vsSmall4P != null) vsSmall4P.DOKill();

        if (bluePanel2P != null) bluePanel2P.DOKill();
        if (redPanel2P != null) redPanel2P.DOKill();

        if (redPanel4P != null) redPanel4P.DOKill();
        if (greenPanel4P != null) greenPanel4P.DOKill();
        if (yellowPanel4P != null) yellowPanel4P.DOKill();
        if (bluePanel4P != null) bluePanel4P.DOKill();

        if (player1Profile2P != null) player1Profile2P.DOKill();
        if (player2Profile2P != null) player2Profile2P.DOKill();

        if (player1Profile4P != null) player1Profile4P.DOKill();
        if (player2Profile4P != null) player2Profile4P.DOKill();
        if (player3Profile4P != null) player3Profile4P.DOKill();
        if (player4Profile4P != null) player4Profile4P.DOKill();
    }
}
