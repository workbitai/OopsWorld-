using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

public class IAPManager : MonoBehaviour
{
    private static IAPManager instance;

    public static IAPManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<IAPManager>();
            }
            return instance;
        }
    }

    [Header("Config")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool autoInitializeOnStart = true;

    [Header("Editor Testing")]
    [SerializeField, HideInInspector] private bool useFakeStoreInEditor = true;
    [SerializeField, HideInInspector] private bool useCatalogPriceAsFallback = true;

    [Header("Reward Overrides")]
    [SerializeField] private List<RewardOverride> rewardOverrides = new List<RewardOverride>();

    [Header("Testing / Fallback")]
    [SerializeField, HideInInspector] private bool useFallbackPricesWhenNotInitialized = true;
    [SerializeField, HideInInspector] private string fallbackPriceString = "\u20B9 0";

    public event Action Initialized;
    public event Action<string, string> PriceUpdated;
    public event Action<string> PurchaseSucceeded;
    public event Action<string, string> PurchaseFailed;

    private bool isInitializing;
    private bool isInitialized;

    public bool IsInitialized => isInitialized;

    private readonly Dictionary<string, string> cachedPriceStrings = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly Dictionary<string, string> catalogPriceStrings = new Dictionary<string, string>(StringComparer.Ordinal);

    [Serializable]
    private sealed class RewardOverride
    {
        public string productId;
        public int coins;
        public int diamonds;
        public bool noAds;
    }

#if UNITY_PURCHASING
    private IStoreController storeController;
    private IExtensionProvider extensionProvider;
#endif

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        EnsureRupeeGlyphSupport();
        if (!autoInitializeOnStart) return;
        InitializeIAP();
    }

    private static void EnsureRupeeGlyphSupport()
    {
        const char rupee = '\u20B9';
        const string rupeeStr = "\u20B9";

        // Fonts are under Assets/TextMesh Pro/Resources/Fonts & Materials/
        TMP_FontAsset[] fonts =
        {
            Resources.Load<TMP_FontAsset>("Fonts & Materials/Baloo2-ExtraBold SDF"),
            Resources.Load<TMP_FontAsset>("Fonts & Materials/Baloo2-Bold SDF"),
            Resources.Load<TMP_FontAsset>("Fonts & Materials/Baloo2-SemiBold SDF"),
            Resources.Load<TMP_FontAsset>("Fonts & Materials/Baloo2-Medium SDF"),
            Resources.Load<TMP_FontAsset>("Fonts & Materials/Baloo2-Regular SDF"),
            Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"),
            Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback")
        };

        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset f = fonts[i];
            if (f == null) continue;
            if (f.HasCharacter(rupee)) continue;
            f.TryAddCharacters(rupeeStr);
        }
    }

    private void CacheCatalogPricesIfPossible()
    {
        if (!useCatalogPriceAsFallback) return;
        catalogPriceStrings.Clear();

        // Unity IAP Catalog is stored as a JSON TextAsset at Resources/IAPProductCatalog.json.
        // In some Unity IAP versions, ProductCatalog APIs do not expose the numeric "googlePrice" reliably.
        // So we parse the JSON ourselves.
        TextAsset catalogJson = Resources.Load<TextAsset>("IAPProductCatalog");
        if (catalogJson == null || string.IsNullOrWhiteSpace(catalogJson.text)) return;

        CatalogRoot root;
        try
        {
            root = JsonUtility.FromJson<CatalogRoot>(catalogJson.text);
        }
        catch
        {
            return;
        }

        if (root == null || root.products == null) return;

        for (int i = 0; i < root.products.Length; i++)
        {
            CatalogProduct p = root.products[i];
            if (p == null) continue;
            if (string.IsNullOrWhiteSpace(p.id)) continue;

            double n = 0;
            if (p.googlePrice != null)
            {
                n = p.googlePrice.num;
            }

            if (n <= 0) continue;
            string price = "\u20B9 " + ((n % 1 == 0) ? ((long)n).ToString() : n.ToString("0.##"));
            catalogPriceStrings[p.id] = price;
        }

        foreach (var kvp in catalogPriceStrings)
        {
            PriceUpdated?.Invoke(kvp.Key, kvp.Value);
        }
    }

    [Serializable]
    private sealed class CatalogRoot
    {
        public CatalogProduct[] products;
    }

    [Serializable]
    private sealed class CatalogProduct
    {
        public string id;
        public CatalogGooglePrice googlePrice;
    }

    [Serializable]
    private sealed class CatalogGooglePrice
    {
        public double num;
    }

    public void InitializeIAP()
    {
        if (isInitialized) return;
        if (isInitializing) return;

        isInitializing = true;

        Debug.Log($"IAPManager.InitializeIAP: starting (UNITY_PURCHASING={(Application.isEditor ? "Editor" : "Runtime")})");

#if UNITY_PURCHASING
        CacheCatalogPricesIfPossible();

        var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
        if (useFakeStoreInEditor)
        {
            Debug.Log("IAPManager.InitializeIAP: FakeStore enabled (Editor)");
            module.useFakeStoreAlways = true;
            module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
        }
#endif
        var builder = ConfigurationBuilder.Instance(module);

        var catalog = ProductCatalog.LoadDefaultCatalog();
        if (catalog != null && catalog.allProducts != null)
        {
            foreach (var p in catalog.allProducts)
            {
                if (p == null) continue;
                if (string.IsNullOrWhiteSpace(p.id)) continue;
                builder.AddProduct(p.id, p.type);
            }
        }

        UnityPurchasing.Initialize(new StoreListener(this), builder);
#else
        Debug.LogError("IAPManager.InitializeIAP: UNITY_PURCHASING not enabled (install/enable Unity IAP package)");
        isInitializing = false;
        isInitialized = false;
#endif
    }

    public string GetLocalizedPriceString(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId)) return string.Empty;

        if (catalogPriceStrings.Count == 0)
        {
            CacheCatalogPricesIfPossible();
        }

        if (cachedPriceStrings.TryGetValue(productId, out string cached))
        {
            return cached;
        }

        if (catalogPriceStrings.TryGetValue(productId, out string cat))
        {
            return cat;
        }

        if (useFallbackPricesWhenNotInitialized)
        {
            return fallbackPriceString;
        }

        return string.Empty;
    }

    public string GetRewardDisplayText(string productId)
    {
        if (!TryGetRewardForProduct(productId, out RewardPayload reward)) return string.Empty;

        bool hasCoins = reward.coins > 0;
        bool hasDiamonds = reward.diamonds > 0;

        if (reward.noAds)
        {
            string t = "NO ADS";
            if (hasCoins) t += "\n" + reward.coins;
            if (hasDiamonds) t += "\n" + reward.diamonds;
            return t;
        }

        if (hasCoins && hasDiamonds)
        {
            return reward.coins + "\n" + reward.diamonds;
        }

        if (hasCoins) return reward.coins.ToString();
        if (hasDiamonds) return reward.diamonds.ToString();

        return string.Empty;
    }

    public bool TryGetRewardPayload(string productId, out int coins, out int diamonds, out bool noAds)
    {
        coins = 0;
        diamonds = 0;
        noAds = false;

        if (!TryGetRewardForProduct(productId, out RewardPayload reward)) return false;

        coins = reward.coins;
        diamonds = reward.diamonds;
        noAds = reward.noAds;
        return coins > 0 || diamonds > 0 || noAds;
    }

    public void Buy(string productId)
    {
        Debug.Log($"IAPManager.Buy: called productId='{productId}'");
        if (string.IsNullOrWhiteSpace(productId))
        {
            Debug.LogWarning("IAPManager.Buy: Invalid product id");
            PurchaseFailed?.Invoke(productId, "Invalid product id");
            return;
        }

#if UNITY_PURCHASING
        Debug.Log($"IAPManager.Buy: Attempting purchase productId='{productId}' initialized={isInitialized}");
        if (!isInitialized || storeController == null)
        {
            Debug.LogWarning($"IAPManager.Buy: IAP not initialized. productId='{productId}'");
            PurchaseFailed?.Invoke(productId, "IAP not initialized");
            return;
        }

        Product p = storeController.products.WithID(productId);
        if (p == null)
        {
            Debug.LogWarning($"IAPManager.Buy: Product not found in storeController. productId='{productId}'");
            PurchaseFailed?.Invoke(productId, "Product not found");
            return;
        }

        storeController.InitiatePurchase(p);
#else
        Debug.LogError($"IAPManager.Buy: UNITY_PURCHASING not enabled. productId='{productId}'");
        PurchaseFailed?.Invoke(productId, "UNITY_PURCHASING not enabled");
#endif
    }

    public void RestorePurchases()
    {
#if UNITY_PURCHASING
        if (!isInitialized || extensionProvider == null)
        {
            PurchaseFailed?.Invoke(string.Empty, "IAP not initialized");
            return;
        }

        var apple = extensionProvider.GetExtension<IAppleExtensions>();
        if (apple == null)
        {
            PurchaseFailed?.Invoke(string.Empty, "Apple extension not available");
            return;
        }

        apple.RestoreTransactions((result, message) =>
        {
            if (!result)
            {
                PurchaseFailed?.Invoke(string.Empty, string.IsNullOrEmpty(message) ? "Restore failed" : message);
            }
        });
#endif
    }

    private void CachePriceAndNotify()
    {
#if UNITY_PURCHASING
        if (storeController == null || storeController.products == null) return;

        var list = storeController.products.all;
        if (list == null) return;

        for (int i = 0; i < list.Length; i++)
        {
            var p = list[i];
            if (p == null) continue;
            if (p.metadata == null) continue;
            if (string.IsNullOrEmpty(p.definition?.id)) continue;

            string id = p.definition.id;
            string price = p.metadata.localizedPriceString ?? string.Empty;
            if (string.IsNullOrEmpty(price) && catalogPriceStrings.TryGetValue(id, out string fallbackFromCatalog))
            {
                price = fallbackFromCatalog;
            }

            cachedPriceStrings[id] = price;
            PriceUpdated?.Invoke(id, price);
        }
#endif
    }

    private bool TryGetRewardForProduct(string productId, out RewardPayload reward)
    {
        reward = default;
        if (string.IsNullOrWhiteSpace(productId)) return false;

        string id = productId.Trim();

        if (rewardOverrides != null)
        {
            for (int i = 0; i < rewardOverrides.Count; i++)
            {
                RewardOverride ov = rewardOverrides[i];
                if (ov == null) continue;
                if (string.IsNullOrWhiteSpace(ov.productId)) continue;
                if (!string.Equals(ov.productId.Trim(), id, StringComparison.Ordinal)) continue;

                reward = new RewardPayload
                {
                    coins = Mathf.Max(0, ov.coins),
                    diamonds = Mathf.Max(0, ov.diamonds),
                    noAds = ov.noAds
                };
                return reward.coins > 0 || reward.diamonds > 0 || reward.noAds;
            }
        }

        if (string.Equals(id, "noads", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "no_ads", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "removeads", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "remove_ads", StringComparison.OrdinalIgnoreCase))
        {
            reward = new RewardPayload { noAds = true };
            return true;
        }

        int underscore = id.LastIndexOf('_');
        if (underscore <= 0 || underscore >= id.Length - 1) return false;

        string prefix = id.Substring(0, underscore);
        string amountStr = id.Substring(underscore + 1);

        if (!int.TryParse(amountStr, out int amount)) return false;
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return false;

        if (string.Equals(prefix, "coin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "coins", StringComparison.OrdinalIgnoreCase))
        {
            reward = new RewardPayload { coins = amount };
            return true;
        }

        if (string.Equals(prefix, "diamond", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "diamonds", StringComparison.OrdinalIgnoreCase))
        {
            reward = new RewardPayload { diamonds = amount };
            return true;
        }

        return false;
    }

    private RewardPayload GrantReward(string productId)
    {
        if (!TryGetRewardForProduct(productId, out RewardPayload reward))
        {
            Debug.LogWarning($"IAPManager: Could not parse reward for productId='{productId}'.");
            return default;
        }

        PlayerWallet wallet = PlayerWallet.Instance;
        if (wallet == null)
        {
            Debug.LogWarning("IAPManager: PlayerWallet not found.");
            return default;
        }

        if (reward.coins > 0) wallet.AddCoins(reward.coins);
        if (reward.diamonds > 0) wallet.AddDiamonds(reward.diamonds);
        if (reward.noAds) wallet.SetNoAds(true);

        return reward;
    }

    private struct RewardPayload
    {
        public int coins;
        public int diamonds;
        public bool noAds;
    }

#if UNITY_PURCHASING
    private sealed class StoreListener : IStoreListener
    {
        private readonly IAPManager owner;

        public StoreListener(IAPManager owner)
        {
            this.owner = owner;
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            owner.isInitializing = false;
            owner.isInitialized = true;
            owner.storeController = controller;
            owner.extensionProvider = extensions;
            owner.CachePriceAndNotify();
            owner.Initialized?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            owner.isInitializing = false;
            owner.isInitialized = false;
            owner.PurchaseFailed?.Invoke(string.Empty, error.ToString());
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            owner.isInitializing = false;
            owner.isInitialized = false;
            owner.PurchaseFailed?.Invoke(string.Empty, string.IsNullOrEmpty(message) ? error.ToString() : message);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string id = args?.purchasedProduct?.definition?.id ?? string.Empty;
            if (!string.IsNullOrEmpty(id))
            {
                string price = args?.purchasedProduct?.metadata?.localizedPriceString;
                if (string.IsNullOrEmpty(price))
                {
                    price = owner.GetLocalizedPriceString(id);
                }

                RewardPayload granted = owner.GrantReward(id);
                Debug.Log($"IAP SUCCESS: productId='{id}' price='{price}' reward(coins={granted.coins}, diamonds={granted.diamonds}, noAds={granted.noAds})");
                owner.PurchaseSucceeded?.Invoke(id);
            }
            else
            {
                owner.PurchaseFailed?.Invoke(id, "Invalid purchased product");
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            string id = product != null ? product.definition.id : string.Empty;
            owner.PurchaseFailed?.Invoke(id, failureReason.ToString());
        }
    }
#endif
}
