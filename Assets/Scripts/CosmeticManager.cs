using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using My.UI;

public class CosmeticManager : MonoBehaviour
{
    private enum CosmeticTab
    {
        Gold,
        Prime
    }

    [Serializable]
    private class AmountTextBinding
    {
        public float amount;
        public TMP_Text text;
    }

    [Serializable]
    private class BuyEffectBinding
    {
        public Button buyButton;
        public Transform particleParent;
        public GameObject particlePrefab;
        public Image imageToFade;
        public Image tickImage;
        public Sprite purchasedSprite;
        public GameObject purchasePieceObject;
    }

    [Header("Boxes")]
    [SerializeField] private RectTransform goldBox;
    [SerializeField] private RectTransform primeBox;

    [Header("Buttons")]
    [SerializeField] private Button goldButton;
    [SerializeField] private Button primeButton;

    [Header("Default")]
    [SerializeField] private bool defaultShowGold = true;

    [Header("Amounts")]
    [SerializeField] private List<AmountTextBinding> goldAmounts = new List<AmountTextBinding>();
    [SerializeField] private List<AmountTextBinding> primeAmounts = new List<AmountTextBinding>();
    [SerializeField] private string amountFormat = "{0:0.##}";

    [Header("Animation")]
    [SerializeField] private float transitionDuration = 0.28f;
    [SerializeField] private Ease transitionEase = Ease.OutCubic;
    [SerializeField] private float offscreenPadding = 120f;

    [Header("Buy Effect")]
    [SerializeField] private List<BuyEffectBinding> buyEffects = new List<BuyEffectBinding>();

    [Header("Tick Sprites (Common)")]
    [SerializeField] private Sprite defaultTickSelectedSprite;
    [SerializeField] private Sprite defaultTickPurchasedSprite;

    [Header("Purchase Complete")]
    [SerializeField] private GameObject purchasePanel;
    [SerializeField] private Image purchasePieceImage;
    [SerializeField] private GameObject purchaseParticleObject;
    [SerializeField] private List<GameObject> purchasePieceObjects = new List<GameObject>();

    private CosmeticTab currentTab;
    private bool isTransitioning;

    private Vector2 goldOnPos;
    private Vector2 primeOnPos;

    private Tween goldTween;
    private Tween primeTween;

    private Tween purchaseMoveTween;
    private Vector3 purchaseBaseLocalPos;
    private bool purchaseBaseCached;

    private const float PurchaseEnterFromBottomOffset = 1200f;
    private const float PurchaseEnterDuration = 0.45f;

    private const string CosmeticPurchasedKeyPrefix = "COSMETIC_PURCHASED_";
    private const string CosmeticSelectedKey = "COSMETIC_SELECTED";

    private Button purchasePanelCloseButton;
    private UnityAction purchasePanelCloseAction;

    private readonly List<UnityAction> buyEffectActions = new List<UnityAction>();
    private readonly List<UnityAction> tickEffectActions = new List<UnityAction>();

    private int selectedCosmeticIndex;

    private void OnValidate()
    {
        ApplyAmounts();
    }

    private void Awake()
    {
        if (goldBox != null) goldOnPos = goldBox.anchoredPosition;
        if (primeBox != null) primeOnPos = primeBox.anchoredPosition;

        currentTab = defaultShowGold ? CosmeticTab.Gold : CosmeticTab.Prime;
        ApplyImmediate(currentTab);

        ApplyAmounts();

        selectedCosmeticIndex = GetSelectedIndexOrDefault();
        ApplyPurchasedStates();
    }

    private void OnEnable()
    {
        if (goldButton != null) goldButton.onClick.AddListener(SelectGold);
        if (primeButton != null) primeButton.onClick.AddListener(SelectPrime);

        RegisterBuyEffects();
        RegisterTickEffects();
        RegisterPurchasePanelClose();

        ApplyImmediate(currentTab);
        ApplyAmounts();

        selectedCosmeticIndex = GetSelectedIndexOrDefault();
        ApplyPurchasedStates();
    }

