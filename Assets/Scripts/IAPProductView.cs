using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IAPProductView : MonoBehaviour
{
    [Header("IAP")]
    [SerializeField] private string productId;

    [Header("UI")]
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text coinRewardText;
    [SerializeField] private TMP_Text diamondRewardText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text priceText;

    public string ProductId => productId;

    private Coroutine waitForIapCoroutine;

    private static TMP_FontAsset rupeeFallbackFont;

    private void OnEnable()
    {
        EnsureRupeeFallbackIfNeeded();
        RefreshReward();
        RefreshPrice();
        Hook();

        if (waitForIapCoroutine != null)
        {
            StopCoroutine(waitForIapCoroutine);
            waitForIapCoroutine = null;
        }
        waitForIapCoroutine = StartCoroutine(WaitForIapAndSubscribe());
    }

    private void OnDisable()
    {
        Unhook();

        if (waitForIapCoroutine != null)
        {
            StopCoroutine(waitForIapCoroutine);
            waitForIapCoroutine = null;
        }

        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.PriceUpdated -= HandlePriceUpdated;
            IAPManager.Instance.Initialized -= HandleInitialized;
        }
    }

    private IEnumerator WaitForIapAndSubscribe()
    {
        float timeout = 2f;
        while (timeout > 0f && IAPManager.Instance == null)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!isActiveAndEnabled) yield break;
        if (IAPManager.Instance == null) yield break;

        IAPManager.Instance.PriceUpdated -= HandlePriceUpdated;
        IAPManager.Instance.PriceUpdated += HandlePriceUpdated;

        IAPManager.Instance.Initialized -= HandleInitialized;
        IAPManager.Instance.Initialized += HandleInitialized;

        RefreshReward();
        RefreshPrice();
    }

    private void Hook()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(HandleBuyClicked);
            buyButton.onClick.AddListener(HandleBuyClicked);
        }
    }

    private void Unhook()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(HandleBuyClicked);
        }
    }

    private void HandleBuyClicked()
    {
        if (string.IsNullOrWhiteSpace(productId)) return;
        if (IAPManager.Instance == null)
        {
            Debug.LogError($"IAPProductView: IAPManager.Instance is null. productId='{productId}'");
            return;
        }
        Debug.Log($"IAPProductView: Buy clicked productId='{productId}'");
        IAPManager.Instance.Buy(productId);
    }

    private void HandleInitialized()
    {
        RefreshReward();
        RefreshPrice();
    }

    private void HandlePriceUpdated(string id, string price)
    {
        if (!string.Equals(id, productId, System.StringComparison.Ordinal)) return;
        ApplyPrice(price);
    }

    public void SetProductId(string id)
    {
        productId = id;
        RefreshReward();
        RefreshPrice();
    }

    public void RefreshReward()
    {
        if (IAPManager.Instance != null)
        {
            if (coinRewardText != null || diamondRewardText != null)
            {
                if (IAPManager.Instance.TryGetRewardPayload(productId, out int coins, out int diamonds, out bool noAds))
                {
                    if (coinRewardText != null) coinRewardText.text = coins > 0 ? coins.ToString() : string.Empty;
                    if (diamondRewardText != null) diamondRewardText.text = diamonds > 0 ? diamonds.ToString() : string.Empty;

                    if (rewardText != null)
                    {
                        rewardText.text = noAds ? "NO ADS" : string.Empty;
                    }
                    return;
                }
            }

            if (rewardText != null)
            {
                string t = IAPManager.Instance.GetRewardDisplayText(productId);
                if (!string.IsNullOrEmpty(t))
                {
                    rewardText.text = t;
                    return;
                }
            }
        }

        if (rewardText != null)
        {
            rewardText.text = BuildRewardDisplayText(productId);
        }
    }

    public void RefreshPrice()
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            ApplyPrice(string.Empty);
            return;
        }

        if (IAPManager.Instance == null)
        {
            ApplyPrice(string.Empty);
            return;
        }

        string p = IAPManager.Instance.GetLocalizedPriceString(productId);
        ApplyPrice(p);
    }

    private void ApplyPrice(string price)
    {
        EnsureRupeeFallbackIfNeeded();
        string p = price ?? string.Empty;
        p = FormatPriceWithRupeePrefixIfNeeded(p);

        if (priceText != null)
        {
            priceText.text = p;
        }
    }

    private static string FormatPriceWithRupeePrefixIfNeeded(string price)
    {
        if (string.IsNullOrWhiteSpace(price)) return string.Empty;

        string t = price.Trim();
        if (t.IndexOf('\u20B9') >= 0) return t;
        if (t.StartsWith("Rs", System.StringComparison.OrdinalIgnoreCase)) return t;

        bool hasDigit = false;
        bool hasLetter = false;
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (char.IsDigit(c)) hasDigit = true;
            else if (char.IsLetter(c)) hasLetter = true;
        }

        if (hasDigit && !hasLetter)
        {
            return "\u20B9 " + t;
        }

        return t;
    }

    private void EnsureRupeeFallbackIfNeeded()
    {
        if (priceText == null) return;

        TMP_FontAsset font = priceText.font;
        if (font == null) return;

        const char rupee = '\u20B9';
        if (font.HasCharacter(rupee)) return;

        if (rupeeFallbackFont == null)
        {
            rupeeFallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        }

        if (rupeeFallbackFont == null) return;

        List<TMP_FontAsset> fallbacks = font.fallbackFontAssetTable;
        if (fallbacks == null)
        {
            fallbacks = new List<TMP_FontAsset>();
            font.fallbackFontAssetTable = fallbacks;
        }

        if (!fallbacks.Contains(rupeeFallbackFont))
        {
            fallbacks.Add(rupeeFallbackFont);
        }
    }

    private static string BuildRewardDisplayText(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;

        string s = id.Trim();
        if (string.Equals(s, "noads", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "no_ads", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "removeads", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "remove_ads", System.StringComparison.OrdinalIgnoreCase))
        {
            return "NO ADS";
        }

        int underscore = s.LastIndexOf('_');
        if (underscore <= 0 || underscore >= s.Length - 1)
        {
            return s;
        }

        string prefix = s.Substring(0, underscore);
        string amountStr = s.Substring(underscore + 1);

        if (!int.TryParse(amountStr, out int amount) || amount <= 0)
        {
            return s;
        }

        if (string.Equals(prefix, "coin", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "coins", System.StringComparison.OrdinalIgnoreCase))
        {
            return amount.ToString();
        }

        if (string.Equals(prefix, "diamond", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "diamonds", System.StringComparison.OrdinalIgnoreCase))
        {
            return amount.ToString();
        }

        return amount.ToString();
    }
}
