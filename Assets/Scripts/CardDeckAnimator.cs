/*
 * Card Deck Animator - Card deck animation system
 * 45 cards ek ek kari ne DeckShadow ma animate thase with rotation
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using My.UI;

public class CardDeckAnimator : MonoBehaviour
{
    [Header("Card Prefab")]
    [Tooltip("Card prefab jo spawn thase")]
    [SerializeField] GameObject cardPrefab = null;

    [Header("Deck Shadow")]
    [Tooltip("DeckShadow GameObject - jya cards aavi jase")]
    [SerializeField] RectTransform deckShadow = null;

    private struct DeckTintBackup
    {
        public bool hasImage;
        public Color imageColor;
        public bool hasButton;
        public ColorBlock buttonColors;
    }

    private readonly Dictionary<int, DeckTintBackup> deckTintBackups = new Dictionary<int, DeckTintBackup>();
    private bool deckTintActive = false;
    private Color deckTintColor = Color.white;

    [Header("Starting Position")]
    [Tooltip("Card start position (bar) - jya thi cards start thase")]
    [SerializeField] RectTransform startPosition = null;

    [Header("Animation Settings")]
    [Tooltip("Ek card nu animation duration (seconds)")]
    [SerializeField] float cardAnimationDuration = 0.5f;

    [Tooltip("Cards ma delay (ek biji pachhi kitlu time)")]
    [SerializeField] float delayBetweenCards = 0.1f;

    [Tooltip("Rotation angle (degrees) during animation")]
    [SerializeField] float rotationAngle = 45f;

    [Tooltip("Total cards count")]
    [SerializeField] int totalCards = 45;

    [Tooltip("Card stacking visual offset (cards ek ke uper ek stack thase - slight offset for depth)")]
    [SerializeField] float cardStackDepthOffset = 2.5f;

    [Tooltip("Stack cards upward instead of downward (Y offset direction)")]
    [SerializeField] bool stackUpwards = false;

    [Tooltip("Final rotation after reaching DeckShadow (degrees)")]
    [SerializeField] float finalStackRotation = 45f;

    [Header("Performance")]
    [SerializeField] int dotweenTweenCapacity = 400;

    [Header("Card Pick Arrow")]
    [SerializeField] private RectTransform cardPickArrow = null;
    [SerializeField] private float cardPickArrowBobDistance = 20f;
    [SerializeField] private float cardPickArrowBobDuration = 0.6f;

    private Vector2 cardPickArrowBaseAnchoredPos;
    private bool cardPickArrowBaseStored = false;
    private Coroutine cardPickArrowCoroutine = null;
    private bool pendingStartCardPickArrow = false;

    private void Awake()
    {
        if (cardPickArrow != null && !cardPickArrowBaseStored)
        {
            cardPickArrowBaseAnchoredPos = cardPickArrow.anchoredPosition;
            cardPickArrowBaseStored = true;
        }
    }

    [Header("Sorting")]
    [SerializeField] bool forceAnimatedCardOnTop = true;
    [SerializeField] int animatedCardTopSortingOffset = 1000;

    [Header("Curve Animation")]
    [Tooltip("Curve height (path curve kare - upar neeche movement)")]
    [SerializeField] float curveHeight = 314.9f;

    [Tooltip("Invert curve vertically (upar-niiche arc opposite karva mate)")]
    [SerializeField] bool invertCurveVertical = false;

    [Tooltip("Curve offset (left-right curve)")]
    [SerializeField] float curveOffset = 106.4f;

    [Header("Auto Start")]
    [Tooltip("Game start par automatically animation start thase")]
    [SerializeField] bool autoStartOnEnable = false;

    [Header("Auto Repeat")]
    [SerializeField] bool autoRepeatAfterBuild = false;
    [SerializeField] float repeatDelay = 0.5f;
    [SerializeField] bool animateResetToStart = true;
    [SerializeField] float resetAnimationDuration = 0.25f;
    [SerializeField] float delayBetweenResetCards = 0.01f;
    [SerializeField] float delayAfterResetComplete = 0.35f;


    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<CardData> deckData = new List<CardData>();
    private bool isAnimating = false;
    private Coroutine currentSequenceCoroutine = null;
    private Sequence activeDealSequence = null;

    private struct CanvasSortingBackup
    {
        public bool hadCanvas;
        public bool overrideSorting;
        public int sortingOrder;
        public int sortingLayerID;
    }

    private readonly Dictionary<int, CanvasSortingBackup> sortingBackups = new Dictionary<int, CanvasSortingBackup>();

    [Header("Last Card Info (Testing - Read Only)")]
    [Tooltip("Last card nu power info (testing mate visible - Inspector ma card power joi shaksho)")]
    [SerializeField] private CardClickHandler lastCardHandler = null;


    /// <summary>
    /// Get last card handler (testing mate)
    /// </summary>
    public CardClickHandler GetLastCardHandler()
    {
        return lastCardHandler;
    }

    public void StartCardPickArrow()
    {
        if (cardPickArrow == null) return;

        if (!isActiveAndEnabled)
        {
            pendingStartCardPickArrow = true;
            return;
        }

        // If the arrow is already running, avoid restarting the tween.
        // Multiple callers (turn refresh + deck-ready reminders) can trigger start in quick succession,
        // and restarting causes a visible "jump".
        if (cardPickArrow.gameObject.activeSelf && cardPickArrowCoroutine == null && DOTween.IsTweening(cardPickArrow))
        {
            return;
        }

        DOTween.Kill(cardPickArrow);

        cardPickArrow.gameObject.SetActive(true);

        if (cardPickArrowCoroutine != null)
        {
            StopCoroutine(cardPickArrowCoroutine);
            cardPickArrowCoroutine = null;
        }

        cardPickArrowCoroutine = StartCoroutine(StartCardPickArrowNextFrame());
    }

    private IEnumerator StartCardPickArrowNextFrame()
    {
        // Wait for layout/anchoredPosition to settle to avoid first-frame jump.
        yield return new WaitForEndOfFrame();

        if (cardPickArrow == null)
        {
            cardPickArrowCoroutine = null;
            yield break;
        }

        DOTween.Kill(cardPickArrow);

        if (!cardPickArrowBaseStored)
        {
            cardPickArrowBaseAnchoredPos = cardPickArrow.anchoredPosition;
            cardPickArrowBaseStored = true;
        }
        cardPickArrow.anchoredPosition = cardPickArrowBaseAnchoredPos;

        Vector2 target = cardPickArrowBaseAnchoredPos + new Vector2(0f, cardPickArrowBobDistance);
        cardPickArrow.DOAnchorPos(target, cardPickArrowBobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        cardPickArrowCoroutine = null;
    }

    public void StopCardPickArrow()
    {
        if (cardPickArrow == null) return;

        pendingStartCardPickArrow = false;

        if (cardPickArrowCoroutine != null)
        {
            StopCoroutine(cardPickArrowCoroutine);
            cardPickArrowCoroutine = null;
        }

        DOTween.Kill(cardPickArrow);
        if (cardPickArrowBaseStored)
        {
            cardPickArrow.anchoredPosition = cardPickArrowBaseAnchoredPos;
        }
        cardPickArrow.gameObject.SetActive(false);
    }

    public void ApplyDeckTint(Color tint)
    {
        deckTintActive = true;
        deckTintColor = tint;
        ApplyDeckTintInternal();
    }

    public void ClearDeckTint()
    {
        deckTintActive = false;
        RestoreDeckTintInternal();
    }

    private void ApplyDeckTintInternal()
    {
        if (deckShadow == null) return;

        for (int i = 0; i < deckShadow.childCount; i++)
        {
            Transform t = deckShadow.GetChild(i);
            if (t == null) continue;

            int id = t.gameObject.GetInstanceID();
            if (!deckTintBackups.ContainsKey(id))
            {
                DeckTintBackup b = new DeckTintBackup();

                Image img = t.GetComponent<Image>();
                if (img != null)
                {
                    b.hasImage = true;
                    b.imageColor = img.color;
                }

                Button btn = t.GetComponent<Button>();
                if (btn != null)
                {
                    b.hasButton = true;
                    b.buttonColors = btn.colors;
                }

                deckTintBackups[id] = b;
            }

            Image imageToTint = t.GetComponent<Image>();
            if (imageToTint != null)
            {
                imageToTint.color = deckTintColor;
            }

            Button buttonToTint = t.GetComponent<Button>();
            if (buttonToTint != null)
            {
                ColorBlock cb = buttonToTint.colors;
                cb.normalColor = deckTintColor;
                cb.highlightedColor = deckTintColor;
                cb.pressedColor = deckTintColor;
                cb.selectedColor = deckTintColor;
                cb.disabledColor = deckTintColor;
                buttonToTint.colors = cb;
            }
        }
    }

    private void RestoreDeckTintInternal()
    {
        if (deckShadow == null) return;

        for (int i = 0; i < deckShadow.childCount; i++)
        {
            Transform t = deckShadow.GetChild(i);
            if (t == null) continue;

            int id = t.gameObject.GetInstanceID();
            if (!deckTintBackups.TryGetValue(id, out var b))
            {
                continue;
            }

            if (b.hasImage)
            {
                Image img = t.GetComponent<Image>();
                if (img != null) img.color = b.imageColor;
            }

            if (b.hasButton)
            {
                Button btn = t.GetComponent<Button>();
                if (btn != null) btn.colors = b.buttonColors;
            }

            deckTintBackups.Remove(id);
        }
    }

    private void ApplyTopSortingIfNeeded(GameObject card, Canvas referenceCanvas, int orderWithinAnimation)
    {
        if (!forceAnimatedCardOnTop || card == null) return;

        int id = card.GetInstanceID();
        if (sortingBackups.ContainsKey(id))
        {
            return;
        }

        Canvas canvas = card.GetComponent<Canvas>();
        bool hadCanvas = canvas != null;
        if (!hadCanvas)
        {
            canvas = card.AddComponent<Canvas>();
        }

        CanvasSortingBackup backup = new CanvasSortingBackup
        {
            hadCanvas = hadCanvas,
            overrideSorting = canvas.overrideSorting,
            sortingOrder = canvas.sortingOrder,
            sortingLayerID = canvas.sortingLayerID
        };
        sortingBackups.Add(id, backup);

        canvas.overrideSorting = true;

        int baseOrder = 0;
        int baseLayerId = backup.sortingLayerID;
        if (referenceCanvas != null)
        {
            baseOrder = referenceCanvas.sortingOrder;
            baseLayerId = referenceCanvas.sortingLayerID;
        }
        canvas.sortingLayerID = baseLayerId;
        canvas.sortingOrder = baseOrder + animatedCardTopSortingOffset + orderWithinAnimation;
    }

    private void RestoreTopSortingIfNeeded(GameObject card)
    {
        if (!forceAnimatedCardOnTop || card == null) return;

        int id = card.GetInstanceID();
        if (!sortingBackups.TryGetValue(id, out CanvasSortingBackup backup))
        {
            return;
        }
        sortingBackups.Remove(id);

        Canvas canvas = card.GetComponent<Canvas>();
        if (canvas == null) return;

        if (!backup.hadCanvas)
        {
            Destroy(canvas);
            return;
        }

        canvas.overrideSorting = backup.overrideSorting;
        canvas.sortingOrder = backup.sortingOrder;
        canvas.sortingLayerID = backup.sortingLayerID;
    }

    void OnEnable()
    {
        StopCardPickArrow();
        if (deckTintActive)
        {
            ApplyDeckTintInternal();
        }

        if (pendingStartCardPickArrow)
        {
            pendingStartCardPickArrow = false;
            StartCardPickArrow();
        }
        if (autoStartOnEnable)
        {
            StartCardAnimation();
        }
    }

    void Start()
    {
        // Deck data generate karo (45 cards)
        GenerateDeckData();

        DOTween.SetTweensCapacity(dotweenTweenCapacity, dotweenTweenCapacity / 2);
    }

    /// <summary>
    /// 45 cards nu deck data generate karo
    /// </summary>
    void GenerateDeckData()
    {
        deckData.Clear();

        // 1-card: 5 cards - "Move +1"
        for (int i = 0; i < 5; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card1, "Move +1", "", false, 1, 0));
        }

        // 2-card: 4 cards - "Move +2"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card2, "Move +2", "", false, 2, 0));
        }

        // 3-card: 4 cards - "Move +3"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card3, "Move +3", "", false, 3, 0));
        }

        // 4-card: 4 cards - "Move -4 backward"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card4, "Move -4", "Backward only", true, -4, 0));
        }

        // 5-card: 4 cards - "Move +5"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card5, "Move +5", "", false, 5, 0));
        }

        // 7-card: 4 cards - "Move +7 or split"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card7, "Move +7", "OR Split", true, 7, 0));
        }

        // 8-card: 4 cards - "Move +8"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card8, "Move +8", "", false, 8, 0));
        }

        // 10-card: 4 cards - "Move +10 or -1 backward"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card10, "Move +10", "OR -1 backward", true, 10, -1));
        }

        // 11-card: 4 cards - "Move +11 or swap"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card11, "Move +11", "OR Swap", true, 11, 0));
        }

        // 12-card: 4 cards - "Move +12"
        for (int i = 0; i < 4; i++)
        {
            deckData.Add(new CardData(CardData.CardType.Card12, "Move +12", "", false, 12, 0));
        }

        // SORRY! card: 5 cards - "Attack card"
        for (int i = 0; i < 5; i++)
        {
            deckData.Add(new CardData(CardData.CardType.SorryCard, "SORRY!", "+4", true, 0, 4));
        }

        // Shuffle deck (random order)
        ShuffleDeck();

        Debug.Log($"Deck generated with {deckData.Count} cards!");
    }

    /// <summary>
    /// Deck shuffle karo (random order)
    /// </summary>
    void ShuffleDeck()
    {
        for (int i = 0; i < deckData.Count; i++)
        {
            CardData temp = deckData[i];
            int randomIndex = Random.Range(i, deckData.Count);
            deckData[i] = deckData[randomIndex];
            deckData[randomIndex] = temp;
        }
    }

    /// <summary>
    /// Start animation - 45 cards animate karse
    /// </summary>
    public void StartCardAnimation()
    {
        if (isAnimating)
        {
            Debug.LogWarning("Card animation already running!");
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogError("Card Prefab is not assigned!");
            return;
        }

        if (deckShadow == null)
        {
            Debug.LogError("Deck Shadow is not assigned!");
            return;
        }

        if (HapticsManager.Instance != null)
        {
            //HapticsManager.Instance.Medium();
        }

        currentSequenceCoroutine = StartCoroutine(AnimateCardsDOTweenCoroutine());
    }

    public void StartCardAnimationAndShuffle()
    {
        ShuffleDeck();
        StartCardAnimation();
    }

    /// <summary>
    /// Stop animation and clear cards
    /// </summary>
    public void ClearCards()
    {
        StopAllCoroutines();
        isAnimating = false;
        currentSequenceCoroutine = null;

        sortingBackups.Clear();

        if (activeDealSequence != null)
        {
            activeDealSequence.Kill();
            activeDealSequence = null;
        }

        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }
        spawnedCards.Clear();
    }

    public void ResetShuffleAndReplay()
    {
        if (currentSequenceCoroutine != null)
        {
            StopCoroutine(currentSequenceCoroutine);
            currentSequenceCoroutine = null;
        }

        if (activeDealSequence != null)
        {
            activeDealSequence.Kill();
            activeDealSequence = null;
        }

        currentSequenceCoroutine = StartCoroutine(ResetShuffleAndReplayCoroutine());
    }

    private IEnumerator AnimateCardsDOTweenCoroutine()
    {
        isAnimating = true;

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.SetCardAnimationLock(true);
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Vector3 startPos = GetStartWorldPos(parentCanvas);
        Transform cardParent = GetCardParent(parentCanvas);

        if (HasReusableCards())
        {
            PrepareReusableCards(cardParent, startPos);
        }
        else
        {
            ClearCardsOnly();
        }

        if (activeDealSequence != null)
        {
            activeDealSequence.Kill();
            activeDealSequence = null;
        }

        activeDealSequence = DOTween.Sequence();
        activeDealSequence.SetAutoKill(true);

        for (int i = 0; i < totalCards; i++)
        {
            GameObject card = null;
            if (HasReusableCards())
            {
                card = spawnedCards[i];
            }
            else
            {
                card = Instantiate(cardPrefab, cardParent);
            }

            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null)
            {
                Debug.LogError("Card prefab ma RectTransform component nahi che!");
                if (!HasReusableCards())
                {
                    Destroy(card);
                }
                continue;
            }

            if (!HasReusableCards())
            {
                cardRect.position = startPos;
                cardRect.localRotation = Quaternion.identity;
                cardRect.localScale = Vector3.one;
                spawnedCards.Add(card);
            }

            cardRect.SetAsLastSibling();

            if (!HasReusableCards())
            {
                if (i < deckData.Count)
                {
                    if (gm == null || !gm.IsPlayWithOopsMode)
                    {
                        AssignCardData(card, deckData[i], i + 1);
                    }
                }
                else
                {
                    Debug.LogWarning($"Card index {i} >= deckData.Count ({deckData.Count}) - No power data assigned!");
                }
            }

            if (gm != null && gm.IsPlayWithOopsMode)
            {
                card.name = $"Card [{i + 1}]";
                CardClickHandler resetHandler = card.GetComponent<CardClickHandler>();
                if (resetHandler != null)
                {
                    resetHandler.cardPower1 = "";
                    resetHandler.cardPower2 = "";
                    resetHandler.cardHasDualPower = false;
                    resetHandler.cardValue1 = 0;
                    resetHandler.cardValue2 = 0;
                }
            }

            CardClickHandler clickHandler = card.GetComponent<CardClickHandler>();
            if (clickHandler != null)
            {
                clickHandler.SetCardClickable(false);
                if (gm != null && gm.IsPlayWithOopsMode)
                {
                    clickHandler.SetOopsHidden(true);
                }
            }

            Vector3 targetLocalPos = Vector3.zero;
            if (cardStackDepthOffset > 0)
            {
                float signedStackOffset = stackUpwards ? cardStackDepthOffset : -cardStackDepthOffset;
                targetLocalPos = new Vector3(0, i * signedStackOffset, 0);
            }
            float animationRotation = rotationAngle;
            float finalRotation = finalStackRotation;

            RectTransform deckShadowParent = deckShadow;
            Vector3 targetWorldPos = deckShadowParent.TransformPoint(targetLocalPos);
            Vector3 midPoint = (startPos + targetWorldPos) * 0.5f;
            Vector3 curveControl = midPoint;
            float signedCurveHeight = invertCurveVertical ? -curveHeight : curveHeight;
            curveControl.y += signedCurveHeight;
            float randomOffset = Mathf.Sin(i * 0.5f) * curveOffset;
            curveControl.x += randomOffset;

            float startTime = (delayBetweenCards > 0f) ? (i * delayBetweenCards) : 0f;

            int capturedIndex = i;
            Tween moveTween = DOTween.To(
                () => 0f,
                t =>
                {
                    float oneMinusT = 1f - t;
                    Vector3 curvedPos = oneMinusT * oneMinusT * startPos +
                                       2f * oneMinusT * t * curveControl +
                                       t * t * targetWorldPos;
                    cardRect.position = curvedPos;
                    cardRect.localRotation = Quaternion.Lerp(Quaternion.identity, Quaternion.Euler(0, 0, animationRotation), t);
                },
                1f,
                cardAnimationDuration
            ).SetEase(Ease.OutCubic);

            int capturedOrderWithinAnimation = i;

            activeDealSequence.InsertCallback(startTime, () =>
            {
                if (capturedOrderWithinAnimation % 6 == 0)
                {
                    if (HapticsManager.Instance != null)
                    {
                      //  HapticsManager.Instance.Light();
                    }
                }
                if (cardRect != null)
                {
                    ApplyTopSortingIfNeeded(cardRect.gameObject, parentCanvas, capturedOrderWithinAnimation);
                    cardRect.SetAsLastSibling();
                }
            });

            activeDealSequence.Insert(startTime, moveTween);

            activeDealSequence.InsertCallback(startTime + cardAnimationDuration, () =>
            {
                if (cardRect == null || deckShadowParent == null) return;
                cardRect.SetParent(deckShadowParent);
                cardRect.localPosition = targetLocalPos;
                cardRect.localRotation = Quaternion.Euler(0, 0, finalRotation);
                cardRect.localScale = Vector3.one;
                cardRect.SetAsLastSibling();

                RestoreTopSortingIfNeeded(cardRect.gameObject);

                CardClickHandler handler = cardRect.GetComponent<CardClickHandler>();
                if (handler != null)
                {
                    if (gm == null || !gm.IsPlayWithOopsMode)
                    {
                        if (string.IsNullOrEmpty(handler.cardPower1) && capturedIndex < deckData.Count)
                        {
                            handler.StoreCardPowerData(deckData[capturedIndex].power1, deckData[capturedIndex].power2, deckData[capturedIndex].hasDualPower, capturedIndex + 1);
                        }
                        if (capturedIndex >= 0 && capturedIndex < deckData.Count && !string.IsNullOrEmpty(handler.cardPower1))
                        {
                            handler.UpdateCardNameWithPower(capturedIndex + 1);
                        }
                    }
                    else
                    {
                        handler.cardPower1 = "";
                        handler.cardPower2 = "";
                        handler.cardHasDualPower = false;
                        handler.cardValue1 = 0;
                        handler.cardValue2 = 0;
                    }
                }
            });
        }

        yield return activeDealSequence.WaitForCompletion();
        activeDealSequence = null;

        isAnimating = false;

        Debug.Log($"Card animation complete! {totalCards} cards animated to deck.");
        if (gm == null || !gm.IsPlayWithOopsMode)
        {
            VerifyAllCardsHavePowerNames();
        }

        if (HapticsManager.Instance != null)
        {
            //HapticsManager.Instance.Success();
        }

        if (spawnedCards.Count > 0 && deckShadow != null)
        {
            if (deckShadow.childCount > 0)
            {
                Transform lastCardTransform = deckShadow.GetChild(deckShadow.childCount - 1);
                CardClickHandler lastCardClickHandler = lastCardTransform.GetComponent<CardClickHandler>();
                if (lastCardClickHandler != null)
                {
                    lastCardClickHandler.SetCardClickable(true);
                    lastCardHandler = lastCardClickHandler;
                }
            }
        }

        if (gm != null)
        {
            gm.SetCardAnimationLock(false);
            gm.NotifyDeckReady();
        }

        if (autoRepeatAfterBuild)
        {
            yield return new WaitForSeconds(repeatDelay);
            currentSequenceCoroutine = StartCoroutine(ResetShuffleAndReplayCoroutine());
        }
    }

    private Transform GetCardParent(Canvas parentCanvas)
    {
        if (parentCanvas != null)
        {
            return parentCanvas.transform;
        }
        if (deckShadow != null)
        {
            return deckShadow.root;
        }
        return transform;
    }

    private Vector3 GetStartWorldPos(Canvas parentCanvas)
    {
        if (startPosition != null)
        {
            return startPosition.position;
        }

        if (parentCanvas != null)
        {
            RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
            return new Vector3(canvasRect.position.x, canvasRect.position.y + canvasRect.rect.height / 2, 0);
        }

        return new Vector3(Screen.width / 2, Screen.height, 0);
    }

    private bool HasReusableCards()
    {
        if (spawnedCards == null || spawnedCards.Count != totalCards)
        {
            return false;
        }

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void PrepareReusableCards(Transform cardParent, Vector3 startPos)
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            GameObject card = spawnedCards[i];
            if (card == null) continue;

            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) continue;

            cardRect.SetParent(cardParent);
            cardRect.position = startPos;
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = Vector3.one;
            cardRect.SetAsLastSibling();

            if (i < deckData.Count)
            {
                AssignCardData(card, deckData[i], i + 1);
            }

            CardClickHandler clickHandler = card.GetComponent<CardClickHandler>();
            if (clickHandler != null)
            {
                clickHandler.SetCardClickable(false);
            }
        }
    }

    /// <summary>
    /// Only clear cards without stopping coroutines
    /// </summary>
    private void ClearCardsOnly()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }
        spawnedCards.Clear();
    }

    private IEnumerator ResetShuffleAndReplayCoroutine()
    {
        if (isAnimating)
        {
            yield break;
        }

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.SetCardAnimationLock(true);
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Transform cardParent = GetCardParent(parentCanvas);
        Vector3 startPos = GetStartWorldPos(parentCanvas);

        Sequence resetSequence = DOTween.Sequence();
        resetSequence.SetAutoKill(true);

        if (deckShadow != null)
        {
            int childCount = deckShadow.childCount;
            for (int idx = 0; idx < childCount; idx++)
            {
                int childIndexFromTop = (childCount - 1) - idx;
                RectTransform cardRect = deckShadow.GetChild(childIndexFromTop) as RectTransform;
                if (cardRect == null) continue;

                float startTime = (delayBetweenResetCards > 0f) ? (idx * delayBetweenResetCards) : 0f;

                resetSequence.InsertCallback(startTime, () =>
                {
                    if (cardRect == null) return;
                    ApplyTopSortingIfNeeded(cardRect.gameObject, parentCanvas, idx);
                    cardRect.SetAsLastSibling();
                });

                if (animateResetToStart && resetAnimationDuration > 0f)
                {
                    resetSequence.Insert(startTime, cardRect.DOMove(startPos, resetAnimationDuration).SetEase(Ease.OutCubic));
                    resetSequence.Insert(startTime, cardRect.DORotate(Vector3.zero, resetAnimationDuration, RotateMode.Fast).SetEase(Ease.OutCubic));
                }
                else
                {
                    resetSequence.InsertCallback(startTime, () =>
                    {
                        if (cardRect != null)
                        {
                            cardRect.position = startPos;
                            cardRect.localRotation = Quaternion.identity;
                        }
                    });
                }

                resetSequence.InsertCallback(startTime + Mathf.Max(0f, resetAnimationDuration), () =>
                {
                    if (cardRect == null) return;
                    cardRect.SetParent(cardParent);
                    cardRect.position = startPos;
                    cardRect.localRotation = Quaternion.identity;
                    cardRect.localScale = Vector3.one;
                    cardRect.SetAsLastSibling();

                    RestoreTopSortingIfNeeded(cardRect.gameObject);
                });
            }
        }

        yield return resetSequence.WaitForCompletion();

        if (delayAfterResetComplete > 0)
        {
            yield return new WaitForSeconds(delayAfterResetComplete);
        }

        ShuffleDeck();
        StartCardAnimation();
    }

    void AssignCardData(GameObject card, CardData cardData, int cardIndex = -1)
    {
        CardClickHandler clickHandler = card.GetComponent<CardClickHandler>();
        if (clickHandler != null)
        {
            // Debug log for first card
            if (cardIndex == 1)
            {
                Debug.Log($"🔵 AssignCardData: First card - Name before: '{card.name}', Index: {cardIndex}, Power1: '{cardData.power1}'");
            }
            
            // Store power data with card index
            clickHandler.StoreCardPowerData(cardData.power1, cardData.power2, cardData.hasDualPower, cardIndex, cardData.value1, cardData.value2);
            
            // Verify name was updated (especially for first card)
            if (cardIndex == 1)
            {
                Debug.Log($"🔵 AssignCardData: First card - Name after: '{card.name}', Expected: 'Card [1] - {cardData.power1}'");
                if (card.name == "Card(Clone)")
                {
                    Debug.LogError($"❌ FIRST CARD NAME NOT UPDATED! Still 'Card(Clone)' - Power1: '{cardData.power1}'");
                }
            }
            
            // Immediately verify stored values
            if (string.IsNullOrEmpty(clickHandler.cardPower1) && !string.IsNullOrEmpty(cardData.power1))
            {
                Debug.LogError($"⚠️ AssignCardData: Power1 store nahi thayu! Expected: '{cardData.power1}', Got: '{clickHandler.cardPower1}'");
            }
            if (string.IsNullOrEmpty(clickHandler.cardPower2) && !string.IsNullOrEmpty(cardData.power2))
            {
                Debug.LogError($"⚠️ AssignCardData: Power2 store nahi thayu! Expected: '{cardData.power2}', Got: '{clickHandler.cardPower2}'");
            }
            
            // SORRY card mate extra verification
            if (cardData.cardType == CardData.CardType.SorryCard)
            {
                Debug.Log($"✅ AssignCardData: SORRY card verified - Power1: '{clickHandler.cardPower1}', Power2: '{clickHandler.cardPower2}', Dual: {clickHandler.cardHasDualPower}");
            }
        }
        else
        {
            Debug.LogError($"⚠️ CardDeckAnimator: Card '{card.name}' par CardClickHandler script nahi mali! Power data store nahi thai sakyu.");
        }
    }

    /// <summary>
    /// Verify all cards have power names (testing mate)
    /// </summary>
    void VerifyAllCardsHavePowerNames()
    {
        if (deckShadow == null) return;
        
        int cardsWithoutPower = 0;
        int cardsWithPower = 0;
        int cardsWithOldName = 0;
        
        for (int i = 0; i < deckShadow.childCount; i++)
        {
            Transform cardTransform = deckShadow.GetChild(i);
            CardClickHandler handler = cardTransform.GetComponent<CardClickHandler>();
            
            if (handler != null)
            {
                // Check if card has power
                if (string.IsNullOrEmpty(handler.cardPower1))
                {
                    cardsWithoutPower++;
                    Debug.LogError($"❌ Card [{i + 1}] '{cardTransform.name}' has NO POWER! cardPower1 is empty.");
                }
                else
                {
                    cardsWithPower++;
                    // Check if name was updated
                    if (cardTransform.name == "Card(Clone)" || cardTransform.name.StartsWith("Card(Clone)"))
                    {
                        cardsWithOldName++;
                        Debug.LogWarning($"⚠️ Card [{i + 1}] '{cardTransform.name}' has power '{handler.cardPower1}' but name not updated! Expected: 'Card [{i + 1}] - {handler.cardPower1}'");
                    }
                }
            }
        }
        
        Debug.Log($"=== CARD POWER VERIFICATION ===");
        Debug.Log($"Total Cards in DeckShadow: {deckShadow.childCount}");
        Debug.Log($"Cards with Power: {cardsWithPower}");
        Debug.Log($"Cards without Power: {cardsWithoutPower}");
        Debug.Log($"Cards with old name (Card(Clone)): {cardsWithOldName}");
        if (cardsWithoutPower == 0 && cardsWithOldName == 0)
        {
            Debug.Log($"✅ ALL {deckShadow.childCount} CARDS HAVE POWER AND UPDATED NAMES!");
        }
        Debug.Log($"=== END VERIFICATION ===");
    }

    /// <summary>
    /// OnDestroy - cleanup
    /// </summary>
    void OnDestroy()
    {
        ClearCards();
    }
}