    private void OnDisable()
    {
        if (goldButton != null) goldButton.onClick.RemoveListener(SelectGold);
        if (primeButton != null) primeButton.onClick.RemoveListener(SelectPrime);

        UnregisterBuyEffects();
        UnregisterTickEffects();
        UnregisterPurchasePanelClose();

        StopPurchasePanelAnimation();
        DisableAllPurchasePiecesIncludingBindings();

        KillTweens();
        isTransitioning = false;
    }

    private int GetSelectedIndexOrDefault()
    {
        int idx = PlayerPrefs.GetInt(CosmeticSelectedKey, 0);
        if (idx < 0) idx = 0;
        if (buyEffects != null && buyEffects.Count > 0)
        {
            idx = Mathf.Clamp(idx, 0, buyEffects.Count - 1);
        }
        if (!IsPurchased(idx)) idx = 0;
        return idx;
    }

    private void SetSelectedIndex(int index)
    {
        int resolved = Mathf.Max(0, index);
        if (buyEffects != null && buyEffects.Count > 0)
        {
            resolved = Mathf.Clamp(resolved, 0, buyEffects.Count - 1);
        }
        if (!IsPurchased(resolved)) resolved = 0;
        selectedCosmeticIndex = resolved;
        PlayerPrefs.SetInt(CosmeticSelectedKey, selectedCosmeticIndex);
        PlayerPrefs.Save();
        ApplyPurchasedStates();
    }

    private void DisableAllPurchasePieces()
    {
        if (purchasePieceObjects == null || purchasePieceObjects.Count == 0) return;

        for (int i = 0; i < purchasePieceObjects.Count; i++)
        {
            GameObject go = purchasePieceObjects[i];
            if (go == null) continue;
            go.SetActive(false);
        }
    }

    private void DisableAllPurchasePiecesIncludingBindings()
    {
        DisableAllPurchasePieces();

        if (buyEffects == null || buyEffects.Count == 0) return;

        for (int i = 0; i < buyEffects.Count; i++)
        {
            BuyEffectBinding b = buyEffects[i];
            if (b == null || b.purchasePieceObject == null) continue;
            b.purchasePieceObject.SetActive(false);
        }
    }

    private void EnablePurchasePiece(int index)
    {
        DisableAllPurchasePiecesIncludingBindings();

        if (purchasePieceObjects == null || index < 0 || index >= purchasePieceObjects.Count) return;

        GameObject go = purchasePieceObjects[index];
        if (go == null) return;

        go.SetActive(true);
    }

    private void EnablePurchasePiece(BuyEffectBinding binding, int fallbackIndex)
    {
        DisableAllPurchasePiecesIncludingBindings();

        GameObject go = null;
        if (binding != null && binding.purchasePieceObject != null)
        {
            go = binding.purchasePieceObject;
        }
        else if (purchasePieceObjects != null && fallbackIndex >= 0 && fallbackIndex < purchasePieceObjects.Count)
        {
            go = purchasePieceObjects[fallbackIndex];
        }

        if (go == null) return;

        go.SetActive(true);
    }

    private void RegisterPurchasePanelClose()
    {
        UnregisterPurchasePanelClose();

        if (purchasePanel == null) return;

        purchasePanelCloseButton = purchasePanel.GetComponent<Button>();
        if (purchasePanelCloseButton == null) return;

        purchasePanelCloseAction = ClosePurchasePanel;
        purchasePanelCloseButton.onClick.AddListener(purchasePanelCloseAction);
    }

    private void UnregisterPurchasePanelClose()
    {
        if (purchasePanelCloseButton != null && purchasePanelCloseAction != null)
        {
            purchasePanelCloseButton.onClick.RemoveListener(purchasePanelCloseAction);
        }

        purchasePanelCloseButton = null;
        purchasePanelCloseAction = null;
    }

    private void ClosePurchasePanel()
    {
        StopPurchasePanelAnimation();
        DisableAllPurchasePiecesIncludingBindings();

        if (purchaseParticleObject != null)
        {
            purchaseParticleObject.SetActive(false);
        }

        if (purchasePanel != null)
        {
            purchasePanel.SetActive(false);
        }
    }

    private bool IsPurchased(int index)
    {
        if (index == 0) return true;
        return PlayerPrefs.GetInt(CosmeticPurchasedKeyPrefix + index, 0) == 1;
    }

