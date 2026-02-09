using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WalletUIBinder : MonoBehaviour
{
    [SerializeField] private List<TMP_Text> coinTexts = new List<TMP_Text>();
    [SerializeField] private List<TMP_Text> diamondTexts = new List<TMP_Text>();
    [SerializeField] private List<TMP_Text> offlineStarTexts = new List<TMP_Text>();

    [SerializeField] private GameObject boardScreen2P;
    [SerializeField] private GameObject boardScreen4P;

    private PlayerWallet wallet;

    private void OnEnable()
    {
        if (boardScreen2P != null && boardScreen2P.activeSelf) boardScreen2P.SetActive(false);
        if (boardScreen4P != null && boardScreen4P.activeSelf) boardScreen4P.SetActive(false);

        wallet = PlayerWallet.Instance != null ? PlayerWallet.Instance : FindObjectOfType<PlayerWallet>();
        if (wallet != null)
        {
            wallet.CoinsChanged -= OnCoinsChanged;
            wallet.DiamondsChanged -= OnDiamondsChanged;
            wallet.OfflineStarsChanged -= OnOfflineStarsChanged;
            wallet.CoinsChanged += OnCoinsChanged;
            wallet.DiamondsChanged += OnDiamondsChanged;
            wallet.OfflineStarsChanged += OnOfflineStarsChanged;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (wallet != null)
        {
            wallet.CoinsChanged -= OnCoinsChanged;
            wallet.DiamondsChanged -= OnDiamondsChanged;
            wallet.OfflineStarsChanged -= OnOfflineStarsChanged;
        }
    }

    private void OnCoinsChanged(int value)
    {
        SetTexts(coinTexts, value);
    }

    private void OnDiamondsChanged(int value)
    {
        SetTexts(diamondTexts, value);
    }

    private void OnOfflineStarsChanged(int value)
    {
        SetTexts(offlineStarTexts, value);
    }

    private void RefreshAll()
    {
        int coins = wallet != null ? wallet.Coins : Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_COINS", 0));
        int diamonds = wallet != null ? wallet.Diamonds : Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_DIAMONDS", 0));
        int offlineStars = wallet != null ? wallet.OfflineStars : PlayerWallet.GetOfflineStarsForCurrentUser();

        SetTexts(coinTexts, coins);
        SetTexts(diamondTexts, diamonds);
        SetTexts(offlineStarTexts, offlineStars);
    }

    private void SetTexts(List<TMP_Text> texts, int value)
    {
        if (texts == null) return;
        string s = Mathf.Max(0, value).ToString();
        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            t.text = s;
        }
    }
}
