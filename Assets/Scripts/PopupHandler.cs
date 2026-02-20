using System;
using System.Collections.Generic;
using UnityEngine;
using FancyScrollView.Example09;

public class PopupHandler : MonoBehaviour
{
    [Header("Optional background overlay")]
    [SerializeField] private GameObject background;

    [Header("Optional controls")]
    [SerializeField] private GameObject backButton;

    [Header("Win Popup (Optional)")]
    [SerializeField] private GameObject winPopup;

    [Header("Pause Popup (Optional)")]
    [SerializeField] private GameObject gamePausePopup;

    [Header("Canvas Priority (Optional)")]
    [SerializeField] private bool forceTopMostCanvas = true;
    [SerializeField] private int forcedSortingOrder = 5000;

    [Header("Popups under this handler")]
    [SerializeField] private List<GameObject> popups = new List<GameObject>();

    [Header("Behavior")]
    [SerializeField] private bool closeOnStart = true;

    private GameObject current;
    private bool isTransitioning;
    private Canvas cachedCanvas;

    private bool backButtonWasActive;

    private readonly Stack<GameObject> history = new Stack<GameObject>();

    private bool IsPausePopup(GameObject popup)
    {
        if (popup == null) return false;
        if (gamePausePopup != null) return popup == gamePausePopup;
        string n = popup.name;
        if (string.IsNullOrEmpty(n)) return false;
        n = n.ToLowerInvariant();
        return n.Contains("pause");
    }

    private bool IsInAppPurchasePopup(GameObject popup)
    {
        if (popup == null) return false;
        string n = popup.name;
        if (string.IsNullOrEmpty(n)) return false;
        n = n.ToLowerInvariant();
        return n.Contains("inapp") || n.Contains("iap") || (n.Contains("purchase") && n.Contains("panel"));
    }

    private void Awake()
    {
        cachedCanvas = GetComponent<Canvas>();
        CloseAllInstant();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        CloseAllInstant();
    }

    private void EnsureTopMost()
    {
        transform.SetAsLastSibling();

        if (!forceTopMostCanvas) return;
        if (cachedCanvas == null) return;

        cachedCanvas.overrideSorting = true;
        cachedCanvas.sortingOrder = forcedSortingOrder;
    }

    public void ShowWinPopup(int winningPlayer)
    {
        if (winPopup == null)
        {
            Debug.LogWarning("PopupHandler: winPopup is not assigned.");
            return;
        }

        GameManager gm = FindObjectOfType<GameManager>();
        WinLossScreenController controller = winPopup.GetComponentInChildren<WinLossScreenController>(true);
        if (controller != null && gm != null)
        {
            if (winningPlayer == gm.LocalPlayerNumber)
            {
                DailyTaskPrefs.AddProgress(DailyTaskPrefs.TaskId.Win3Times, 1, 3);
            }

            long winCoin = gm.SelectedLobbyWinningCoin;
            long winDiamond = gm.SelectedLobbyWinningDiamond;
            if (gm.CurrentMatchUsesOfflineStarRewards)
            {
                winCoin = gm.SelectedLobbyWinningOfflineStar;
                winDiamond = 0;
            }

            controller.ShowResult(
                winningPlayer,
                gm.LocalPlayerNumber,
                winCoin,
                winDiamond,
                gm.PlayerAvatarSprite
            );

            gm.DisconnectOopsSocketAfterMatchEndDelayed(1.0f);
        }

        SetBackButtonActive(false);
        OpenPopup(winPopup);
    }

    private void SetBackButtonActive(bool active)
    {
        if (backButton == null) return;
        backButton.SetActive(active);
    }

