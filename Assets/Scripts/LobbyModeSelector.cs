using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LobbyModeSelector : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button button2Player;
    [SerializeField] private Button button4Player;

    [Header("Images (Optional)")]
    [SerializeField] private Image image2Player;
    [SerializeField] private Image image4Player;

    [Header("Sprites")]
    [SerializeField] private Sprite sprite2PlayerSelected;
    [SerializeField] private Sprite sprite2PlayerDisabled;
    [SerializeField] private Sprite sprite4PlayerSelected;
    [SerializeField] private Sprite sprite4PlayerDisabled;

    [Header("Zoom")]
    [SerializeField] private float selectedScale = 1.12f;
    [SerializeField] private float unselectedScale = 1.0f;
    [SerializeField] private float zoomDuration = 0.16f;
    [SerializeField] private Ease zoomEase = Ease.OutBack;

    [Header("Default")]
    [SerializeField] private bool defaultTo2Player = true;

    [Header("Optional: Apply to GameManager")]
    [SerializeField] private bool applyToGameManager = true;
    [SerializeField] private GameManager gameManager;

    public int SelectedPlayerCount { get; private set; } = 0;

    public event Action<int> OnSelectionChanged;

    private RectTransform rt2;
    private RectTransform rt4;

    public void SetInteractable(bool interactable)
    {
        if (button2Player != null) button2Player.interactable = interactable;
        if (button4Player != null) button4Player.interactable = interactable;
    }

    private void Awake()
    {
        if (button2Player != null) button2Player.onClick.AddListener(Select2Player);
        if (button4Player != null) button4Player.onClick.AddListener(Select4Player);

        rt2 = button2Player != null ? button2Player.GetComponent<RectTransform>() : null;
        rt4 = button4Player != null ? button4Player.GetComponent<RectTransform>() : null;

        TryAutoAssignImages();

        if (applyToGameManager && gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
    }

    private void OnEnable()
    {
        // Always default to 2-player whenever the lobby panel opens.
        ApplySelection(2, animateScale: false);
    }

    private void OnDestroy()
    {
        if (button2Player != null) button2Player.onClick.RemoveListener(Select2Player);
        if (button4Player != null) button4Player.onClick.RemoveListener(Select4Player);
    }

    public void Select2Player()
    {
        ApplySelection(2, animateScale: true);
    }

    public void Select4Player()
    {
        ApplySelection(4, animateScale: true);
    }

    private void ApplySelection(int playerCount, bool animateScale)
    {
        playerCount = playerCount == 4 ? 4 : 2;
        SelectedPlayerCount = playerCount;

        bool is2Selected = playerCount == 2;

        if (image2Player != null)
        {
            image2Player.sprite = is2Selected ? sprite2PlayerSelected : sprite2PlayerDisabled;
        }

        if (image4Player != null)
        {
            image4Player.sprite = is2Selected ? sprite4PlayerDisabled : sprite4PlayerSelected;
        }

        if (animateScale)
        {
            AnimateScale(rt2, is2Selected);
            AnimateScale(rt4, !is2Selected);
        }
        else
        {
            SetScaleImmediate(rt2, is2Selected);
            SetScaleImmediate(rt4, !is2Selected);
        }

        if (applyToGameManager && gameManager != null)
        {
            if (playerCount == 2) gameManager.Select2Player();
            else gameManager.Select4Player();
        }

        OnSelectionChanged?.Invoke(playerCount);
    }

    private void SetScaleImmediate(RectTransform target, bool selected)
    {
        if (target == null) return;
        float s = selected ? selectedScale : unselectedScale;
        target.localScale = new Vector3(s, s, s);
    }

    private void AnimateScale(RectTransform target, bool selected)
    {
        if (target == null) return;

        float s = selected ? selectedScale : unselectedScale;
        target.DOKill();
        target.DOScale(s, zoomDuration).SetEase(zoomEase);
    }

    private void TryAutoAssignImages()
    {
        if (image2Player == null && button2Player != null)
        {
            image2Player = button2Player.targetGraphic as Image;
            if (image2Player == null)
            {
                image2Player = button2Player.GetComponent<Image>();
            }
            if (image2Player == null)
            {
                image2Player = button2Player.GetComponentInChildren<Image>(true);
            }
        }

        if (image4Player == null && button4Player != null)
        {
            image4Player = button4Player.targetGraphic as Image;
            if (image4Player == null)
            {
                image4Player = button4Player.GetComponent<Image>();
            }
            if (image4Player == null)
            {
                image4Player = button4Player.GetComponentInChildren<Image>(true);
            }
        }
    }
}