    private void SetPurchased(int index, bool purchased)
    {
        if (index == 0 && !purchased) return;
        PlayerPrefs.SetInt(CosmeticPurchasedKeyPrefix + index, purchased ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyPurchasedStates()
    {
        if (buyEffects == null) return;

        for (int i = 0; i < buyEffects.Count; i++)
        {
            ApplyPurchasedStateForIndex(i);
        }
    }

    private void ApplyPurchasedStateForIndex(int index)
    {
        if (buyEffects == null || index < 0 || index >= buyEffects.Count) return;

        BuyEffectBinding b = buyEffects[index];
        if (b == null) return;

        bool purchased = IsPurchased(index);
        bool selected = index == selectedCosmeticIndex;

        if (b.buyButton != null)
        {
            b.buyButton.interactable = !purchased;
            b.buyButton.gameObject.SetActive(!purchased);
        }

        if (b.tickImage != null)
        {
            b.tickImage.gameObject.SetActive(purchased);
            if (purchased)
            {
                Sprite purchasedSprite = defaultTickPurchasedSprite;
                Sprite selectedSprite = defaultTickSelectedSprite;

                Sprite s = selected ? (selectedSprite != null ? selectedSprite : purchasedSprite) : purchasedSprite;

                if (s != null)
                {
                    b.tickImage.sprite = s;
                    b.tickImage.enabled = true;
                }
            }
        }

        if (b.imageToFade != null)
        {
            b.imageToFade.DOKill();
            b.imageToFade.gameObject.SetActive(!purchased);
        }
    }

    private void RegisterBuyEffects()
    {
        UnregisterBuyEffects();

        if (buyEffects == null || buyEffects.Count == 0) return;

        for (int i = 0; i < buyEffects.Count; i++)
        {
            int index = i;
            BuyEffectBinding b = buyEffects[index];
            if (b == null || b.buyButton == null)
            {
                buyEffectActions.Add(null);
                continue;
            }

            UnityAction a = () => PlayBuyEffect(index);
            buyEffectActions.Add(a);
            b.buyButton.onClick.AddListener(a);
        }
    }

    private void RegisterTickEffects()
    {
        UnregisterTickEffects();

        if (buyEffects == null || buyEffects.Count == 0) return;

        for (int i = 0; i < buyEffects.Count; i++)
        {
            int index = i;
            BuyEffectBinding b = buyEffects[index];

            Button btn = EnsureTickButton(b);

            if (btn == null)
            {
                tickEffectActions.Add(null);
                continue;
            }

            UnityAction a = () => SelectPurchasedCosmetic(index);
            tickEffectActions.Add(a);
            btn.onClick.AddListener(a);
        }
    }

    private Button EnsureTickButton(BuyEffectBinding b)
    {
        if (b == null) return null;

        if (b.tickImage == null)
        {
            return null;
        }

        b.tickImage.raycastTarget = true;

        Button btn = b.tickImage.GetComponent<Button>();
        if (btn == null)
        {
            btn = b.tickImage.gameObject.AddComponent<Button>();
        }
        return btn;
    }

    private void UnregisterBuyEffects()
    {
        if (buyEffects != null && buyEffectActions.Count > 0)
        {
            int count = Mathf.Min(buyEffects.Count, buyEffectActions.Count);
            for (int i = 0; i < count; i++)
            {
                BuyEffectBinding b = buyEffects[i];
                UnityAction a = buyEffectActions[i];
                if (b != null && b.buyButton != null && a != null)
                {
                    b.buyButton.onClick.RemoveListener(a);
                }
            }
        }

        buyEffectActions.Clear();
    }

    private void UnregisterTickEffects()
    {
        if (buyEffects != null && tickEffectActions.Count > 0)
        {
            int count = Mathf.Min(buyEffects.Count, tickEffectActions.Count);
            for (int i = 0; i < count; i++)
            {
                BuyEffectBinding b = buyEffects[i];
                UnityAction a = tickEffectActions[i];

                Button btn = b != null && b.tickImage != null ? b.tickImage.GetComponent<Button>() : null;

                if (btn != null && a != null)
                {
                    btn.onClick.RemoveListener(a);
                }
            }
        }

        tickEffectActions.Clear();
    }

    private void SelectPurchasedCosmetic(int index)
    {
        if (!IsPurchased(index))
        {
            ApplyPurchasedStateForIndex(index);
            return;
        }

        SetSelectedIndex(index);
    }

    private void PlayBuyEffect(int index)
    {
        if (buyEffects == null || index < 0 || index >= buyEffects.Count) return;

        BuyEffectBinding b = buyEffects[index];
        if (b == null) return;

        if (IsPurchased(index))
        {
            SetSelectedIndex(index);
            return;
        }

        if (b.buyButton != null)
        {
            b.buyButton.interactable = false;
        }

        GameObject particleInstance = null;
        ParticleSystem ps = null;

        if (b.particlePrefab != null && b.particleParent != null)
        {
            particleInstance = Instantiate(b.particlePrefab, b.particleParent);
            particleInstance.transform.localPosition = Vector3.zero;
            particleInstance.transform.localRotation = Quaternion.identity;
            particleInstance.transform.localScale = b.particlePrefab.transform.localScale;
            ps = particleInstance.GetComponentInChildren<ParticleSystem>(true);
        }

        if (ps != null)
        {
            StartCoroutine(WaitForParticleThenFade(ps, particleInstance, index));
        }
        else
        {
            if (particleInstance != null)
            {
                Destroy(particleInstance);
            }

            CompletePurchase(index);
        }
    }

    private System.Collections.IEnumerator WaitForParticleThenFade(ParticleSystem ps, GameObject particleInstance, int purchasedIndex)
    {
        float timeout = 10f;
        float t = 0f;
        while (ps != null && ps.IsAlive(true) && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (particleInstance != null)
        {
            Destroy(particleInstance);
        }

        CompletePurchase(purchasedIndex);
    }

    private void CompletePurchase(int index)
    {
        if (buyEffects == null || index < 0 || index >= buyEffects.Count) return;

        SetPurchased(index, true);
        SetSelectedIndex(index);
        ApplyPurchasedStateForIndex(index);

        BuyEffectBinding b = buyEffects[index];
        if (b != null)
        {
            ShowPurchaseComplete(b.purchasedSprite, index);
        }
    }

    private void DisableBoughtImage(Image imageToFade)
    {
        if (imageToFade == null) return;

        imageToFade.DOKill();
        imageToFade.gameObject.SetActive(false);
    }

    private void ShowPurchaseComplete(Sprite purchasedSprite, int purchasedIndex)
    {
        if (purchasePanel != null)
        {
            purchasePanel.SetActive(true);
        }

        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Pattern(new long[] { 0, 35, 25, 75 }, new int[] { 160, 0, 255 });
        }

        if (purchasePieceImage != null)
        {
            purchasePieceImage.sprite = purchasedSprite;
            purchasePieceImage.enabled = purchasedSprite != null;
            purchasePieceImage.gameObject.SetActive(true);
        }

        BuyEffectBinding b = null;
        if (buyEffects != null && purchasedIndex >= 0 && purchasedIndex < buyEffects.Count)
        {
            b = buyEffects[purchasedIndex];
        }

        EnablePurchasePiece(b, purchasedIndex);

        PlayPurchaseParticles();
        StartPurchasePanelAnimation();
    }

    private void PlayPurchaseParticles()
    {
        if (purchaseParticleObject == null) return;

        purchaseParticleObject.SetActive(true);
        ParticleSystem[] systems = purchaseParticleObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            systems[i].Clear(true);
            systems[i].Play(true);
        }
    }

    private void StartPurchasePanelAnimation()
    {
        if (purchasePieceImage == null) return;

        Transform t = purchasePieceImage.transform;
        if (t == null) return;

        if (!purchaseBaseCached)
        {
            purchaseBaseLocalPos = t.localPosition;
            purchaseBaseCached = true;
        }

        StopPurchasePanelAnimation();

        Vector3 targetPos = purchaseBaseLocalPos;
        Vector3 startPos = targetPos + Vector3.up * PurchaseEnterFromBottomOffset;
        t.localPosition = startPos;

        purchaseMoveTween = t.DOLocalMove(targetPos, Mathf.Max(0.01f, PurchaseEnterDuration))
            .SetEase(Ease.OutBack);
    }

    private void StopPurchasePanelAnimation()
    {
        if (purchaseMoveTween != null)
        {
            purchaseMoveTween.Kill();
            purchaseMoveTween = null;
        }

        if (purchasePieceImage != null && purchaseBaseCached)
        {
            Transform t = purchasePieceImage.transform;
            if (t != null)
            {
                t.localPosition = purchaseBaseLocalPos;
            }
        }
    }

    private void KillTweens()
    {
        if (goldTween != null)
        {
            goldTween.Kill();
            goldTween = null;
        }

        if (primeTween != null)
        {
            primeTween.Kill();
            primeTween = null;
        }
    }

    public void SelectGold()
    {
        Select(CosmeticTab.Gold);
    }

    public void SelectPrime()
    {
        Select(CosmeticTab.Prime);
    }

    private void Select(CosmeticTab tab)
    {
        if (isTransitioning) return;
        if (tab == currentTab) return;

        RectTransform incoming = tab == CosmeticTab.Gold ? goldBox : primeBox;
        RectTransform outgoing = tab == CosmeticTab.Gold ? primeBox : goldBox;

        if (incoming == null || outgoing == null)
        {
            currentTab = tab;
            ApplyImmediate(currentTab);
            return;
        }

        isTransitioning = true;
        KillTweens();

        float offset = GetOffscreenOffset(incoming);

        Vector2 incomingOn = tab == CosmeticTab.Gold ? goldOnPos : primeOnPos;
        Vector2 outgoingOn = tab == CosmeticTab.Gold ? primeOnPos : goldOnPos;

        bool incomingFromRight = tab == CosmeticTab.Prime;
        Vector2 incomingStart = incomingFromRight
            ? (incomingOn + Vector2.right * offset)
            : (incomingOn + Vector2.left * offset);
        Vector2 outgoingEnd = incomingFromRight
            ? (outgoingOn + Vector2.left * offset)
            : (outgoingOn + Vector2.right * offset);

        incoming.gameObject.SetActive(true);
        incoming.anchoredPosition = incomingStart;

        Tween tIn = incoming.DOAnchorPos(incomingOn, transitionDuration).SetEase(transitionEase);
        Tween tOut = outgoing.DOAnchorPos(outgoingEnd, transitionDuration).SetEase(transitionEase);

        if (tab == CosmeticTab.Gold)
        {
            goldTween = tIn;
            primeTween = tOut;
        }
        else
        {
            primeTween = tIn;
            goldTween = tOut;
        }

        tIn.OnComplete(() =>
        {
            outgoing.gameObject.SetActive(false);
            outgoing.anchoredPosition = outgoingOn;

            currentTab = tab;
            isTransitioning = false;
        });
    }

    private void ApplyImmediate(CosmeticTab tab)
    {
        if (goldBox != null)
        {
            goldBox.gameObject.SetActive(tab == CosmeticTab.Gold);
            goldBox.anchoredPosition = goldOnPos;
        }

        if (primeBox != null)
        {
            primeBox.gameObject.SetActive(tab == CosmeticTab.Prime);
            primeBox.anchoredPosition = primeOnPos;
        }
    }

    private void ApplyAmounts()
    {
        ApplyAmountList(goldAmounts);
        ApplyAmountList(primeAmounts);
    }

    private void ApplyAmountList(List<AmountTextBinding> bindings)
    {
        if (bindings == null || bindings.Count == 0) return;

        for (int i = 0; i < bindings.Count; i++)
        {
            AmountTextBinding b = bindings[i];
            if (b == null) continue;

            TMP_Text t = b.text;
            if (t == null) continue;

            float v = b.amount;
            try
            {
                t.text = string.Format(amountFormat, v);
            }
            catch
            {
                t.text = v.ToString("0.##");
            }
        }
    }

    private float GetOffscreenOffset(RectTransform box)
    {
        RectTransform parent = box != null ? box.parent as RectTransform : null;
        float parentWidth = parent != null ? parent.rect.width : 1080f;
        float boxWidth = box != null ? box.rect.width : 0f;
        return (parentWidth * 0.5f) + (boxWidth * 0.5f) + Mathf.Max(0f, offscreenPadding);
    }
}
