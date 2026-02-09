using NewGame.API;
using System;
using UnityEngine;
using FancyScrollView.Example09;
using System.Collections;
using System.Collections.Generic;

public class LoginController : MonoBehaviour
{
    [Header("Next Screen")]
    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private GameObject homeOrLobbyScreen;

    [Header("Login UI")]
    [SerializeField] private TwoButtonTweenAnimator loginButtonsAnimator;
    [SerializeField] private GameObject gameLoaderPanel;
    [SerializeField] private float loaderSeconds = 2f;

    [Header("Welcome Popup (Optional)")]
    [SerializeField] private WelcomeLoginPopup welcomePopup;

    [Header("Currency Toggle (Offline/Online)")]
    [SerializeField] private List<GameObject> coinRoots = new List<GameObject>();
    [SerializeField] private List<GameObject> diamondRoots = new List<GameObject>();
    [SerializeField] private List<GameObject> offlineStarRoots = new List<GameObject>();

    [SerializeField] private float internetPollSeconds = 0.5f;

    private Coroutine internetWatchCoroutine;
    private NetworkReachability lastReachability;

    private void OnEnable()
    {
        if (internetWatchCoroutine != null)
        {
            StopCoroutine(internetWatchCoroutine);
            internetWatchCoroutine = null;
        }

        lastReachability = Application.internetReachability;
        ApplyCurrencyVisibility(lastReachability == NetworkReachability.NotReachable);
        internetWatchCoroutine = StartCoroutine(WatchInternetReachability());
    }

    private void OnDisable()
    {
        if (internetWatchCoroutine != null)
        {
            StopCoroutine(internetWatchCoroutine);
            internetWatchCoroutine = null;
        }
    }

    private IEnumerator WatchInternetReachability()
    {
        float interval = Mathf.Max(0.1f, internetPollSeconds);
        while (true)
        {
            yield return new WaitForSecondsRealtime(interval);

            NetworkReachability now = Application.internetReachability;
            if (now == lastReachability) continue;

            lastReachability = now;
            ApplyCurrencyVisibility(now == NetworkReachability.NotReachable);
        }
    }

    private void ApplyCurrencyVisibility(bool offline)
    {
        SetRootsActive(coinRoots, !offline);
        SetRootsActive(diamondRoots, !offline);
        SetRootsActive(offlineStarRoots, offline);
    }

    private void SetRootsActive(List<GameObject> roots, bool active)
    {
        if (roots == null) return;
        for (int i = 0; i < roots.Count; i++)
        {
            GameObject r = roots[i];
            if (r == null) continue;
            r.SetActive(active);
        }
    }

