/*
 * Card Click Handler - Card click par flip animation aur OpenCardHolder ma move
 * Card click thay to flip thy ne OpenCardHolder ma aavi jay
 */

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using DG.Tweening;
using My.UI;

public class CardClickHandler : MonoBehaviour
{
    [Serializable]
    private class PowerSpriteMapping
    {
        public string powerText;
        public Sprite sprite;
        public Sprite player1Sprite;
        public Sprite player2Sprite;
        public Sprite player3Sprite;
        public Sprite player4Sprite;
    }

    [Header("Open Card Holder")]
    [Tooltip("OpenCardHolder GameObject - jya card move thase (agar nahi assign to automatically find karse)")]
    [SerializeField] RectTransform openCardHolder = null;

    [Tooltip("Automatically find OpenCardHolder (agar assign nahi kari hoy to)")]
    [SerializeField] bool autoFindOpenCardHolder = true;

    [Header("Starting Position")]
    [Tooltip("Starting Position GameObject - jya card return thase (agar nahi assign to automatically find karse)")]
    [SerializeField] RectTransform startPosition = null;

    [Tooltip("Automatically find Starting Position (agar assign nahi kari hoy to)")]
    [SerializeField] bool autoFindStartPosition = true;

    [Header("Animation Settings")]
    [Tooltip("Flip animation duration (seconds - fast flip mate 0.15-0.2 use karo)")]
    [SerializeField] float flipDuration = 0.2f;

    [Tooltip("Move animation duration (seconds - flip pachhi delay control kare)")]
    [SerializeField] float moveDuration = 0.3f;

    [Tooltip("OpenCardHolder ma kitlu time rakhse pachhi starting position ma return karse (seconds)")]
    [SerializeField] float waitTimeBeforeReturn = 2f;

    [Tooltip("Return animation duration (fade out + move back)")]
    [SerializeField] float returnAnimationDuration = 0.5f;

    [Tooltip("Return animation par card kitlu rotations spin karse (default 2 = 720 degrees)")]
    [SerializeField] float returnSpinRotations = 2f;

    [Header("Final Transform")]
    [Tooltip("Final local position (default 0,0,0)")]
    [SerializeField] Vector3 targetLocalPosition = Vector3.zero;