    private void SetPauseState(bool paused)
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null) return;
        gm.SetPausePopupOpen(paused);
    }

    private bool RequiresInternetForPopup(GameObject popup)
    {
        if (popup == null) return false;
        string n = popup.name;
        if (string.IsNullOrEmpty(n)) return false;
        n = n.ToLowerInvariant();
        return n.Contains("ranking") || n.Contains("cosmetic") || n.Contains("dailytask") || (n.Contains("daily") && n.Contains("task")) || n.Contains("noads") || (n.Contains("no") && n.Contains("ads")) || n.Contains("shop") || n.Contains("store") || n.Contains("purchase") || n.Contains("iap") || n.Contains("inapp") || n.Contains("coin") || n.Contains("coins") || n.Contains("diamond") || n.Contains("diamonds");
    }

    public void OpenPopup(GameObject popup)
    {
        OpenPopupInternal(popup, pushHistory: true);
    }

    private void OpenPopupInternal(GameObject popup, bool pushHistory)
    {
        if (popup == null) return;
        if (isTransitioning) return;

        if (Application.internetReachability == NetworkReachability.NotReachable && RequiresInternetForPopup(popup))
        {
            NoInternetStrip.BlockIfOffline();
            return;
        }

        EnsureTopMost();

        if (background != null)
        {
            background.SetActive(true);
        }

        if (current == popup && popup.activeSelf)
        {
            RefreshBackground();
            return;
        }

        isTransitioning = true;

        if (backButton != null)
        {
            backButtonWasActive = backButton.activeSelf;
        }

        GameObject previousForHistory = current;
        if (pushHistory && previousForHistory == null)
        {
            previousForHistory = InferActivePopup(exclude: popup);
        }

        if (pushHistory && previousForHistory != null && previousForHistory != popup && previousForHistory != winPopup && !IsPausePopup(previousForHistory))
        {
            history.Push(previousForHistory);
        }

        EnsureInList(popup);

        for (int i = 0; i < popups.Count; i++)
        {
            var p = popups[i];
            if (p == null) continue;
            if (p == popup) continue;

            p.SetActive(false);
        }

        current = popup;
        popup.SetActive(true);

        if (popup == winPopup || IsPausePopup(popup))
        {
            SetBackButtonActive(false);
        }

        if (IsPausePopup(popup))
        {
            SetPauseState(true);
        }

        var anim = popup.GetComponent<CommonPopupAnimator>();
        if (anim != null)
        {
            anim.Open();
        }

        RefreshBackground();
        isTransitioning = false;
    }

    private GameObject InferActivePopup(GameObject exclude)
    {
        GameObject candidate = null;

        if (popups != null)
        {
            for (int i = 0; i < popups.Count; i++)
            {
                GameObject p = popups[i];
                if (p == null) continue;
                if (p == exclude) continue;
                if (p == background) continue;
                if (p == backButton) continue;
                if (!p.activeSelf) continue;

                candidate = p;
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c == null) continue;
            GameObject go = c.gameObject;
            if (go == null) continue;
            if (go == exclude) continue;
            if (go == background) continue;
            if (go == backButton) continue;
            if (!go.activeSelf) continue;

            candidate = go;
        }

        return candidate;
    }

    public void CloseCurrent()
    {
        CloseCurrent(null);
    }

    public void ClosePopup(GameObject popup)
    {
        if (popup == null) return;
        if (isTransitioning) return;

        if (current == popup)
        {
            CloseCurrent();
            return;
        }

        var anim = popup.GetComponent<CommonPopupAnimator>();
        if (anim != null)
        {
            anim.Close(() =>
            {
                if (IsPausePopup(popup))
                {
                    SetPauseState(false);
                }

                popup.SetActive(false);
                RefreshBackground();

                if (popup == winPopup || IsPausePopup(popup))
                {
                    SetBackButtonActive(backButtonWasActive);
                }
            });
            return;
        }

        if (IsPausePopup(popup))
        {
            SetPauseState(false);
        }

        popup.SetActive(false);
        RefreshBackground();

        if (popup == winPopup || IsPausePopup(popup))
        {
            SetBackButtonActive(backButtonWasActive);
        }
    }

    public void CloseAllInstant()
    {
        current = null;
        history.Clear();

        for (int i = 0; i < popups.Count; i++)
        {
            var p = popups[i];
            if (p == null) continue;

            p.SetActive(false);
        }

        if (gamePausePopup != null || (popups != null && popups.Count > 0))
        {
            SetPauseState(false);
        }

        // PanelHandler open/enable thy tyare backButton default ON rehvu joiye.
        SetBackButtonActive(true);
        backButtonWasActive = true;

        RefreshBackground();
    }

    private void CloseCurrent(Action onClosed)
    {
        if (current == null)
        {
            onClosed?.Invoke();
            return;
        }

        var closing = current;
        current = null;

        bool shouldOpenHomeAfterClose = IsInAppPurchasePopup(closing);
        bool shouldReturnToHistory = !shouldOpenHomeAfterClose && closing != winPopup && !IsPausePopup(closing);

        var anim = closing.GetComponent<CommonPopupAnimator>();
        if (anim != null)
        {
            anim.Close(() =>
            {
                if (IsPausePopup(closing))
                {
                    SetPauseState(false);
                }

                closing.SetActive(false);
                RefreshBackground();

                if (closing == winPopup || IsPausePopup(closing))
                {
                    SetBackButtonActive(backButtonWasActive);
                }

                if (shouldReturnToHistory)
                {
                    ReturnToPreviousPopupOrFallback();
                }
                else if (shouldOpenHomeAfterClose)
                {
                    history.Clear();
                    OpenHomePanelFallback();
                }
                onClosed?.Invoke();
            });
            return;
        }

        if (IsPausePopup(closing))
        {
            SetPauseState(false);
        }

        closing.SetActive(false);
        RefreshBackground();

        if (closing == winPopup || IsPausePopup(closing))
        {
            SetBackButtonActive(backButtonWasActive);
        }
        if (shouldReturnToHistory)
        {
            ReturnToPreviousPopupOrFallback();
        }
        else if (shouldOpenHomeAfterClose)
        {
            history.Clear();
            OpenHomePanelFallback();
        }
        onClosed?.Invoke();
    }

    private void OpenHomePanelFallback()
    {
        ScreenManager sm = FindObjectOfType<ScreenManager>();
        if (sm == null) return;

        sm.OpenScreenByName("HomePanel");
        if (sm.CurrentActiveScreen == null)
        {
            sm.OpenScreenByName("LobbyPanel");
        }
    }

    private void ReturnToPreviousPopupOrFallback()
    {
        while (history.Count > 0)
        {
            GameObject prev = history.Pop();
            if (prev == null) continue;
            if (!popups.Contains(prev))
            {
                EnsureInList(prev);
            }
            OpenPopupInternal(prev, pushHistory: false);
            return;
        }

        ScreenManager sm = FindObjectOfType<ScreenManager>();
        if (sm != null && (sm.CurrentActiveScreen == null || !sm.CurrentActiveScreen.activeInHierarchy))
        {
            sm.OpenScreenByName("HomePanel");
            if (sm.CurrentActiveScreen == null)
            {
                sm.OpenScreenByName("LobbyPanel");
            }
        }
    }

    private void RefreshBackground()
    {
        if (background == null) return;

        bool anyOpen = false;
        for (int i = 0; i < popups.Count; i++)
        {
            if (popups[i] != null && popups[i].activeSelf)
            {
                anyOpen = true;
                break;
            }
        }

        background.SetActive(anyOpen);
    }

    private void EnsureInList(GameObject popup)
    {
        if (popups == null) popups = new List<GameObject>();
        if (!popups.Contains(popup)) popups.Add(popup);
    }
}
