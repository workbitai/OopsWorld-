using System;
using System.Collections;
using FancyScrollView.Example09;
using NewGame.API;
using UnityEngine;

public class SplashAutoLogin : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private ScreenManager screenManager;
    [SerializeField] private GameObject loginScreen;
    [SerializeField] private GameObject homeOrLobbyScreen;

    [Header("Behavior")]
    [SerializeField] private bool autoLoginOnSplashComplete = true;

    [Header("UI (Optional)")]
    [SerializeField] private GameLoaderPanelAnimator gameLoaderPanel;
    [SerializeField] private string autoLoginBaseText = "Logging in";
    [SerializeField] private bool showAutoLoginErrorOnPanel = true;

    [Header("Retry (Optional)")]
    [SerializeField] private bool retryAutoLoginOnFailure = true;
    [SerializeField] private float retryIntervalSeconds = 2f;

    private bool awaitingLoginResponse;
    private bool loginSucceeded;
    private string loginError;
    private Coroutine autoLoginRoutine;

    [Serializable]
    private class LoginRequest
    {
        public string user_id;
        public string username;
        public bool isGuest;
    }

    public void HandleSplashComplete()
    {
        UIFillAndMove fillAndMove = GetComponent<UIFillAndMove>();
        if (fillAndMove != null)
        {
            fillAndMove.SetSkipDefaultNavigation(true);
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            UserSession.LoadFromPrefs();

            bool hasStoredUser =
                !string.IsNullOrEmpty(UserSession.UserId) &&
                !string.IsNullOrEmpty(UserSession.Username);

            if (hasStoredUser)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetPlayerName(UserSession.Username, saveToPrefs: true);
                    GameManager.Instance.SetPlayerAvatar(UserSession.AvatarIndex, null, saveToPrefs: true);
                }

                if (PlayerWallet.Instance != null)
                {
                    PlayerWallet.Instance.SetCoins(UserSession.Coins);
                    PlayerWallet.Instance.SetDiamonds(UserSession.Diamonds);
                }

                if (screenManager == null)
                {
                    screenManager = FindObjectOfType<ScreenManager>();
                }

                if (screenManager != null && homeOrLobbyScreen != null)
                {
                    screenManager.OpenScreen(homeOrLobbyScreen);
                    return;
                }
            }

            OpenLogin();
            return;
        }

        if (!autoLoginOnSplashComplete)
        {
            OpenLogin();
            return;
        }

        StartAutoLoginFlow();
    }

    public void TryAutoLogin()
    {
        StartAutoLoginFlow();
    }

    private void StartAutoLoginFlow()
    {
        if (autoLoginRoutine != null)
        {
            StopCoroutine(autoLoginRoutine);
            autoLoginRoutine = null;
        }

        autoLoginRoutine = StartCoroutine(AutoLoginFlowRoutine());
    }

    private IEnumerator AutoLoginFlowRoutine()
    {
        UserSession.LoadFromPrefs();

        bool hasStoredUser =
            !string.IsNullOrEmpty(UserSession.UserId) &&
            !string.IsNullOrEmpty(UserSession.Username);

        if (!hasStoredUser)
        {
            OpenLogin();
            autoLoginRoutine = null;
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            OpenLogin();
            autoLoginRoutine = null;
            yield break;
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (gameLoaderPanel == null)
        {
            gameLoaderPanel = FindObjectOfType<GameLoaderPanelAnimator>(true);
        }

        if (gameLoaderPanel != null)
        {
            gameLoaderPanel.gameObject.SetActive(true);
            gameLoaderPanel.ShowLoader(autoLoginBaseText);
        }

        while (true)
        {
            TryAutoLoginOnce();
            while (awaitingLoginResponse)
            {
                yield return null;
            }

            if (loginSucceeded)
            {
                autoLoginRoutine = null;
                yield break;
            }

            if (showAutoLoginErrorOnPanel && gameLoaderPanel != null)
            {
                string msg = string.IsNullOrWhiteSpace(loginError)
                    ? "Login failed. Please check internet and try again."
                    : loginError;
                gameLoaderPanel.ShowError(msg);
            }

            if (!retryAutoLoginOnFailure)
            {
                autoLoginRoutine = null;
                yield break;
            }

            while (Application.internetReachability == NetworkReachability.NotReachable)
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            float wait = Mathf.Max(0.1f, retryIntervalSeconds);
            yield return new WaitForSecondsRealtime(wait);

            if (gameLoaderPanel != null)
            {
                gameLoaderPanel.ShowLoader(autoLoginBaseText);
            }
        }
    }

    private void TryAutoLoginOnce()
    {
        awaitingLoginResponse = false;
        loginSucceeded = false;
        loginError = string.Empty;

        ApiManager api = ApiManager.Instance != null ? ApiManager.Instance : FindObjectOfType<ApiManager>();
        if (api == null)
        {
            OnLoginError("API not available");
            return;
        }

        LoginRequest request = new LoginRequest
        {
            user_id = UserSession.UserId,
            username = UserSession.Username,
            isGuest = UserSession.IsGuest
        };

        string json = JsonUtility.ToJson(request);
        awaitingLoginResponse = true;

        api.Post(
            api.GetLoginApiUrl(),
            json,
            OnLoginSuccess,
            OnLoginError
        );
    }

    private void OnLoginSuccess(string response)
    {
        awaitingLoginResponse = false;
        loginSucceeded = true;
        loginError = string.Empty;

        PlayerPrefs.SetString("LOGIN_TYPE", UserSession.IsGuest ? "GUEST" : "USER");
        PlayerPrefs.SetString("USER_DATA", response);
        PlayerPrefs.Save();

        UserSession.TryApplyLoginResponse(response);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerName(UserSession.Username, saveToPrefs: true);
            GameManager.Instance.SetPlayerAvatar(UserSession.AvatarIndex, null, saveToPrefs: true);
        }

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetCoins(UserSession.Coins);
            PlayerWallet.Instance.SetDiamonds(UserSession.Diamonds);
        }
        else
        {
            PlayerPrefs.SetInt("PLAYER_COINS", UserSession.Coins);
            PlayerPrefs.SetInt("PLAYER_DIAMONDS", UserSession.Diamonds);
            PlayerPrefs.Save();
        }

        if (screenManager != null && homeOrLobbyScreen != null)
        {
            if (gameLoaderPanel != null)
            {
                gameLoaderPanel.Hide();
            }
            screenManager.OpenScreen(homeOrLobbyScreen);
            return;
        }

        OpenLogin();
    }

    private void OnLoginError(string error)
    {
        awaitingLoginResponse = false;
        loginSucceeded = false;
        loginError = error;
    }

    private void OpenLogin()
    {
        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (screenManager != null && loginScreen != null)
        {
            screenManager.OpenScreen(loginScreen);
        }
        else if (loginScreen != null)
        {
            loginScreen.SetActive(true);
        }
    }
}
