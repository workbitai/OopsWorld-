using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyItemView : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image boardImage;
    [SerializeField] private Image piecesImage;
    [SerializeField] private Image entryCurrencyImage;
    [SerializeField] private Sprite offlineStarSprite;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI winningCoinText;
    [SerializeField] private TextMeshProUGUI winningDiamondText;
    [SerializeField] private TextMeshProUGUI entryCoinText;

    [Header("Entry")]
    [SerializeField] private Button entryButton;

    [Header("Lock")]
    [SerializeField] private GameObject unlockedRoot;
    [SerializeField] private GameObject lockedRoot;

    private bool isLocked = false;

    private Sprite onlineCoinSprite;

    public string LobbyId = string.Empty;

    public event Action OnEntryClicked;

    private void Awake()
    {
        if (entryButton != null)
        {
            entryButton.onClick.AddListener(HandleEntryClicked);
        }

        if (onlineCoinSprite == null && entryCurrencyImage != null)
        {
            onlineCoinSprite = entryCurrencyImage.sprite;
        }

        ApplyLockState();
    }

    private void OnDestroy()
    {
        if (entryButton != null)
        {
            entryButton.onClick.RemoveListener(HandleEntryClicked);
        }
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        ApplyLockState();
    }

    public void SetBoardSprite(Sprite sprite)
    {
        if (boardImage != null && sprite != null)
        {
            boardImage.sprite = sprite;
        }
    }

    public void SetPiecesSprite(Sprite sprite)
    {
        if (piecesImage != null && sprite != null)
        {
            piecesImage.sprite = sprite;
        }
    }

    public void SetWinningCoin(long value)
    {
        if (winningCoinText != null)
        {
            winningCoinText.text = value.ToString();
        }
    }

    public void SetWinningDiamond(long value)
    {
        if (winningDiamondText != null)
        {
            winningDiamondText.text = value.ToString();
        }
    }

    public void SetEntryCoin(long value)
    {
        if (entryCoinText != null)
        {
            entryCoinText.text = value.ToString();
        }
    }

    public void SetLobbyId(string lobbyId)
    {
        LobbyId = lobbyId ?? string.Empty;
    }

    public void SetOfflineCurrency(bool offline)
    {
        if (entryCurrencyImage == null) return;

        if (offline)
        {
            if (offlineStarSprite != null) entryCurrencyImage.sprite = offlineStarSprite;
        }
        else
        {
            if (onlineCoinSprite != null) entryCurrencyImage.sprite = onlineCoinSprite;
        }
    }

    public void SetAll(
        Sprite boardSprite,
        Sprite piecesSprite,
        long winningCoin,
        long winningDiamond,
        long entryCoin,
        bool locked)
    {
        SetBoardSprite(boardSprite);
        SetPiecesSprite(piecesSprite);
        SetWinningCoin(winningCoin);
        SetWinningDiamond(winningDiamond);
        SetEntryCoin(entryCoin);
        SetLocked(locked);
    }

    private void ApplyLockState()
    {
        if (lockedRoot != null)
        {
            lockedRoot.SetActive(isLocked);
        }

        if (unlockedRoot != null)
        {
            unlockedRoot.SetActive(!isLocked);
        }

        if (entryButton != null)
        {
            entryButton.interactable = !isLocked;
        }
    }

    private void HandleEntryClicked()
    {
        if (isLocked)
        {
            return;
        }

        OnEntryClicked?.Invoke();
    }
}
