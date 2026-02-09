using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LobbyModeLobbySwitcher : MonoBehaviour
{
    [Header("Selector")]
    [SerializeField] private LobbyModeSelector modeSelector;

    [Header("Roots")]
    [SerializeField] private GameObject lobbyRoot2P;
    [SerializeField] private GameObject lobbyRoot4P;

    [Header("Optional Carousels")]
    [SerializeField] private LobbyCarousel carousel2P;
    [SerializeField] private LobbyCarousel carousel4P;

    [Header("Lobby Root Scale")]
    [SerializeField] private float lobbyScale2P = 1f;
    [SerializeField] private float lobbyScale4P = 0.9f;

    [Header("Background (Optional)")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite backgroundSpriteVsBot;
    [SerializeField] private Sprite backgroundSpriteFriends;
    [SerializeField] private GameManager gameManager;

    [Header("Transition")]
    [SerializeField] private float transitionHalfDuration = 0.16f;
    [SerializeField] private float gapBetweenTransitions = 0.06f;
    [SerializeField] private Ease transitionEaseOut = Ease.InBack;
    [SerializeField] private Ease transitionEaseIn = Ease.OutBack;

    private bool isTransitioning = false;
    private bool? currentIs2p = null;
    private Sequence transitionSeq;

    private int? pendingPlayerCount = null;

    private void Awake()
    {
        if (modeSelector == null)
        {
            modeSelector = FindObjectOfType<LobbyModeSelector>();
        }

        if (carousel2P == null && lobbyRoot2P != null)
        {
            carousel2P = lobbyRoot2P.GetComponentInChildren<LobbyCarousel>(true);
        }

        if (carousel4P == null && lobbyRoot4P != null)
        {
            carousel4P = lobbyRoot4P.GetComponentInChildren<LobbyCarousel>(true);
        }
    }

    private void OnEnable()
    {
        if (modeSelector != null)
        {
            modeSelector.OnSelectionChanged += HandleSelectionChanged;
        }

        ApplyBackground();

        int selected = modeSelector != null && modeSelector.SelectedPlayerCount != 0 ? modeSelector.SelectedPlayerCount : 2;
        Apply(selected, animate: false);
    }

    private void OnDisable()
    {
        if (modeSelector != null)
        {
            modeSelector.OnSelectionChanged -= HandleSelectionChanged;
        }

        if (transitionSeq != null)
        {
            transitionSeq.Kill();
            transitionSeq = null;
        }

        isTransitioning = false;
        pendingPlayerCount = null;
        if (modeSelector != null) modeSelector.SetInteractable(true);
    }

    private void ApplyBackground()
    {
        if (backgroundImage == null) return;

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        bool vsBot = gameManager != null && gameManager.IsVsBotMode;

        Sprite target = vsBot ? backgroundSpriteVsBot : backgroundSpriteFriends;
        if (target != null)
        {
            backgroundImage.sprite = target;
        }
    }

    private void HandleSelectionChanged(int playerCount)
    {
        if (isTransitioning)
        {
            pendingPlayerCount = playerCount;
            return;
        }

        Apply(playerCount, animate: true);
    }

    private void Apply(int playerCount, bool animate)
    {
        bool use2p = playerCount != 4;

        float targetToScale = use2p ? lobbyScale2P : lobbyScale4P;
        float targetFromScale = use2p ? lobbyScale4P : lobbyScale2P;

        if (currentIs2p.HasValue && currentIs2p.Value == use2p)
        {
            return;
        }

        if (isTransitioning)
        {
            pendingPlayerCount = playerCount;
            return;
        }

        currentIs2p = use2p;

        if (!animate)
        {
            if (lobbyRoot2P != null)
            {
                lobbyRoot2P.SetActive(use2p);
                if (!use2p) SetChildrenActive(lobbyRoot2P, false);
            }
            if (lobbyRoot4P != null)
            {
                lobbyRoot4P.SetActive(!use2p);
                if (use2p) SetChildrenActive(lobbyRoot4P, false);
            }

            SetRootScale(lobbyRoot2P, lobbyScale2P);
            SetRootScale(lobbyRoot4P, lobbyScale4P);

            if (use2p)
            {
                if (carousel2P != null) carousel2P.Reinitialize(resetToFirst: true);
            }
            else
            {
                if (carousel4P != null) carousel4P.Reinitialize(resetToFirst: true);
            }
            return;
        }

        GameObject fromRoot = use2p ? lobbyRoot4P : lobbyRoot2P;
        GameObject toRoot = use2p ? lobbyRoot2P : lobbyRoot4P;

        if (transitionSeq != null)
        {
            transitionSeq.Kill();
            transitionSeq = null;
        }

        isTransitioning = true;
        if (modeSelector != null) modeSelector.SetInteractable(false);

        RectTransform fromRt = fromRoot != null ? fromRoot.GetComponent<RectTransform>() : null;
        RectTransform toRt = toRoot != null ? toRoot.GetComponent<RectTransform>() : null;

        CanvasGroup fromCg = EnsureCanvasGroup(fromRoot);
        CanvasGroup toCg = EnsureCanvasGroup(toRoot);

        if (fromCg != null) fromCg.alpha = 1f;
        if (fromRt != null) fromRt.localScale = Vector3.one * targetFromScale;

        if (toRoot != null) toRoot.SetActive(false);
        if (toCg != null) toCg.alpha = 1f;
        if (toRt != null) toRt.localScale = Vector3.one * targetToScale;

        transitionSeq = DOTween.Sequence();
        transitionSeq.SetAutoKill(true);

        if (fromCg != null) transitionSeq.Join(fromCg.DOFade(0f, transitionHalfDuration).SetEase(Ease.OutQuad));
        if (fromRt != null) transitionSeq.Join(fromRt.DOScale(0f, transitionHalfDuration).SetEase(transitionEaseOut));

        transitionSeq.AppendCallback(() =>
        {
            if (fromRoot != null)
            {
                SetChildrenActive(fromRoot, false);
                fromRoot.SetActive(false);
            }
            if (fromCg != null) fromCg.alpha = 1f;
            if (fromRt != null) fromRt.localScale = Vector3.one * targetFromScale;

            if (toRoot != null) toRoot.SetActive(true);
            if (toRoot != null) SetChildrenActive(toRoot, false);
            if (toCg != null) toCg.alpha = 0f;
            if (toRt != null) toRt.localScale = Vector3.zero;

            if (use2p)
            {
                if (carousel2P != null) carousel2P.Reinitialize(resetToFirst: true);
            }
            else
            {
                if (carousel4P != null) carousel4P.Reinitialize(resetToFirst: true);
            }
        });

        if (gapBetweenTransitions > 0f)
        {
            transitionSeq.AppendInterval(gapBetweenTransitions);
        }

        if (toCg != null) transitionSeq.Append(toCg.DOFade(1f, transitionHalfDuration).SetEase(Ease.OutQuad));
        if (toRt != null) transitionSeq.Join(toRt.DOScale(targetToScale, transitionHalfDuration).SetEase(transitionEaseIn));

        transitionSeq.OnComplete(() =>
        {
            isTransitioning = false;
            transitionSeq = null;

            if (modeSelector != null) modeSelector.SetInteractable(true);

            if (pendingPlayerCount.HasValue)
            {
                int pending = pendingPlayerCount.Value;
                pendingPlayerCount = null;
                Apply(pending, animate: true);
            }
        });
    }

    private CanvasGroup EnsureCanvasGroup(GameObject root)
    {
        if (root == null) return null;
        var cg = root.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = root.AddComponent<CanvasGroup>();
        }
        return cg;
    }

    private void SetRootScale(GameObject root, float scale)
    {
        if (root == null) return;

        Transform t = root.transform;
        if (t == null) return;

        t.localScale = Vector3.one * scale;
    }

    private void SetChildrenActive(GameObject root, bool active)
    {
        if (root == null) return;

        Transform t = root.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }
    }
}
