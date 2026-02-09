using System;
using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    private static PlayerWallet instance;

    public static PlayerWallet Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<PlayerWallet>();
            }
            return instance;
        }
    }

    private const string CoinsKey = "PLAYER_COINS";
    private const string DiamondsKey = "PLAYER_DIAMONDS";
    private const string NoAdsKey = "NO_ADS";
    public const string OfflineStarsKey = "PLAYER_OFFLINE_STARS";

    public static string GetOfflineStarsPrefsKey(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return OfflineStarsKey;
        return $"{OfflineStarsKey}_{userId}";
    }

    private static string ResolveOfflineStarsUserId()
    {
        UserSession.LoadFromPrefs();
        return UserSession.UserId;
    }

    public static bool EnsureOfflineStarsInitializedForCurrentUser(int seedAmount, out int value)
    {
        string userId = ResolveOfflineStarsUserId();
        string key = GetOfflineStarsPrefsKey(userId);

        if (PlayerPrefs.HasKey(key))
        {
            value = Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
            return false;
        }

        if (key != OfflineStarsKey && PlayerPrefs.HasKey(OfflineStarsKey))
        {
            value = Mathf.Max(0, PlayerPrefs.GetInt(OfflineStarsKey, 0));
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            return true;
        }

        value = Mathf.Max(0, seedAmount);
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        return true;
    }

    public static int GetOfflineStarsForCurrentUser()
    {
        string userId = ResolveOfflineStarsUserId();
        string key = GetOfflineStarsPrefsKey(userId);

        if (!PlayerPrefs.HasKey(key) && key != OfflineStarsKey && PlayerPrefs.HasKey(OfflineStarsKey))
        {
            int legacy = Mathf.Max(0, PlayerPrefs.GetInt(OfflineStarsKey, 0));
            PlayerPrefs.SetInt(key, legacy);
            PlayerPrefs.Save();
        }

        return Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
    }

    public static void SetOfflineStarsForCurrentUser(int amount)
    {
        string userId = ResolveOfflineStarsUserId();
        string key = GetOfflineStarsPrefsKey(userId);
        PlayerPrefs.SetInt(key, Mathf.Max(0, amount));
        PlayerPrefs.Save();
    }

    [Header("Config")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    public event Action<int> CoinsChanged;
    public event Action<int> DiamondsChanged;
    public event Action<int> OfflineStarsChanged;
    public event Action<bool> NoAdsChanged;

    public int Coins => Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));
    public int Diamonds => Mathf.Max(0, PlayerPrefs.GetInt(DiamondsKey, 0));
    public int OfflineStars => GetOfflineStarsForCurrentUser();
    public bool NoAds => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;

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

    public void AddCoins(int amount)
    {
        int add = Mathf.Max(0, amount);
        if (add <= 0) return;

        int next = Coins + add;
        PlayerPrefs.SetInt(CoinsKey, next);
        PlayerPrefs.Save();
        CoinsChanged?.Invoke(next);
    }

    public void SetCoins(int amount)
    {
        int next = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(CoinsKey, next);
        PlayerPrefs.Save();
        CoinsChanged?.Invoke(next);
    }

    public bool TrySpendCoins(int amount)
    {
        int spend = Mathf.Max(0, amount);
        if (spend <= 0) return true;

        int current = Coins;
        if (current < spend) return false;

        int next = current - spend;
        PlayerPrefs.SetInt(CoinsKey, next);
        PlayerPrefs.Save();
        CoinsChanged?.Invoke(next);
        return true;
    }

    public void AddDiamonds(int amount)
    {
        int add = Mathf.Max(0, amount);
        if (add <= 0) return;

        int next = Diamonds + add;
        PlayerPrefs.SetInt(DiamondsKey, next);
        PlayerPrefs.Save();
        DiamondsChanged?.Invoke(next);
    }

    public void AddOfflineStars(int amount)
    {
        int add = Mathf.Max(0, amount);
        if (add <= 0) return;

        int next = OfflineStars + add;
        PlayerPrefs.SetInt(GetOfflineStarsPrefsKey(ResolveOfflineStarsUserId()), next);
        PlayerPrefs.Save();
        OfflineStarsChanged?.Invoke(next);
    }

    public void SetOfflineStars(int amount)
    {
        int next = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(GetOfflineStarsPrefsKey(ResolveOfflineStarsUserId()), next);
        PlayerPrefs.Save();
        OfflineStarsChanged?.Invoke(next);
    }

    public void SetDiamonds(int amount)
    {
        int next = Mathf.Max(0, amount);
        PlayerPrefs.SetInt(DiamondsKey, next);
        PlayerPrefs.Save();
        DiamondsChanged?.Invoke(next);
    }

    public bool TrySpendDiamonds(int amount)
    {
        int spend = Mathf.Max(0, amount);
        if (spend <= 0) return true;

        int current = Diamonds;
        if (current < spend) return false;

        int next = current - spend;
        PlayerPrefs.SetInt(DiamondsKey, next);
        PlayerPrefs.Save();
        DiamondsChanged?.Invoke(next);
        return true;
    }

    public void SetNoAds(bool enabled)
    {
        PlayerPrefs.SetInt(NoAdsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        NoAdsChanged?.Invoke(enabled);
    }
}
