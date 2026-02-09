using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ConverterPopupController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int minConvertStars = 2000;
    [SerializeField] private int stepStars = 1000;
    [SerializeField] private int starsPerCoin = 10;

    [Header("UI")]
    [SerializeField] private TMP_Text balanceStarText;
    [SerializeField] private TMP_Text starAmountText;
    [SerializeField] private TMP_Text coinAmountText;

    [SerializeField] private Button[] plusButtons;
    [SerializeField] private Button[] minusButtons;
    [SerializeField] private Button collectButton;

    [Header("Collect Animation")]
    [SerializeField] private PopupHandler popupHandler;
    [SerializeField] private RectTransform coinPrefab;
    [SerializeField] private RectTransform coinFlyParent;
    [SerializeField] private RectTransform coinSpawnTransform;
    [SerializeField] private RectTransform coinTargetTransform;
    [SerializeField] private int maxFlyCoins = 20;
    [SerializeField] private float coinFlyDuration = 0.45f;
    [SerializeField] private Ease coinFlyEase = Ease.InOutQuad;
    [SerializeField] private float coinSpawnStagger = 0.03f;
    [SerializeField] private float coinStartScale = 0.2f;
    [SerializeField] private float coinEndScale = 1f;
    [SerializeField] private float coinScaleDuration = 0.18f;
    [SerializeField] private Ease coinScaleEase = Ease.OutBack;

    private int selectedStars;
    private bool isCollecting;

    private void OnEnable()
    {
        CacheRefsIfNeeded();
        RefreshOnOpen();
        HookButtons();
    }

    private void OnDisable()
    {
        UnhookButtons();
    }

    private void CacheRefsIfNeeded()
    {
        if (balanceStarText == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(balanceStarText)}", this);
        if (starAmountText == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(starAmountText)}", this);
        if (coinAmountText == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(coinAmountText)}", this);
        if (plusButtons == null || plusButtons.Length == 0) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(plusButtons)}", this);
        if (minusButtons == null || minusButtons.Length == 0) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(minusButtons)}", this);
        if (collectButton == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(collectButton)}", this);
        if (popupHandler == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(popupHandler)}", this);
        if (coinPrefab == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(coinPrefab)}", this);
        if (coinFlyParent == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(coinFlyParent)}", this);
        if (coinSpawnTransform == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(coinSpawnTransform)}", this);
        if (coinTargetTransform == null) Debug.LogError($"[{nameof(ConverterPopupController)}] Missing reference: {nameof(coinTargetTransform)}", this);
    }

    private void RefreshOnOpen()
    {
        int availableStars = GetAvailableStars();
        if (balanceStarText != null)
        {
            balanceStarText.text = FormatAvailableStarsText(availableStars);
        }

        if (availableStars < minConvertStars)
        {
            selectedStars = 0;
        }
        else
        {
            int defaultStars = minConvertStars;
            selectedStars = Mathf.Min(RoundDownToStep(defaultStars), RoundDownToStep(availableStars));
        }

        ApplySelectionToUI();
    }

    private void HookButtons()
    {
        if (plusButtons != null)
        {
            for (int i = 0; i < plusButtons.Length; i++)
            {
                Button b = plusButtons[i];
                if (b == null) continue;
                b.onClick.RemoveListener(OnPlusClicked);
                b.onClick.AddListener(OnPlusClicked);
            }
        }

        if (minusButtons != null)
        {
            for (int i = 0; i < minusButtons.Length; i++)
            {
                Button b = minusButtons[i];
                if (b == null) continue;
                b.onClick.RemoveListener(OnMinusClicked);
                b.onClick.AddListener(OnMinusClicked);
            }
        }

        if (collectButton != null)
        {
            collectButton.onClick.RemoveListener(OnCollectClicked);
            collectButton.onClick.AddListener(OnCollectClicked);
        }
    }

    private void UnhookButtons()
    {
        if (plusButtons != null)
        {
            for (int i = 0; i < plusButtons.Length; i++)
            {
                Button b = plusButtons[i];
                if (b == null) continue;
                b.onClick.RemoveListener(OnPlusClicked);
            }
        }

        if (minusButtons != null)
        {
            for (int i = 0; i < minusButtons.Length; i++)
            {
                Button b = minusButtons[i];
                if (b == null) continue;
                b.onClick.RemoveListener(OnMinusClicked);
            }
        }

        if (collectButton != null) collectButton.onClick.RemoveListener(OnCollectClicked);
    }

    private void OnPlusClicked()
    {
        int availableStars = GetAvailableStars();
        if (availableStars < minConvertStars)
        {
            selectedStars = 0;
            ApplySelectionToUI();
            return;
        }

        int maxSelectable = RoundDownToStep(availableStars);
        int next;

        if (selectedStars <= 0)
        {
            next = RoundDownToStep(Mathf.Min(minConvertStars, availableStars));
        }
        else
        {
            next = selectedStars + stepStars;
        }

        selectedStars = Mathf.Clamp(next, RoundDownToStep(minConvertStars), maxSelectable);
        ApplySelectionToUI();
    }

    private void OnMinusClicked()
    {
        if (selectedStars <= 0)
        {
            ApplySelectionToUI();
            return;
        }

        int next = selectedStars - stepStars;
        if (next < minConvertStars)
        {
            selectedStars = 0;
        }
        else
        {
            selectedStars = RoundDownToStep(next);
        }

        ApplySelectionToUI();
    }

    private void OnCollectClicked()
    {
        if (NoInternetStrip.BlockIfOffline())
        {
            return;
        }

        if (isCollecting) return;

        int availableStars = GetAvailableStars();
        if (selectedStars < minConvertStars)
        {
            return;
        }

        if (availableStars < selectedStars)
        {
            selectedStars = RoundDownToStep(availableStars);
            if (selectedStars < minConvertStars) selectedStars = 0;
            ApplySelectionToUI();
            return;
        }

        int coinsToCredit = ConvertStarsToCoins(selectedStars);
        if (coinsToCredit <= 0)
        {
            return;
        }

        isCollecting = true;
        if (collectButton != null) collectButton.interactable = false;

        int preDeductStars = availableStars;
        int deductedStars = selectedStars;

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetOfflineStars(Mathf.Max(0, preDeductStars - deductedStars));
        }
        else
        {
            int next = Mathf.Max(0, preDeductStars - deductedStars);
            PlayerWallet.SetOfflineStarsForCurrentUser(next);
        }

        GameWalletApi.CreditUpdateCoins(
            coinsToCredit,
            onSuccess: () =>
            {
                isCollecting = false;
                if (collectButton != null) collectButton.interactable = true;
                PlayCoinFlyAnimation(coinsToCredit);
                if (popupHandler != null) popupHandler.CloseCurrent();
            },
            onError: err =>
            {
                if (PlayerWallet.Instance != null)
                {
                    PlayerWallet.Instance.SetOfflineStars(preDeductStars);
                }
                else
                {
                    PlayerWallet.SetOfflineStarsForCurrentUser(preDeductStars);
                }

                isCollecting = false;
                if (collectButton != null) collectButton.interactable = true;
                RefreshOnOpen();
            },
            refreshWalletAfter: true
        );
    }

    private void ApplySelectionToUI()
    {
        int availableStars = GetAvailableStars();
        if (balanceStarText != null)
        {
            balanceStarText.text = FormatAvailableStarsText(availableStars);
        }

        int starsToShow = Mathf.Max(0, selectedStars);
        int coinsToShow = ConvertStarsToCoins(starsToShow);

        if (starAmountText != null)
        {
            starAmountText.text = starsToShow.ToString();
        }

        if (coinAmountText != null)
        {
            coinAmountText.text = coinsToShow.ToString();
        }

        bool canConvert = availableStars >= minConvertStars;
        bool hasSelection = starsToShow >= minConvertStars;

        if (plusButtons != null)
        {
            int maxSelectable = canConvert ? RoundDownToStep(availableStars) : 0;
            bool plusOn = canConvert && (starsToShow == 0 || starsToShow < maxSelectable);
            for (int i = 0; i < plusButtons.Length; i++)
            {
                Button b = plusButtons[i];
                if (b == null) continue;
                b.interactable = plusOn;
            }
        }

        if (minusButtons != null)
        {
            for (int i = 0; i < minusButtons.Length; i++)
            {
                Button b = minusButtons[i];
                if (b == null) continue;
                b.interactable = hasSelection;
            }
        }

        if (collectButton != null)
        {
            collectButton.interactable = hasSelection;
        }
    }

    private int ConvertStarsToCoins(int stars)
    {
        if (starsPerCoin <= 0) return 0;
        return Mathf.Max(0, stars) / starsPerCoin;
    }

    private int GetAvailableStars()
    {
        if (PlayerWallet.Instance != null)
        {
            return PlayerWallet.Instance.OfflineStars;
        }

        return PlayerWallet.GetOfflineStarsForCurrentUser();
    }

    private int RoundDownToStep(int value)
    {
        int v = Mathf.Max(0, value);
        int step = Mathf.Max(1, stepStars);
        return (v / step) * step;
    }

    private string FormatAvailableStarsText(int availableStars)
    {
        return $"Available Stars: {Mathf.Max(0, availableStars)}";
    }

    private void PlayCoinFlyAnimation(int coinsToCredit)
    {
        if (coinPrefab == null) return;
        if (coinFlyParent == null) return;
        if (coinSpawnTransform == null) return;
        if (coinTargetTransform == null) return;

        int count = Mathf.Clamp(coinsToCredit, 1, Mathf.Max(1, maxFlyCoins));
        Vector3 startWorld = coinSpawnTransform.position;
        Vector3 targetWorld = coinTargetTransform.position;

        for (int i = 0; i < count; i++)
        {
            float delay = i * Mathf.Max(0f, coinSpawnStagger);
            SpawnOneCoin(startWorld, targetWorld, delay);
        }
    }

    private void SpawnOneCoin(Vector3 startWorld, Vector3 targetWorld, float delay)
    {
        RectTransform coin = Instantiate(coinPrefab, coinFlyParent);
        coin.gameObject.SetActive(true);
        coin.SetAsLastSibling();

        coin.position = startWorld;
        coin.localScale = Vector3.one * Mathf.Max(0.01f, coinStartScale);
        coin.DOKill();

        float moveDur = Mathf.Max(0.01f, coinFlyDuration);
        float scaleDur = Mathf.Max(0.01f, coinScaleDuration);

        Sequence s = DOTween.Sequence();
        s.SetDelay(Mathf.Max(0f, delay));
        s.Append(coin.DOMove(targetWorld, moveDur).SetEase(coinFlyEase));
        s.Join(coin.DOScale(Vector3.one * Mathf.Max(0.01f, coinEndScale), scaleDur).SetEase(coinScaleEase));
        s.OnComplete(() =>
        {
            if (coin != null)
            {
                Destroy(coin.gameObject);
            }
        });
    }
}
