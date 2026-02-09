using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LobbyCarousel : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private RectTransform viewport;

    [Header("Auto Pages (Optional)")]
    [SerializeField] private bool autoFromLobbyListManager = true;
    [SerializeField] private LobbyListManager lobbyListManager;

    [Header("Pages (Lobby Objects)")]
    [SerializeField] private List<RectTransform> pages = new List<RectTransform>();

    [Header("Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Animation")]
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;

    [Header("Settings")]
    [SerializeField] private bool loop = true;

    private int currentIndex = 0;
    private bool isAnimating = false;

    private void Awake()
    {
        if (leftButton != null) leftButton.onClick.AddListener(Prev);
        if (rightButton != null) rightButton.onClick.AddListener(Next);
    }

    private void OnDestroy()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(Prev);
        if (rightButton != null) rightButton.onClick.RemoveListener(Next);
    }

    private void OnDisable()
    {
        if (pages == null) return;
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] != null) pages[i].DOKill();
        }
        isAnimating = false;
    }

    private void OnEnable()
    {
        EnsureViewport();
        if (autoFromLobbyListManager)
        {
            AutoBuildPages();
        }
        SetupInitial();
    }

    public void Reinitialize(bool resetToFirst = true)
    {
        EnsureViewport();
        if (autoFromLobbyListManager)
        {
            AutoBuildPages();
        }

        if (resetToFirst)
        {
            currentIndex = 0;
        }

        SetupInitial();
    }

    private void AutoBuildPages()
    {
        lobbyListManager = GetComponent<LobbyListManager>();
        if (lobbyListManager == null)
        {
            lobbyListManager = GetComponentInParent<LobbyListManager>();
        }

        if (lobbyListManager == null) return;

        pages.Clear();
        var list = lobbyListManager.Lobbies;
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.view == null) continue;

            RectTransform rt = e.view.transform as RectTransform;
            if (rt != null)
            {
                pages.Add(rt);
            }
        }
    }

    private void EnsureViewport()
    {
        if (viewport != null) return;
        viewport = transform as RectTransform;
    }

    private float GetWidth()
    {
        if (viewport == null)
        {
            return 0f;
        }

        return viewport.rect.width;
    }

    private void SetupInitial()
    {
        if (pages == null || pages.Count == 0) return;

        currentIndex = Mathf.Clamp(currentIndex, 0, pages.Count - 1);

        for (int i = 0; i < pages.Count; i++)
        {
            RectTransform p = pages[i];
            if (p == null) continue;

            p.DOKill();

            bool active = i == currentIndex;
            p.gameObject.SetActive(active);
            p.anchoredPosition = Vector2.zero;
        }

        UpdateButtonsInteractable();
    }

    public void Next()
    {
        SlideTo(currentIndex + 1, direction: 1);
    }

    public void Prev()
    {
        SlideTo(currentIndex - 1, direction: -1);
    }

    private void SlideTo(int targetIndex, int direction)
    {
        if (isAnimating) return;
        if (pages == null || pages.Count == 0) return;
        if (pages.Count == 1) return;

        int newIndex = targetIndex;
        if (loop)
        {
            if (newIndex < 0) newIndex = pages.Count - 1;
            if (newIndex >= pages.Count) newIndex = 0;
        }
        else
        {
            newIndex = Mathf.Clamp(newIndex, 0, pages.Count - 1);
        }

        if (newIndex == currentIndex) return;

        RectTransform current = pages[currentIndex];
        RectTransform next = pages[newIndex];
        if (current == null || next == null) return;

        float w = GetWidth();
        if (w <= 0f)
        {
            var canvas = GetComponentInParent<Canvas>();
            w = canvas != null ? canvas.pixelRect.width : 1080f;
        }

        isAnimating = true;
        UpdateButtonsInteractable();

        current.DOKill();
        next.DOKill();

        next.gameObject.SetActive(true);
        next.anchoredPosition = new Vector2(direction > 0 ? w : -w, 0f);

        Sequence seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        seq.Join(current.DOAnchorPos(new Vector2(direction > 0 ? -w : w, 0f), slideDuration).SetEase(slideEase));
        seq.Join(next.DOAnchorPos(Vector2.zero, slideDuration).SetEase(slideEase));

        seq.OnComplete(() =>
        {
            if (current != null)
            {
                current.gameObject.SetActive(false);
                current.anchoredPosition = Vector2.zero;
            }

            currentIndex = newIndex;
            isAnimating = false;
            UpdateButtonsInteractable();
        });
    }

    private void UpdateButtonsInteractable()
    {
        if (leftButton != null)
        {
            leftButton.interactable = !isAnimating && (loop || currentIndex > 0);
        }

        if (rightButton != null)
        {
            rightButton.interactable = !isAnimating && (loop || currentIndex < pages.Count - 1);
        }
    }
}