    [Tooltip("Final local scale during flip (default 1.2 - flip thy tyare moto thase)")]
    [SerializeField] Vector3 targetLocalScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Tooltip("Final actual size scale (default 1.0 - flip pachhi bounce sathe nano thase)")]
    [SerializeField] Vector3 actualSizeScale = new Vector3(1.0f, 1.0f, 1.0f);

    [Header("Bounce Effect Settings")]
    [Tooltip("Bounce effect duration (seconds - fast bounce mate 0.08-0.12 use karo)")]
    [SerializeField] float bounceDuration = 0.1f;

    [Tooltip("Bounce intensity (1.0 = no bounce, 1.05 = small bounce, 1.1 = large bounce)")]
    [Range(1.0f, 1.2f)]
    [SerializeField] float bounceIntensity = 1.05f;

    [Header("Card Sprite")]
    [Tooltip("Card flip thay pachhi je sprite show thase")]
    [SerializeField] Sprite cardFrontSprite = null;

    [Tooltip("Card ke andar Image component (sprite assign kari shake)")]
    [SerializeField] Image cardImage = null;

    [Header("Card Power Text")]
    [Tooltip("First power text (direct assign karo - name se find nahi karse)")]
    [SerializeField] TMPro.TextMeshProUGUI firstPowerText = null;

    [Tooltip("Second power text (direct assign karo - name se find nahi karse)")]
    [SerializeField] TMPro.TextMeshProUGUI secondPowerText = null;

    [Header("Card Power Sprites (Optional)")]
    [Tooltip("If assigned, this Image will be used for dual-power cards as a single combined sprite (covers both lines)")]
    [SerializeField] private Image combinedPowerImage = null;

    [Tooltip("Map power strings (same as CardData power1/power2) to sprites. When found, sprite shows and text hides.")]
    [SerializeField] private PowerSpriteMapping[] powerSpriteMappings = Array.Empty<PowerSpriteMapping>();

    [Tooltip("2-player mode mapping. If set and game is running with 2 players, this list is used instead of the default mapping.")]
    [SerializeField] private PowerSpriteMapping[] powerSpriteMappingsForTwoPlayers = Array.Empty<PowerSpriteMapping>();

    [Header("Card Power Info (Testing - Read Only)")]
    [Tooltip("First power text (testing mate visible)")]
    public string cardPower1 = "";

    [Tooltip("Second power text (testing mate visible)")]
    public string cardPower2 = "";

    [Tooltip("Card has dual power (testing mate visible)")]
    public bool cardHasDualPower = false;

     public int cardValue1 = 0;
     public int cardValue2 = 0;

     public string serverCardId = "";

    private RectTransform cardRectTransform;
    private Button cardButton;
    private CanvasGroup canvasGroup; // For fade out
    private bool isAnimating = false;
    private bool isCardClickable = false; // Internal flag - card clickable hai ya nahi
    private static CardClickHandler currentlyClickableCard = null; // Track which card is clickable

    public static CardClickHandler CurrentClickableCard => currentlyClickableCard;

    private Sprite cardBackSprite;

    private bool storedCardImageColor = false;
    private Color cardImageBaseColor = Color.white;

    private bool storedButtonColorBlock = false;
    private ColorBlock cardButtonBaseColors;

    private bool oopsHiddenVisualsApplied = false;
    private bool oopsCardOpenSent = false;
    private bool oopsWaitingForOpenResponse = false;
    
    // Store original values for return animation (piece move thay pachhi)
    private Vector3 storedOriginalPosition;
    private Quaternion storedOriginalRotation;
    private Vector3 storedOriginalScale;
    private Transform storedOriginalParent;

    void Awake()
    {
        cardRectTransform = GetComponent<RectTransform>();
        if (cardRectTransform == null)
        {
            Debug.LogError("CardClickHandler: RectTransform component nahi mali!");
        }

        if (cardImage != null)
        {
            cardBackSprite = cardImage.sprite;
        }

        // Button component add karo ya use karo
        cardButton = GetComponent<Button>();
        if (cardButton == null)
        {
            cardButton = gameObject.AddComponent<Button>();
        }

        // Button interactable always true rakho (UI par clickable dikhe)
        // But internal flag se control karo ki actual click par kaam kare ya nahi
        cardButton.interactable = true;
        isCardClickable = false; // Initially click par kaam nahi karega

        // Click listener add karo
        cardButton.onClick.AddListener(OnCardClicked);

        // Agar cardImage nahi assign kari hoy to find karo
        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }

        if (cardImage != null && !storedCardImageColor)
        {
            cardImageBaseColor = cardImage.color;
            storedCardImageColor = true;
        }

        if (cardButton != null && !storedButtonColorBlock)
        {
            cardButtonBaseColors = cardButton.colors;
            storedButtonColorBlock = true;
        }

        EnsureCombinedPowerImageAssigned();

        // CanvasGroup add karo fade out mate (agar nahi hoy to)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void SetOopsHidden(bool hidden)
    {
        oopsHiddenVisualsApplied = hidden;
        oopsCardOpenSent = false;

        if (hidden)
        {
            if (cardImage != null)
            {
                if (storedCardImageColor)
                {
                    cardImage.color = cardImageBaseColor;
                }
                else
                {
                    cardImage.color = Color.white;
                }
            }

            if (firstPowerText != null) firstPowerText.text = "";
            if (secondPowerText != null) secondPowerText.text = "";

            SetPowerSpriteInternal(combinedPowerImage, null);
        }
        else
        {
            if (cardImage != null && storedCardImageColor)
            {
                cardImage.color = cardImageBaseColor;
            }
        }
    }

    private void EnsureCombinedPowerImageAssigned()
    {
        if (combinedPowerImage != null)
        {
            // If mistakenly assigned to the main card image, treat as unassigned and create an overlay.
            if (cardImage != null && ReferenceEquals(combinedPowerImage, cardImage))
            {
                combinedPowerImage = null;
            }
            else
            {
                return;
            }
        }

        // Try find by common child names.
        Transform t = transform.Find("PowerSpriteImage");
        if (t == null) t = transform.Find("CombinedPowerImage");
        if (t != null)
        {
            combinedPowerImage = t.GetComponent<Image>();
            return;
        }

        // If no child overlay exists, create one at runtime so prefab doesn't need manual setup.
        GameObject go = new GameObject("PowerSpriteImage", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.localPosition = Vector3.zero;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.white;
        img.enabled = false; // enable only when sprite is assigned

        go.transform.SetAsLastSibling();
        combinedPowerImage = img;
    }

    void Start()
    {
        // Agar OpenCardHolder assign nahi kari hoy to automatically find karo
        if (openCardHolder == null && autoFindOpenCardHolder)
        {
            FindOpenCardHolder();
        }

        // Agar StartPosition assign nahi kari hoy to automatically find karo
        if (startPosition == null && autoFindStartPosition)
        {
            FindStartPosition();
        }
    }

    public void EnsureCardAnimationTargetsResolved()
    {
        if (openCardHolder == null && autoFindOpenCardHolder)
        {
            FindOpenCardHolder();
        }

        if (startPosition == null && autoFindStartPosition)
        {
            FindStartPosition();
        }
    }

    /// <summary>
    /// OpenCardHolder automatically find karo scene ma
    /// </summary>
    void FindOpenCardHolder()
    {
        // Pehle scene ma "OpenCardHolder" naam nu GameObject dhundho
        GameObject foundHolder = GameObject.Find("OpenCardHolder");
        
        if (foundHolder != null)
        {
            openCardHolder = foundHolder.GetComponent<RectTransform>();
            if (openCardHolder != null)
            {
                //Debug.Log("CardClickHandler: OpenCardHolder automatically found!");
            }
            else
            {
               // Debug.LogWarning("CardClickHandler: OpenCardHolder GameObject mali but RectTransform component nahi!");
            }
        }
        else
        {
            // Agar Find() thi nahi mali to Canvas ke andar search karo
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform[] allRects = canvas.GetComponentsInChildren<RectTransform>();
                foreach (var rect in allRects)
                {
                    if (rect.name == "OpenCardHolder")
                    {
                        openCardHolder = rect;
                       // Debug.Log("CardClickHandler: OpenCardHolder found in Canvas!");
                        break;
                    }
                }
            }

            if (openCardHolder == null)
            {
              //  Debug.LogWarning("CardClickHandler: OpenCardHolder nahi mali! Please manually assign karo.");
            }
        }
    }

    /// <summary>
    /// Starting Position automatically find karo scene ma
    /// </summary>
    void FindStartPosition()
    {
        // Pehle common names try karo
        string[] possibleNames = { "StartPointCard ANimtiion", "StartPointCardAnimation", "StartPosition", "StartPoint" };
        
        foreach (string name in possibleNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                startPosition = found.GetComponent<RectTransform>();
                if (startPosition != null)
                {
                    //Debug.Log($"CardClickHandler: StartPosition '{name}' automatically found!");
                    return;
                }
            }
        }

        // Agar Find() thi nahi mali to Canvas ke andar search karo
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform[] allRects = canvas.GetComponentsInChildren<RectTransform>();
            foreach (var rect in allRects)
            {
                foreach (string name in possibleNames)
                {
                    if (rect.name.Contains(name) || rect.name == name)
                    {
                        startPosition = rect;
                       // Debug.Log($"CardClickHandler: StartPosition found in Canvas as '{rect.name}'!");
                        return;
                    }
                }
            }
        }

        if (startPosition == null)
        {
           // Debug.LogWarning("CardClickHandler: StartPosition nahi mali! Please manually assign karo.");
        }
    }

    void OnDestroy()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnCardClicked);
        }
    }

    public void TriggerCardClick()
    {
        OnCardClickedInternal(true);
    }

    /// <summary>
    /// Card click par call thase
    /// </summary>
    void OnCardClicked()
    {
        OnCardClickedInternal(false);
    }

    void OnCardClickedInternal(bool bypassHumanSuppression)
    {
        GameManager gm = GameManager.Instance;

        if (!bypassHumanSuppression)
        {
            if (gm != null && gm.IsHumanInputSuppressed())
            {
                return;
            }
        }

        // Agar card clickable nahi hai to return (death card - click par kuch nahi hoga)
        if (!isCardClickable)
        {
            return; // Button interactable hai but click par koi kaam nahi
        }

        // Agar koi aur card already clickable hai to is card par click na thavu
        if (currentlyClickableCard != null && currentlyClickableCard != this)
        {
            return;
        }

        if (isAnimating)
        {
            return; // Animation already running
        }

        if (openCardHolder == null)
        {
           // Debug.LogWarning("CardClickHandler: OpenCardHolder assign nahi kari che!");
            return;
        }

        if (gm != null && gm.IsPlayWithOopsMode)
        {
            if (oopsWaitingForOpenResponse)
            {
                return;
            }

            if (!oopsCardOpenSent)
            {
                oopsCardOpenSent = true;
                oopsWaitingForOpenResponse = true;
            }
        }

        // Is card ko clickable mark karo aur biji cards disable karo
        SetCardClickable(true);
        currentlyClickableCard = this;

        // Card flip start thay to DeckShadow ma je cards che ae disable karo
        if (cardRectTransform.parent != null && cardRectTransform.parent.name.Contains("DeckShadow"))
        {
            CardClickHandler.DisableAllDeckShadowCards(cardRectTransform.parent);
            // But current card enable rakho
            SetCardClickable(true);
        }

        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Selection();
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.NotifyCardClickStarted();
        }

        if (gm != null && gm.IsPlayWithOopsMode)
        {
            gm.RequestOopsCardOpen(this);
            return;
        }

        StartCoroutine(FlipAndMoveCard());
    }

    public void OnOopsCardOpenResponse(string cardId, string power1, string power2, bool hasDualPower, int value1, int value2)
    {
        oopsWaitingForOpenResponse = false;
        oopsHiddenVisualsApplied = false;

        EnsureCardAnimationTargetsResolved();

        Debug.Log($"PlayWithOops: OnOopsCardOpenResponse target='{gameObject.name}' enabled={enabled} active={gameObject.activeInHierarchy} openHolder={(openCardHolder != null ? openCardHolder.name : "<null>")} startPos={(startPosition != null ? startPosition.name : "<null>")} power1='{power1}' power2='{power2}'");

        serverCardId = cardId ?? "";

        StoreCardPowerData(power1, power2, hasDualPower, -1, value1, value2);

        StartCoroutine(FlipAndMoveCard());
    }

    /// <summary>
    /// Card flip aur move animation
    /// Flip ke beech me hi rotation 0 aur scale 1.2 ho jase
    /// </summary>
    private IEnumerator FlipAndMoveCard()
    {
        isAnimating = true;

        EnsureCardAnimationTargetsResolved();

        if (cardRectTransform == null)
        {
            Debug.LogWarning($"CardClickHandler: FlipAndMoveCard aborted (missing RectTransform). target='{gameObject.name}'");
            isAnimating = false;
            yield break;
        }

        if (openCardHolder == null)
        {
            Debug.LogWarning($"CardClickHandler: FlipAndMoveCard aborted (OpenCardHolder not found). target='{gameObject.name}'");
            isAnimating = false;
            yield break;
        }

        Debug.Log($"CardClickHandler: FlipAndMoveCard START target='{gameObject.name}' openHolder='{openCardHolder.name}'");

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.SetCardAnimationLock(true);
        }

        // Store original values (for return animation)
        storedOriginalPosition = cardRectTransform.position;
        Vector3 originalRotationEuler = cardRectTransform.localRotation.eulerAngles;
        storedOriginalRotation = cardRectTransform.localRotation;
        storedOriginalScale = cardRectTransform.localScale;
        storedOriginalParent = cardRectTransform.parent;
        
        // Local variables for flip animation
        Vector3 originalPosition = storedOriginalPosition;
        Quaternion originalRotation = storedOriginalRotation;
        Vector3 originalScale = storedOriginalScale;
        Transform originalParent = storedOriginalParent;

        // Get current Z rotation (45 degrees ya koi bhi)
        float currentZRotation = originalRotationEuler.z;
        float targetZRotation = 0f; // Flip ke baad 0 degrees

        // Step 1: Flip Animation (Y-axis rotation) + Z rotation to 0 + Scale to 1.2
        float elapsed = 0f;
        while (elapsed < flipDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flipDuration / 2f);

            // Ease in-out
            t = t * t * (3f - 2f * t);

            // Flip to 90 degrees (Y-axis - card flip effect)
            float rotationY = Mathf.Lerp(0f, 90f, t);
            
            // Z rotation 45 to 0 (card straight ho jase)
            float rotationZ = Mathf.Lerp(currentZRotation, targetZRotation, t);
            
            cardRectTransform.localRotation = Quaternion.Euler(0, rotationY, rotationZ);

            // Scale lerp (1 to 1.2 during flip)
            cardRectTransform.localScale = Vector3.Lerp(originalScale, targetLocalScale, t);

            yield return null;
        }

        // Mid-point: Sprite change karo aur Text assign karo (card flip thay pachhi - 90 degrees par)
        if (cardImage != null && cardFrontSprite != null)
        {
            cardImage.sprite = cardFrontSprite;
        }

        if (cardImage != null && storedCardImageColor)
        {
            cardImage.color = cardImageBaseColor;
        }

        // Step 2: Complete flip (90 to 0 Y rotation) + ensure Z rotation is 0 + Scale 1.2
        elapsed = 0f;
        while (elapsed < flipDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flipDuration / 2f);

            // Ease in-out
            t = t * t * (3f - 2f * t);

            // Flip back from 90 to 0 (Y-axis - complete flip)
            float rotationY = Mathf.Lerp(90f, 0f, t);
            
            // Z rotation already 0 (ensure it stays 0)
            float rotationZ = Mathf.Lerp(targetZRotation, targetZRotation, t);
            
            cardRectTransform.localRotation = Quaternion.Euler(0, rotationY, rotationZ);

            // Scale already 1.2 (ensure it stays 1.2)
            cardRectTransform.localScale = Vector3.Lerp(targetLocalScale, targetLocalScale, t);

            yield return null;
        }

        // Flip complete - card ab straight hai (rotation 0) aur scale 1.2 hai
        cardRectTransform.localRotation = Quaternion.Euler(0, 0, 0);
        cardRectTransform.localScale = targetLocalScale;

        // Power sprite/text assign AFTER flip complete (avoid visible swap during flip)
        if (!string.IsNullOrEmpty(cardPower1))
        {
            SetCardPowerTextInternal(cardPower1, cardPower2, cardHasDualPower);
        }

        // Flip complete hone par card click disable karo (flip animation complete)
        SetCardClickable(false);

        // Step 3: Move to OpenCardHolder (card already straight aur scaled hai)
        Vector3 targetWorldPos = openCardHolder.TransformPoint(targetLocalPosition);
        
        elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;

            // Ease out curve
            t = 1f - Mathf.Pow(1f - t, 3f);

            // Position lerp (move to OpenCardHolder)
            cardRectTransform.position = Vector3.Lerp(originalPosition, targetWorldPos, t);

            // Scale already 1.2 (no change needed)

            yield return null;
        }

        // Step 4: Card nu OpenCardHolder ke andar move karo
        cardRectTransform.SetParent(openCardHolder);
        cardRectTransform.localPosition = targetLocalPosition;
        cardRectTransform.localScale = targetLocalScale; // Still 1.2 (moto)
        cardRectTransform.localRotation = Quaternion.Euler(0, 0, 0); // Final rotation 0

        // Step 5: Fast bounce effect - scale 1.2 -> 1.0 (nano) with bounce
        // Bounce effect: moto (1.2) -> nano (1.0) -> thoduk moto (bounceIntensity) -> nano (1.0)
        yield return StartCoroutine(PlayFastBounceEffect());

        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Medium();
        }

       // Debug.Log("Card flip and move complete! Card is now straight (rotation 0) and at actual size (1.0) in OpenCardHolder.");

        // Notify GameManager that card is picked
        NotifyGameManagerCardPicked();

        // Step 5: Card visible raheshe - automatic return nahi (piece move thay pachhi return thase)
        // Wait nahi karo - card visible raheshe until piece move thay
        isAnimating = false; // Animation complete, but card visible raheshe

        if (gm != null)
        {
            gm.SetCardAnimationLock(false);
        }
        
        yield break; // Card return nahi karo - piece move thay pachhi return thase
    }

    /// <summary>
    /// Fast bounce effect play karo (scale 1.2 -> 1.0 with bounce)
    /// </summary>
    private IEnumerator PlayFastBounceEffect()
    {
        // Fast bounce effect: moto (1.2) -> nano (1.0) -> thoduk moto (bounceIntensity) -> nano (1.0)
        Sequence bounceSequence = DOTween.Sequence();
        
        // Phase 1: Scale down from 1.2 to 1.0 (nano) - 40% of bounce duration
        float phase1Duration = bounceDuration * 0.4f;
        bounceSequence.Append(cardRectTransform.DOScale(actualSizeScale, phase1Duration)
            .SetEase(Ease.OutQuad));
        
        // Phase 2: Bounce up (1.0 -> bounceIntensity) - 30% of bounce duration
        float phase2Duration = bounceDuration * 0.3f;
        Vector3 bounceScale = actualSizeScale * bounceIntensity;
        bounceSequence.Append(cardRectTransform.DOScale(bounceScale, phase2Duration)
            .SetEase(Ease.OutQuad));
        
        // Phase 3: Final settle to actual size (bounceIntensity -> 1.0) - 30% of bounce duration
        float phase3Duration = bounceDuration * 0.3f;
        bounceSequence.Append(cardRectTransform.DOScale(actualSizeScale, phase3Duration)
            .SetEase(Ease.InQuad));

        yield return bounceSequence.WaitForCompletion(); // Wait for bounce sequence to complete
    }

    /// <summary>
    /// Card return karo starting position ma (piece move thay pachhi call thase)
    /// </summary>
    public void ReturnCardToStart()
    {
        if (isAnimating)
        {
            //Debug.LogWarning("Card is already animating, cannot return!");
            return;
        }
        
        StartCoroutine(ReturnCardCoroutine());
    }

    /// <summary>
    /// Card return animation coroutine (piece move thay pachhi)
    /// </summary>
    private IEnumerator ReturnCardCoroutine()
    {
        isAnimating = true;
        
        // Step 6: Fade out aur starting position ma return karo
        // Agar startPosition assign nahi kari hoy to find karo
        if (startPosition == null && autoFindStartPosition)
        {
            FindStartPosition();
        }

        // Target position determine karo (startPosition ya originalPosition)
        Vector3 targetReturnPosition;
        RectTransform targetReturnParent = null;

        if (startPosition != null)
        {
            targetReturnPosition = startPosition.position;
            targetReturnParent = startPosition;
        }
        else if (storedOriginalParent != null)
        {
            // Fallback to original position
            targetReturnPosition = storedOriginalPosition;
            targetReturnParent = storedOriginalParent as RectTransform;
        }
        else
        {
            targetReturnPosition = storedOriginalPosition;
        }

        Vector3 currentPosition = cardRectTransform.position;
        Vector3 currentScale = cardRectTransform.localScale;
        float startAlpha = canvasGroup.alpha;
        float startRotationZ = cardRectTransform.localRotation.eulerAngles.z;
        float endRotationZ = startRotationZ + (360f * returnSpinRotations); // Multiple rotations

        float elapsed = 0f;
        while (elapsed < returnAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnAnimationDuration;

            // Ease out curve
            t = 1f - Mathf.Pow(1f - t, 2f);

            // Fade out (transparent ho jase)
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            // Position lerp (startPosition ma return - curved path)
            cardRectTransform.position = Vector3.Lerp(currentPosition, targetReturnPosition, t);

            // Scale lerp (back to original scale)
            cardRectTransform.localScale = Vector3.Lerp(currentScale, storedOriginalScale, t);

            // Rotation spin (card ghumi ne jay - multiple rotations)
            float currentRotationZ = Mathf.Lerp(startRotationZ, endRotationZ, t);
            cardRectTransform.localRotation = Quaternion.Euler(0, 0, currentRotationZ);

            yield return null;
        }

        // Step 7: Card nu startPosition ke andar move karo (child bano)
        if (targetReturnParent != null)
        {
            cardRectTransform.SetParent(targetReturnParent);
            cardRectTransform.localPosition = Vector3.zero; // StartPosition ke center ma
        }
        else if (storedOriginalParent != null)
        {
            cardRectTransform.SetParent(storedOriginalParent);
            cardRectTransform.position = storedOriginalPosition;
        }

        // Final values set karo
        cardRectTransform.localScale = storedOriginalScale;
        cardRectTransform.localRotation = Quaternion.identity; // Reset rotation
        canvasGroup.alpha = 0f; // Fully transparent

        // Step 8: Card startPosition par pahunchi jay pachhi DeckShadow ka last card enable karo
        if (storedOriginalParent != null && storedOriginalParent.name.Contains("DeckShadow"))
        {
            // Pehle sab cards disable karo
            CardClickHandler.DisableAllDeckShadowCards(storedOriginalParent);
            
            // Phir last card enable karo (next card clickable)
            CardClickHandler.EnableLastDeckShadowCard(storedOriginalParent);

            if (storedOriginalParent.childCount == 0)
            {
                CardDeckAnimator deckAnimator = FindObjectOfType<CardDeckAnimator>();
                if (deckAnimator != null)
                {
                    deckAnimator.ResetShuffleAndReplay();
                }
            }
        }

        // Step 9: DeckShadow ma return thayu hoy to card active/visible rakhvu.
        // StartPosition ma return thayu hoy (DeckShadow no hoy) to card disable kari devu.
        // IMPORTANT: storedOriginalParent can be DeckShadow even if we re-parented the card to startPosition.
        // Use the *current* parent to decide visibility.
        bool returnedToDeckShadow = (cardRectTransform != null
            && cardRectTransform.parent != null
            && cardRectTransform.parent.name.Contains("DeckShadow"));
        if (returnedToDeckShadow)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (cardImage != null)
            {
                if (cardBackSprite != null)
                {
                    cardImage.sprite = cardBackSprite;
                }
            }

            if (firstPowerText != null) firstPowerText.text = "";
            if (secondPowerText != null) secondPowerText.text = "";
        }
        else
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (cardImage != null)
            {
                if (cardBackSprite != null)
                {
                    cardImage.sprite = cardBackSprite;
                }
            }

            if (combinedPowerImage != null)
            {
                combinedPowerImage.sprite = null;
                combinedPowerImage.enabled = false;
            }

            if (firstPowerText != null) firstPowerText.text = "";
            if (secondPowerText != null) secondPowerText.text = "";

            SetCardClickable(false);
        }

        if (cardImage != null && storedCardImageColor)
        {
            cardImage.color = cardImageBaseColor;
        }

        if (cardButton != null)
        {
            cardButton.interactable = true;
            if (storedButtonColorBlock)
            {
                cardButton.colors = cardButtonBaseColors;
            }
        }

        // Currently clickable card clear karo
        if (currentlyClickableCard == this)
        {
            currentlyClickableCard = null;
        }

        if (HapticsManager.Instance != null)
        {
          //  HapticsManager.Instance.Light();
        }

        isAnimating = false;

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.SetCardAnimationLock(false);
        }

        Debug.Log(returnedToDeckShadow
            ? "Card returned to DeckShadow and is active!"
            : "Card returned to starting position and disabled!");
    }

    /// <summary>
    /// Card clickable enable/disable karo
    /// Button interactable rahega but internal flag se control karo
    /// </summary>
    public void SetCardClickable(bool clickable)
    {
        isCardClickable = clickable; // Internal flag set karo

        // Button interactable always true rakho (UI par clickable dikhe)
        // But actual click functionality internal flag se control thase
        if (cardButton != null)
        {
            cardButton.interactable = true; // Always true - UI par clickable dikhe
        }

        if (clickable)
        {
            currentlyClickableCard = this;
        }
        else if (currentlyClickableCard == this)
        {
            currentlyClickableCard = null;
        }
    }

    /// <summary>
    /// DeckShadow ma je cards che ae disable karo (last card startPosition par pahunchi jay tya sudhi)
    /// </summary>
    public static void DisableAllDeckShadowCards(Transform deckShadowParent)
    {
        if (deckShadowParent == null) return;

        foreach (Transform child in deckShadowParent)
        {
            CardClickHandler clickHandler = child.GetComponent<CardClickHandler>();
            if (clickHandler != null)
            {
                clickHandler.SetCardClickable(false); // Internal flag false - click par kuch nahi hoga
            }
        }
    }

    /// <summary>
    /// DeckShadow ka last card enable karo (clickable banavo)
    /// </summary>
    public static void EnableLastDeckShadowCard(Transform deckShadowParent)
    {
        if (deckShadowParent == null || deckShadowParent.childCount == 0) return;

        // Last child (last card) enable karo
        Transform lastChild = deckShadowParent.GetChild(deckShadowParent.childCount - 1);
        CardClickHandler lastCardHandler = lastChild.GetComponent<CardClickHandler>();
        if (lastCardHandler != null)
        {
            lastCardHandler.SetCardClickable(true);
            currentlyClickableCard = lastCardHandler;
            Debug.Log($"Last card in DeckShadow is now clickable! Power1: {lastCardHandler.cardPower1}, Power2: {lastCardHandler.cardPower2}, Dual: {lastCardHandler.cardHasDualPower}");
        }
    }

    /// <summary>
    /// Get currently clickable card (last card) - testing mate
    /// </summary>
    public static CardClickHandler GetLastClickableCard()
    {
        return currentlyClickableCard;
    }

    /// <summary>
    /// Print current card power info (testing mate - Inspector ma context menu thi call kari shaksho)
    /// </summary>
    [ContextMenu("Print Card Power Info")]
    public void PrintCardPowerInfo()
    {
        Debug.Log($"=== CARD POWER INFO (Inspector Verification) ===");
        Debug.Log($"Card Name: {gameObject.name}");
        Debug.Log($"Card Power 1: '{cardPower1}' (Length: {cardPower1?.Length ?? 0})");
        Debug.Log($"Card Power 2: '{cardPower2}' (Length: {cardPower2?.Length ?? 0})");
        Debug.Log($"Card Has Dual Power: {cardHasDualPower}");
        Debug.Log($"=== END CARD POWER INFO ===");
    }

    /// <summary>
    /// Force refresh card power values (testing mate - Inspector ma context menu thi call kari shaksho)
    /// </summary>
    [ContextMenu("Force Refresh Power Values")]
    public void ForceRefreshPowerValues()
    {
        // Force Unity to serialize these values
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
        
        Debug.Log($"Card '{gameObject.name}' - Power1: '{cardPower1}', Power2: '{cardPower2}', Dual: {cardHasDualPower}");
    }

    /// <summary>
    /// Get card power info as string (testing mate)
    /// </summary>
    public string GetCardPowerInfoString()
    {
        return $"Power1: '{cardPower1}', Power2: '{cardPower2}', Dual: {cardHasDualPower}";
    }
    
    /// <summary>
    /// Card name ma power info add karo (Hierarchy ma visible raheshe)
    /// Example: "Card [1] - Move +5" or "Card [2] - SORRY! / Attack card"
    /// </summary>
    public void UpdateCardNameWithPower(int cardIndex = -1)
    {
        if (string.IsNullOrEmpty(cardPower1))
        {
            Debug.LogWarning($"⚠️ UpdateCardNameWithPower: cardPower1 is empty for card '{gameObject.name}', Index: {cardIndex}");
            return; // Power assign nahi thayu to name change nahi karo
        }
        
        // Agar cardIndex pass nahi thayu to parent ma find karo
        if (cardIndex <= 0)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent.GetChild(i) == transform)
                    {
                        cardIndex = i + 1; // 1-based index (1, 2, 3...)
                        break;
                    }
                }
            }
        }

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.IsPlayWithOopsMode)
        {
            if (cardIndex > 0)
            {
                gameObject.name = $"Card [{cardIndex}]";
            }
            else
            {
                gameObject.name = "Card";
            }
            return;
        }
        
        // Power text format karo
        string powerText = cardPower1;
        if (cardHasDualPower && !string.IsNullOrEmpty(cardPower2))
        {
            powerText = $"{cardPower1} / {cardPower2}";
        }
        
        // Card name update karo - ALWAYS update, even if index is 0
        if (cardIndex > 0)
        {
            string newName = $"Card [{cardIndex}] - {powerText}";
            gameObject.name = newName;
            
            // Debug log for first card
            if (cardIndex == 1)
            {
                Debug.Log($"✅ UpdateCardNameWithPower: First card name set to '{newName}'");
            }
        }
        else if (!string.IsNullOrEmpty(powerText))
        {
            // Index nahi mali to bhi power text sathe name update karo
            gameObject.name = $"Card - {powerText}";
            Debug.LogWarning($"⚠️ UpdateCardNameWithPower: Card index not found, using power only: '{gameObject.name}'");
        }
    }
    
    /// <summary>
    /// Update Inspector display (called when values change)
    /// </summary>
    void OnValidate()
    {
        // This will be called in editor when values change
        // But during runtime, Inspector might not update
    }

    /// <summary>
    /// Manually card sprite set karo
    /// </summary>
    public void SetCardSprite(Sprite sprite)
    {
        cardFrontSprite = sprite;
        if (cardImage != null && sprite != null)
        {
            // Optionally set immediately or wait for flip
            // cardImage.sprite = sprite;
        }
    }

    public Sprite ResolveCardFaceSpriteFromServer(string power1, string power2, bool hasDualPower, int spriteVariant)
    {
        PowerSpriteMapping[] activeMappings = GetActivePowerSpriteMappings();
        if (activeMappings == null || activeMappings.Length == 0)
        {
            return null;
        }

        string p1 = power1 ?? string.Empty;
        string p2 = power2 ?? string.Empty;

        string keyPrimary = NormalizePowerKey(hasDualPower ? (p1 + " / " + p2) : p1);
        string keyAlt = NormalizePowerKey(hasDualPower ? (p1 + "|" + p2) : p1);

        for (int i = 0; i < activeMappings.Length; i++)
        {
            PowerSpriteMapping m = activeMappings[i];
            if (m == null) continue;
            if (string.IsNullOrWhiteSpace(m.powerText)) continue;

            string k = NormalizePowerKey(m.powerText);
            if (string.IsNullOrWhiteSpace(k)) continue;

            if (k != keyPrimary && k != keyAlt) continue;

            Sprite chosen = null;
            if (spriteVariant == 1) chosen = m.player1Sprite;
            else if (spriteVariant == 2) chosen = m.player2Sprite;
            else if (spriteVariant == 3) chosen = m.player3Sprite;
            else if (spriteVariant == 4) chosen = m.player4Sprite;

            if (chosen != null)
            {
                return chosen;
            }

            return m.sprite;
        }

        return null;
    }

    /// <summary>
    /// Card power data store karo (CardDeckAnimator thi call thase)
    /// Flip ke time text assign thase (90 degrees par)
    /// </summary>
    public void StoreCardPowerData(string power1, string power2 = "", bool hasDualPower = false, int cardIndex = -1, int value1 = 0, int value2 = 0)
    {
        // Store values
        cardPower1 = power1 ?? ""; // Null check
        cardPower2 = power2 ?? ""; // Null check
        cardHasDualPower = hasDualPower;

        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
        
        // Debug log for testing
      //  Debug.Log($"Card '{gameObject.name}' power stored - Power1: '{cardPower1}', Power2: '{cardPower2}', Dual: {cardHasDualPower}");
        
        // SORRY card mate special check
        if (cardPower1 == "SORRY!" || cardPower1.Contains("SORRY"))
        {
           /* Debug.Log($"=== SORRY CARD DETECTED in StoreCardPowerData ===");
            Debug.Log($"Card Name: {gameObject.name}");
            Debug.Log($"Power1: '{cardPower1}'");
            Debug.Log($"Power2: '{cardPower2}'");
            Debug.Log($"Has Dual Power: {cardHasDualPower}");
            Debug.Log($"=== END SORRY CARD CHECK ===");*/
        }
        
        // Initially text empty rakho (flip ke time assign thase)
        if (firstPowerText != null)
        {
            firstPowerText.text = "";
        }
        if (secondPowerText != null)
        {
            secondPowerText.text = "";
        }

        // Also clear sprites (if used)
        SetPowerSpriteInternal(combinedPowerImage, null);
    }

    /// <summary>
    /// Card power text set karo (internal method - flip ke midpoint par call thase)
    /// </summary>
    private void SetCardPowerTextInternal(string power1, string power2 = "", bool hasDualPower = false)
    {
        EnsureCombinedPowerImageAssigned();

        bool spriteApplied = false;
        if (hasDualPower)
        {
            spriteApplied = TryApplyCombinedPowerSprite(power1, power2);
        }
        else
        {
            spriteApplied = TryApplySinglePowerSprite(power1);
        }

        // If combined sprite applied, hide texts.
        if (spriteApplied)
        {
            if (firstPowerText != null) firstPowerText.enabled = false;
            if (secondPowerText != null) secondPowerText.enabled = false;
            return;
        }

        // Otherwise hide combined image and show texts normally.
        SetPowerSpriteInternal(combinedPowerImage, null);
        if (firstPowerText != null) firstPowerText.enabled = true;
        if (secondPowerText != null) secondPowerText.enabled = true;

        // First power text set karo
        if (firstPowerText != null)
        {
            firstPowerText.text = power1;
        }
        else
        {
            Debug.LogWarning("CardClickHandler: FirstPowerText not assigned!");
        }

        // Second power text set karo (agar dual power hoy to)
        if (secondPowerText != null)
        {
            if (hasDualPower && !string.IsNullOrEmpty(power2))
            {
                secondPowerText.text = power2;
            }
            else
            {
                secondPowerText.text = ""; // Empty karo agar single power card hoy to
            }
        }
        else if (hasDualPower)
        {
            Debug.LogWarning("CardClickHandler: SecondPowerText not assigned but card has dual power!");
        }
    }

    private bool TryApplySinglePowerSprite(string power1)
    {
        EnsureCombinedPowerImageAssigned();
        if (combinedPowerImage == null)
        {
            return false;
        }

        Sprite sprite = FindSpriteForPower(power1);
        if (sprite == null)
        {
            SetPowerSpriteInternal(combinedPowerImage, null);
            return false;
        }

        SetPowerSpriteInternal(combinedPowerImage, sprite);
        return true;
    }

    private bool TryApplyCombinedPowerSprite(string power1, string power2)
    {
        EnsureCombinedPowerImageAssigned();
        if (combinedPowerImage == null)
        {
            return false;
        }

        // Use the same single mapping list; key can be "power1/power2" or "power1|power2".
        Sprite sprite = FindSpriteForPower(power1 + "|" + power2);
        if (sprite == null)
        {
            SetPowerSpriteInternal(combinedPowerImage, null);
            return false;
        }

        SetPowerSpriteInternal(combinedPowerImage, sprite);
        return true;
    }

    private void SetPowerSpriteInternal(Image img, Sprite sprite)
    {
        if (img == null) return;

        // Safety: Don't allow power overlay sprite assignment to overwrite the card face image.
        if (cardImage != null && ReferenceEquals(img, cardImage))
        {
            Debug.LogWarning("CardClickHandler: CombinedPowerImage is pointing to the main card Image. Please assign a separate child Image for power sprites. Skipping sprite assignment to avoid hiding the card front.");
            return;
        }

        img.sprite = sprite;
        img.enabled = (sprite != null);
    }

    private Sprite FindSpriteForPower(string power)
    {
        PowerSpriteMapping[] activeMappings = GetActivePowerSpriteMappings();
        if (string.IsNullOrWhiteSpace(power) || activeMappings == null)
        {
            return null;
        }

        // Highest priority: match against this card's GameObject name (Hierarchy shows power in the name).
        // This lets you use mapping keys exactly as they appear in names like:
        // "Card [14] - Move +7 / OR Split"
        Sprite byName = FindSpriteByCardName();
        if (byName != null)
        {
            return byName;
        }

        string key = NormalizePowerKey(power);
        Sprite direct = FindSpriteByNormalizedKey(key);
        if (direct != null)
        {
            return direct;
        }

        // Fallback: allow simplified keys like "+1" even if runtime string is "Move +1".
        // Also works for "Move -4", etc.
        bool isSorry = power.IndexOf("SORRY", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isSorry)
        {
            Sprite sorrySprite = FindSpriteByNormalizedKey(NormalizePowerKey("SORRY!"));
            if (sorrySprite != null)
            {
                return sorrySprite;
            }
        }

        int value = GameManager.ExtractCardValue(power);
        if (value != 0)
        {
            string shortKey = (value > 0) ? ("+" + value) : value.ToString();
            Sprite shortSprite = FindSpriteByNormalizedKey(NormalizePowerKey(shortKey));
            if (shortSprite != null)
            {
                return shortSprite;
            }
        }

        return null;
    }

    private PowerSpriteMapping[] GetActivePowerSpriteMappings()
    {
        GameManager gm = GameManager.Instance;
        int count = 0;
        if (gm != null)
        {
            count = gm.GetActivePlayerCountPublic();
        }

        if (count <= 2 && powerSpriteMappingsForTwoPlayers != null && powerSpriteMappingsForTwoPlayers.Length > 0)
        {
            return powerSpriteMappingsForTwoPlayers;
        }

        return powerSpriteMappings;
    }

    private Sprite FindSpriteByCardName()
    {
        PowerSpriteMapping[] activeMappings = GetActivePowerSpriteMappings();
        if (activeMappings == null)
        {
            return null;
        }

        int spriteVariant = 0;
        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            spriteVariant = gm.GetCardSpriteVariantForCurrentPlayer();
        }

        string n = gameObject != null ? gameObject.name : string.Empty;
        if (string.IsNullOrWhiteSpace(n))
        {
            return null;
        }

        string normalizedName = NormalizePowerKey(n);
        Sprite bestSprite = null;
        int bestNeedleLength = -1;

        for (int i = 0; i < activeMappings.Length; i++)
        {
            PowerSpriteMapping m = activeMappings[i];
            if (m == null) continue;

            if (string.IsNullOrWhiteSpace(m.powerText)) continue;

            string needle = NormalizePowerKey(m.powerText);
            if (string.IsNullOrWhiteSpace(needle)) continue;

            if (normalizedName.Contains(needle))
            {
                Sprite chosen = null;
                if (spriteVariant == 1) chosen = m.player1Sprite;
                else if (spriteVariant == 2) chosen = m.player2Sprite;
                else if (spriteVariant == 3) chosen = m.player3Sprite;
                else if (spriteVariant == 4) chosen = m.player4Sprite;

                if (chosen == null)
                {
                    chosen = m.sprite;
                }

                if (chosen == null)
                {
                    continue;
                }

                // Prefer the most specific mapping (longest match) so "MOVE+1" won't beat "MOVE+10|OR-1BACKWARD".
                if (needle.Length > bestNeedleLength)
                {
                    bestNeedleLength = needle.Length;
                    bestSprite = chosen;
                }
            }
        }

        return bestSprite;
    }

    private Sprite FindSpriteByNormalizedKey(string normalizedKey)
    {
        PowerSpriteMapping[] activeMappings = GetActivePowerSpriteMappings();
        if (string.IsNullOrWhiteSpace(normalizedKey) || activeMappings == null)
        {
            return null;
        }

        int spriteVariant = 0;
        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            spriteVariant = gm.GetCardSpriteVariantForCurrentPlayer();
        }

        for (int i = 0; i < activeMappings.Length; i++)
        {
            PowerSpriteMapping m = activeMappings[i];
            if (m == null) continue;

            if (!string.IsNullOrWhiteSpace(m.powerText) && NormalizePowerKey(m.powerText) == normalizedKey)
            {
                Sprite chosen = null;
                if (spriteVariant == 1) chosen = m.player1Sprite;
                else if (spriteVariant == 2) chosen = m.player2Sprite;
                else if (spriteVariant == 3) chosen = m.player3Sprite;
                else if (spriteVariant == 4) chosen = m.player4Sprite;

                if (chosen != null)
                {
                    return chosen;
                }

                return m.sprite;
            }
        }

        return null;
    }

    private string NormalizePowerKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        // Normalize to a stable key (case-insensitive, ignore spaces)
        // Examples:
        // "Move +10" => "MOVE+10"
        // "OR -1 backward" => "OR-1BACKWARD"
        // "SORRY!" => "SORRY!"
        return s.Trim()
            .Replace(" ", "")
            .Replace("/", "|")
            .Replace("\\", "|")
            .ToUpperInvariant();
    }

    /// <summary>
    /// GameManager ne notify karo ki card pick thayu che
    /// </summary>
    private void NotifyGameManagerCardPicked()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            // SORRY! card should always be treated as a special card by GameManager.
            // Do NOT extract +4 from power2 here; GameManager will decide fallback +4 if Option1 isn't possible.
            bool isSorryCard = (!string.IsNullOrEmpty(cardPower1) && cardPower1.Contains("SORRY"))
                || (!string.IsNullOrEmpty(cardPower2) && cardPower2.Contains("SORRY"));

            int cardValue;
            if (isSorryCard)
            {
                cardValue = 0;
            }
            else
            {
                cardValue = cardValue1;
                if (cardValue == 0)
                {
                    cardValue = GameManager.ExtractCardValue(cardPower1);

                    if (cardValue == 0 && !string.IsNullOrEmpty(cardPower2))
                    {
                        cardValue = GameManager.ExtractCardValue(cardPower2);
                    }
                }
            }

            // GameManager ne notify karo
            gameManager.OnCardPicked(this, cardValue);
        }
        else
        {
            Debug.LogWarning("CardClickHandler: GameManager not found!");
        }
    }
}