    // 🎮 AS GUEST BUTTON CLICK
    public void LoginAsGuest()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            LoginAsGuestOffline();
            return;
        }

        string userId = GenerateGuestUserId();
        string username = GenerateGuestUsername();

        Debug.Log(
            $"<color=#00ffff><b>GUEST LOGIN</b></color> → " +
            $"user_id={userId}, username={username}"
        );

        // ✅ isGuest = true
        LoginRequest request = new LoginRequest
        {
            user_id = userId,
            username = username,
            isGuest = true
        };

        string json = JsonUtility.ToJson(request);

        if (gameLoaderPanel != null)
        {
            gameLoaderPanel.SetActive(true);
            GameLoaderPanelAnimator anim = gameLoaderPanel.GetComponent<GameLoaderPanelAnimator>();
            if (anim != null)
            {
                anim.ShowLoader();
            }
        }

        ApiManager.Instance.Post(
            ApiManager.Instance.GetLoginApiUrl(),
            json,
            OnLoginSuccess,
            OnLoginError
        );
    }

    private void LoginAsGuestOffline()
    {
        UserSession.LoadFromPrefs();

        bool hasStoredUser =
            !string.IsNullOrEmpty(UserSession.UserId) &&
            !string.IsNullOrEmpty(UserSession.Username);

        string userId = hasStoredUser ? UserSession.UserId : GenerateGuestUserId();
        string username = hasStoredUser ? UserSession.Username : GenerateGuestUsername();

        bool firstTimeOfflineGuest = !hasStoredUser;

        UserSession.Apply(
            username: username,
            userId: userId,
            avatarIndex: 0,
            isGuest: true,
            coins: 0,
            diamonds: 0,
            jwtToken: string.Empty,
            saveToPrefs: true
        );

        PlayerPrefs.SetString("LOGIN_TYPE", "GUEST");
        PlayerPrefs.SetString("USER_DATA", string.Empty);
        PlayerPrefs.Save();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerName(UserSession.Username, saveToPrefs: true);
            GameManager.Instance.SetPlayerAvatar(UserSession.AvatarIndex, null, saveToPrefs: true);
            GameManager.Instance.RefreshSessionDebugFields();
        }

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetCoins(0);
            PlayerWallet.Instance.SetDiamonds(0);
            if (firstTimeOfflineGuest)
            {
                PlayerWallet.EnsureOfflineStarsInitializedForCurrentUser(1000, out int seeded);
                PlayerWallet.Instance.SetOfflineStars(seeded);
            }
        }
        else
        {
            PlayerPrefs.SetInt("PLAYER_COINS", 0);
            PlayerPrefs.SetInt("PLAYER_DIAMONDS", 0);
            if (firstTimeOfflineGuest)
            {
                PlayerWallet.EnsureOfflineStarsInitializedForCurrentUser(1000, out _);
            }
            PlayerPrefs.Save();
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        StartCoroutine(PostLoginSequence(showWelcomePopup: false));
    }

    #region Guest Helpers

    private string GenerateGuestUserId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 10);
    }

    private string GenerateGuestUsername()
    {
        int random = UnityEngine.Random.Range(1000, 9999);
        return "Guest_" + random;
    }

    #endregion

    #region API Callbacks

    private void OnLoginSuccess(string response)
    {
        Debug.Log(
            $"<color=#00ff7f><b>LOGIN SUCCESS</b></color> → {response}"
        );

        if (gameLoaderPanel != null)
        {
            GameLoaderPanelAnimator anim = gameLoaderPanel.GetComponent<GameLoaderPanelAnimator>();
            if (anim != null)
            {
                anim.Hide();
            }
            else
            {
                gameLoaderPanel.SetActive(false);
            }
        }

        PlayerPrefs.SetString("LOGIN_TYPE", "GUEST");
        PlayerPrefs.SetString("USER_DATA", response);
        PlayerPrefs.Save();

        UserSession.TryApplyLoginResponse(response);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerName(UserSession.Username, saveToPrefs: true);
            GameManager.Instance.SetPlayerAvatar(UserSession.AvatarIndex, null, saveToPrefs: true);
            GameManager.Instance.RefreshSessionDebugFields();
        }

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetCoins(UserSession.Coins);
            PlayerWallet.Instance.SetDiamonds(UserSession.Diamonds);

            PlayerWallet.EnsureOfflineStarsInitializedForCurrentUser(1000, out int seeded);
            PlayerWallet.Instance.SetOfflineStars(seeded);
        }
        else
        {
            PlayerPrefs.SetInt("PLAYER_COINS", UserSession.Coins);
            PlayerPrefs.SetInt("PLAYER_DIAMONDS", UserSession.Diamonds);

            PlayerWallet.EnsureOfflineStarsInitializedForCurrentUser(1000, out _);
            PlayerPrefs.Save();
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (welcomePopup == null)
        {
            welcomePopup = FindObjectOfType<WelcomeLoginPopup>();
        }

        StartCoroutine(PostLoginSequence(showWelcomePopup: true));
    }

    private IEnumerator PostLoginSequence(bool showWelcomePopup)
    {
        if (loginButtonsAnimator == null)
        {
            loginButtonsAnimator = FindObjectOfType<TwoButtonTweenAnimator>();
        }

        bool closeDone = false;
        if (loginButtonsAnimator != null)
        {
            loginButtonsAnimator.Close(() => closeDone = true);
        }
        else
        {
            closeDone = true;
        }

        while (!closeDone) yield return null;

        if (!showWelcomePopup)
        {
            OpenHome();
            yield break;
        }

        bool showedPopup = false;
        if (showWelcomePopup && welcomePopup != null)
        {
            showedPopup = welcomePopup.ShowIfFirstTime(UserSession.UserId, UserSession.Username, OpenHome);
        }

        if (!showedPopup)
        {
            OpenHome();
        }
    }

    private void OpenHome()
    {
        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (screenManager != null && homeOrLobbyScreen != null)
        {
            screenManager.OpenScreen(homeOrLobbyScreen);
        }
    }

    private void OnLoginError(string error)
    {
        Debug.LogError(
            $"<color=#ff4c4c><b>LOGIN FAILED</b></color> → {error}"
        );

        if (gameLoaderPanel != null)
        {
            gameLoaderPanel.SetActive(true);
            GameLoaderPanelAnimator anim = gameLoaderPanel.GetComponent<GameLoaderPanelAnimator>();
            if (anim != null)
            {
                anim.ShowError(string.IsNullOrWhiteSpace(error)
                    ? "Login failed. Please try again."
                    : error);
            }
        }
    }

    #endregion
}

[Serializable]
public class LoginRequest
{
    public string user_id;
    public string username;
    public bool isGuest;
}
