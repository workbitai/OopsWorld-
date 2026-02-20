/*
 * Player Piece - Player na kukri (piece) handle karva mate
 * Piece click par movement handle kare che
 */

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using My.UI;

public class PlayerPiece : MonoBehaviour
{
    [Header("Player Info")]
    [Tooltip("Player number (1 ya 2)")]
    public int playerNumber = 1;

    [Tooltip("Piece number (1, 2, ya 3)")]
    public int pieceNumber = 1;

    public string pawnId = string.Empty;

    [Header("References")]
    [Tooltip("PlayerPathManager reference (auto find karse agar nahi assign)")]
    public PlayerPathManager pathManager;

    [Tooltip("GameManager reference (auto find karse agar nahi assign)")]
    public GameManager gameManager;


    [Header("Animation Settings")]
    [Tooltip("Step-by-step jump height (path par move karta)")]
    [Range(0f, 100f)]
    public float stepJumpHeight = 20f;

    [Range(0.08f, 0.35f)]
    public float stepJumpDuration = 0.18f;

    [Range(0.5f, 2.5f)]
    public float jumpEffectMultiplier = 1.4f;

    [Header("Final Home Animation")]
    [Range(1f, 3.5f)]
    public float finalHomeJumpMultiplier = 1.8f;

    [Range(0.6f, 2.0f)]
    public float finalHomeDurationMultiplier = 1.2f;

    [Range(0f, 1080f)]
    public float finalHomeSpinDegrees = 360f;

    [Range(0f, 0.6f)]
    public float finalHomePreDelay = 0.12f;

    public bool finalHomeUseScaleEffects = true;

    [Tooltip("Delay between steps (seconds)")]
    [Range(0f, 0.5f)]
    public float delayBetweenSteps = 0.15f;

    [Header("Start Exit Animation")]
    [Tooltip("When piece is at home/start, wait this long after click before jumping out to the first tile")]
    [Range(0f, 1.5f)]
    public float leaveStartDelay = 0.25f;

    [Tooltip("Spin degrees applied while leaving start to the first tile (0 = no spin)")]
    [Range(0f, 1440f)]
    public float leaveStartSpinDegrees = 360f;

    [Tooltip("Object minimum scale (landing par object chhota thase - e.g., 0.9)")]
    [Range(0.1f, 1f)]
    public float objectMinScale = 0.9f;

    [Tooltip("Object maximum scale (original size - e.g., 1.0)")]
    [Range(0.1f, 2f)]
    public float objectMaxScale = 1.0f;

    [Tooltip("Object bounce duration (seconds)")]
    [Range(0.1f, 1f)]
    public float objectBounceDuration = 0.2f;

    // Current position index in path (-1 = at home/start, 0+ = on path)
    private int currentPathIndex = -1;

    // Reference to current position transform
    private Transform currentPositionTransform = null;

    private Transform homeTransform = null;


    // Image component for color change
    private Image pieceImage;
    private SpriteRenderer pieceSpriteRenderer;

    private Rigidbody2D pieceRigidbody2D;

    public void ApplyServerPawnId(int pawnNumber, string pawnObjectId)
    {
        pawnId = pawnObjectId ?? string.Empty;
        if (pawnNumber >= 1 && pawnNumber <= 3)
        {
            pieceNumber = pawnNumber;
        }
    }

    public void ApplyServerBaseState()
    {
        ReturnToHome();
        SyncCurrentPathIndexFromTransform();
        SetClickable(false);
    }

    public void ApplyServerPathIndexState(int pathIndex)
    {
        if (pathManager == null)
        {
            return;
        }

        Transform t = pathManager.GetPathPosition(playerNumber, pathIndex);
        if (t == null)
        {
            return;
        }

        ForceSetPosition(t, pathIndex);
        SyncCurrentPathIndexFromTransform();
        SetClickable(false);
    }

    public void MovePieceToPathIndex(int pathIndex)
    {
        if (pathManager == null)
        {
            return;
        }

        SyncCurrentPathIndexFromTransform();

        Transform t = pathManager.GetPathPosition(playerNumber, pathIndex);
        if (t == null)
        {
            return;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.NotifyMoveStarted(this, 0);
        }

        StartCoroutine(MoveToDestination(t, pathIndex, 0));
    }

    public void MovePieceToPathIndex(int pathIndex, int originalSteps)
    {
        if (pathManager == null)
        {
            return;
        }

        SyncCurrentPathIndexFromTransform();

        Transform t = pathManager.GetPathPosition(playerNumber, pathIndex);
        if (t == null)
        {
            Debug.LogError($"Position not found at index {pathIndex}");
            return;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.NotifyMoveStarted(this, originalSteps);
        }

        StartCoroutine(MoveToDestination(t, pathIndex, originalSteps));
    }

    public void MovePieceToPathIndexWithServerSlide(int baseIndex, int finalIndex, int originalSteps)
    {
        if (pathManager == null)
        {
            return;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (isMoving)
        {
            return;
        }

        StartCoroutine(MoveToBaseThenServerSlide(baseIndex, finalIndex, originalSteps));
    }

    private void SetPieceVisualActive(bool active)
    {
        if (pieceImage != null)
        {
            pieceImage.enabled = active;
            return;
        }

        if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.enabled = active;
        }
    }

    private void SetPieceParent(Transform parent)
    {
        if (pieceImage != null)
        {
            RectTransform rt = transform as RectTransform;
            if (rt != null)
            {
                rt.SetParent(parent, false);
                ApplyAnchorsForParent(rt, parent);
                return;
            }
        }

        transform.SetParent(parent);
    }

    private void ApplyAnchorsForParent(RectTransform rt, Transform parent)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector3 p = rt.anchoredPosition3D;
        p.x = 0f;
        p.y = 0f;
        rt.anchoredPosition3D = p;
    }

    private void ApplyPieceLocalPosition(Vector3 localPos)
    {
        if (pieceImage != null)
        {
            RectTransform rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition3D = localPos;
                return;
            }
        }

        transform.localPosition = localPos;
    }

    private Transform GetMovementRoot()
    {
        if (pieceImage == null)
        {
            return null;
        }

        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        if (canvases == null || canvases.Length == 0)
        {
            return null;
        }

        for (int i = canvases.Length - 1; i >= 0; i--)
        {
            Canvas c = canvases[i];
            if (c == null) continue;
            if (c.transform == transform) continue;
            return c.transform;
        }

        return null;
    }

    // Movement animation
    private bool isMoving = false;

    private static bool isCard11SwapAnimating = false;

    private static bool isOopsBumpAnimating = false;

    private static bool isOopsKillReturnAnimating = false;

    private static bool isOopsSorryReplaceAnimating = false;

    [Header("Card 11 Swap Animation")]
    [Range(0.05f, 1.25f)]
    [SerializeField] private float card11SwapDuration = 0.35f;

    [Range(0f, 250f)]
    [SerializeField] private float card11SwapCurveAmount = 55f;

    private bool isSliding = false;

    public bool IsBusy => isMoving || isSliding;

    private Tween turnHighlightTween;
    private Vector3 turnHighlightBaseScale;
    private bool turnHighlightBaseScaleStored = false;
    private Color turnHighlightBaseColor = Color.white;
    private bool turnHighlightBaseColorStored = false;

    [Header("Turn Highlight Glow")]
    [SerializeField] private Transform turnGlow;
    private Vector3 turnGlowBaseScale = Vector3.one;
    private bool turnGlowBaseScaleStored = false;
    private Animator turnGlowAnimator;

    private Vector3 pauseStoredScale;
    private bool pauseScaleStored = false;

    private Coroutine slideHapticsCoroutine;

    // Button component for click handling
    private Button pieceButton;
    private Collider2D pieceCollider;

    [Header("Debug")]
    [SerializeField] private string inspectorStatus;
    [SerializeField] private bool debugPrintMoveDestinations = false;
    [SerializeField] private bool debugPrintHighlightedDestinationNames = false;

    // Split mode destination highlighting
    private List<Transform> highlightedDestinations = new List<Transform>();
    private List<GameObject> destinationHighlightObjects = new List<GameObject>();
    
    // Static dictionary to track which pieces have highlighted each destination (destination -> list of pieces)
    private static Dictionary<Transform, List<PlayerPiece>> destinationToPieces = new Dictionary<Transform, List<PlayerPiece>>();

    [Header("Destination Highlight")]
    [SerializeField] private Color destinationHighlightColor = new Color(0f, 1f, 0f, 0.35f);

    public bool IsClickable
    {
        get
        {
            if (pieceButton != null) return pieceButton.interactable;
            if (pieceCollider != null) return pieceCollider.enabled;
            return false;
        }
    }

    public static IEnumerable<Transform> GetHighlightedDestinationTransforms()
    {
        return destinationToPieces.Keys;
    }

    // Track original colors so we can restore when highlight is cleared
    private static Dictionary<Transform, Color> destinationToOriginalImageColor = new Dictionary<Transform, Color>();
    private static Dictionary<Transform, Color> destinationToOriginalSpriteColor = new Dictionary<Transform, Color>();

    private GameObject opponentClickableGlowInstance;
    private GameObject opponentClickableRingInstance;
    private Tween opponentClickableRingTween;

    public bool IsSwapTargetHighlighted { get; private set; }

    void Awake()
    {
        // Image component find karo (UI piece mate)
        pieceImage = GetComponent<Image>();
        
        // SpriteRenderer find karo (2D sprite piece mate)
        if (pieceImage == null)
        {
            pieceSpriteRenderer = GetComponent<SpriteRenderer>();
        }


        // Button component add karo (UI piece mate)
        if (pieceImage != null)
        {
            pieceButton = GetComponent<Button>();
            if (pieceButton == null)
            {
                pieceButton = gameObject.AddComponent<Button>();
            }
            pieceButton.onClick.AddListener(OnPieceClicked);
        }
        else
        {
            // 2D sprite piece mate Collider2D add karo
            pieceCollider = GetComponent<Collider2D>();
            if (pieceCollider == null)
            {
                pieceCollider = gameObject.AddComponent<BoxCollider2D>();
            }
        }

        pieceRigidbody2D = GetComponent<Rigidbody2D>();

        ResolveTurnGlow();
    }

    private void ResolveTurnGlow()
    {
        if (turnGlow == null)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t == transform) continue;

                string n = t.name;
                if (string.IsNullOrEmpty(n)) continue;

                if (n.ToLowerInvariant().Contains("glow"))
                {
                    turnGlow = t;
                    break;
                }
            }
        }

        if (turnGlow != null && !turnGlowBaseScaleStored)
        {
            turnGlowBaseScale = turnGlow.localScale;
            turnGlowBaseScaleStored = true;
            turnGlow.gameObject.SetActive(false);
            turnGlowAnimator = turnGlow.GetComponent<Animator>();
        }
    }

    void LateUpdate()
    {
        if (pieceRigidbody2D != null)
        {
            pieceRigidbody2D.linearVelocity = Vector2.zero;
            pieceRigidbody2D.angularVelocity = 0f;
        }

        if (!isMoving)
        {
            ForceZeroLocalXYPreserveZ();
        }
    }

    private void ForceZeroLocalXYPreserveZ()
    {
        if (pieceImage != null)
        {
            RectTransform rt = transform as RectTransform;
            if (rt != null)
            {
                Vector3 p = rt.anchoredPosition3D;
                p.x = 0f;
                p.y = 0f;
                rt.anchoredPosition3D = p;
                return;
            }
        }

        Vector3 lp = transform.localPosition;
        lp.x = 0f;
        lp.y = 0f;
        transform.localPosition = lp;
    }

    void Start()
    {
        // Auto find references
        if (pathManager == null)
        {
            pathManager = FindObjectOfType<PlayerPathManager>();
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        // Initial position set karo (home/start position)
        currentPathIndex = -1;
        
        // NOTE: Piece visibility GameManager handle karse (HideAllPieces/ShowCurrentPlayerPieces)
        // Ahiya hide nahi kariye - GameManager Start() ma hide thase
    }

    void Update()
    {
        inspectorStatus = GetStatusString();
    }

    /// <summary>
    /// Piece click handler - card value anusar move karo
    /// </summary>
    public void OnPieceClicked()
    {
        OnPieceClickedInternal(false);
    }

    public void TriggerBotPieceClick()
    {
        OnPieceClickedInternal(true);
    }

    void OnPieceClickedInternal(bool bypassHumanSuppression)
    {
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager not found!");
            return;
        }

        if (!bypassHumanSuppression && gameManager.IsHumanInputSuppressed())
        {
            return;
        }

        SyncCurrentPathIndexFromTransform();
        if (IsFinishedInHomePath())
        {
            return;
        }

        if (gameManager.IsSorryMode() && gameManager.GetSelectedPieceForSorry() != null && gameManager.GetCurrentPlayer() != playerNumber)
        {
            TryHandleSorryTargetClick();
            return;
        }

        if (gameManager.IsCard11Mode() && gameManager.GetSelectedPieceForCard11() != null && gameManager.GetCurrentPlayer() != playerNumber)
        {
            TryHandleCard11SwapTargetClick();
            return;
        }

        if (gameManager.IsCard12Mode() && gameManager.GetSelectedPieceForCard12() != null && gameManager.GetCurrentPlayer() != playerNumber)
        {
            TryHandleCard12CaptureTargetClick();
            return;
        }

        // Agar current player turn nahi che to return
        if (gameManager.GetCurrentPlayer() != playerNumber)
        {
            Debug.Log($"Not Player {playerNumber}'s turn!");
            return;
        }

        // Agar card pick nahi thayu to return
        if (!gameManager.IsCardPicked())
        {
            Debug.Log("No card picked yet!");
            return;
        }

        if (debugPrintHighlightedDestinationNames)
        {
            if (highlightedDestinations != null && highlightedDestinations.Count > 0)
            {
                string names = "";
                for (int i = 0; i < highlightedDestinations.Count; i++)
                {
                    Transform t = highlightedDestinations[i];
                    if (t == null) continue;
                    if (names.Length > 0) names += ", ";
                    names += t.name;
                }
                Debug.Log($"🟩 Highlighted Destinations: P{playerNumber}-Piece{pieceNumber} [{names}]");
            }
            else
            {
                Debug.Log($"🟩 Highlighted Destinations: P{playerNumber}-Piece{pieceNumber} [none]");
            }
        }

        if (debugPrintMoveDestinations)
        {
            int debugCardValue = gameManager.GetCurrentCardValue();
            gameManager.DebugPrintMoveDestinations(this, debugCardValue);
        }

        // New piece selection: clear any previous destination highlights so only current piece highlights remain.
        ClearAllPiecesHighlights();

        // Card value get karo
        int cardValue = gameManager.GetCurrentCardValue();
        if (cardValue == 0 && (gameManager == null || !gameManager.IsSorryMode()))
        {
            Debug.LogWarning("Card value is 0!");
            return;
        }

        if (gameManager.IsSorryMode())
        {
            // Dual-power UX for SORRY:
            // - If you click a START pawn, show opponent targets (Option 1)
            // - If you click an on-board pawn, show +4 destination (Option 2), if possible
            if (IsAtHome())
            {
                ShowSorryTargets();
            }
            else
            {
                bool canPlus4 = gameManager.CheckIfMovePossible(this, 4);
                if (!canPlus4)
                {
                    return;
                }

                ShowSorryPlus4DestinationIfPossible();
            }
            return;
        }

        // Card 10 dual power mode check
        if (gameManager.IsCard10Mode())
        {
            bool canForward = gameManager.CheckIfMovePossible(this, 10);
            bool canBackward = !IsAtHome() && gameManager.CheckIfMovePossible(this, -1);
            if (!canForward && !canBackward)
            {
                return;
            }

            // Card 10 mode - show all possible destinations (both +10 and -1)
            ShowCard10Destinations();
            return;
        }

        if (gameManager.IsCard11Mode())
        {
            bool canForward = gameManager.CheckIfMovePossible(this, 11);
            bool canSwap = false;
            if (IsOnOuterTrack())
            {
                foreach (PlayerPiece p in gameManager.GetOpponentPiecesForPlayer(playerNumber))
                {
                    if (p == null) continue;
                    p.SyncCurrentPathIndexFromTransform();
                    if (p.IsOnOuterTrack())
                    {
                        canSwap = true;
                        break;
                    }
                }
            }

            if (!canForward && !canSwap)
            {
                return;
            }

            // NOTE: Card 11 can be a two-step action (select your piece, then select an opponent piece to swap).
            // Do not stop the turn glow/highlight on the first selection click; it will be stopped when the
            // action actually executes (swap coroutine / move execution).
            ShowCard11Destinations();
            return;
        }

        if (gameManager.IsCard12Mode())
        {
            bool canCapture = gameManager.CheckIfMovePossible(this, 12);
            if (!canCapture)
            {
                return;
            }

            ShowCard12Targets();
            return;
        }

        // Split mode check (Card 7)
        if (gameManager.IsSplitMode())
        {
            // Agar first piece already move thayu che to remaining steps use karo
            if (gameManager.IsFirstPieceMovedInSplit())
            {
                PlayerPiece firstSplitPiece = gameManager.GetSelectedPieceForSplit();
                if (firstSplitPiece != null && firstSplitPiece == this)
                {
                    Debug.LogWarning($"❌ Split Mode: Remaining steps cannot be used on the same first piece (Piece {pieceNumber}). Select another piece.");
                    return;
                }

                // Second piece - remaining steps use karo
                int remainingSteps = gameManager.GetRemainingSteps();
                if (remainingSteps > 0)
                {
                    // Rule: Move possible check karo (same player piece blocking)
                    bool canMove = gameManager.CheckIfMovePossible(this, remainingSteps);
                    if (!canMove)
                    {
                        Debug.LogWarning($"❌ Piece {pieceNumber} cannot move {remainingSteps} steps in split mode - Move blocked!");
                        return; // Move nahi kari shaksho
                    }

                    gameManager.StopAllTurnPieceHighlights();
                    Debug.Log($"🔵 Split Mode: Using remaining {remainingSteps} steps for second piece");

                    if (gameManager != null && gameManager.IsPlayWithOopsMode)
                    {
                        // PlayWithOops second split: server-authoritative.
                        // User wants 2nd split to be pawn-click driven (no destination highlight).
                        bool sent = gameManager.TryOopsPlayCardSplitSecond(this, remainingSteps);
                        if (!sent)
                        {
                            Debug.LogWarning($"❌ PlayWithOops: Split second not sent for piece {pieceNumber} steps={remainingSteps}");
                        }
                        return;
                    }

                    MovePieceDirectly(remainingSteps);
                }
                else
                {
                    Debug.LogWarning("No remaining steps in split mode!");
                }
                return;
            }
            else
            {
                // First piece - show destination highlights (7 steps ahead)
                // Rule: Move possible check karo (same player piece blocking)
                // Note: Split mode ma 7 steps possible check kariye, pan user ne piece select kari shaksho
                bool anyDest = false;
                for (int s = 1; s <= 7; s++)
                {
                    if (gameManager.CheckIfMovePossible(this, s))
                    {
                        anyDest = true;
                        break;
                    }
                }

                if (!anyDest)
                {
                    return;
                }

                gameManager.StopAllTurnPieceHighlights();
                ShowSplitDestinations(7);
                return;
            }
        }

        // Rule: Move possible check karo (same player piece blocking) - SABHI cards mate
        // GameManager already highlight kari chuki hase (Card 1-7 mate), pan double check karo
        // IMPORTANT: Card 8, 10, 11, 12 mate pan blocking check karo!
        if (cardValue >= 1) // Sabhi forward cards mate (1, 2, 3, 4, 5, 7, 8, 10, 11, 12)
        {
            bool canMove = gameManager.CheckIfMovePossible(this, cardValue);
            if (!canMove)
            {
                Debug.LogWarning($"❌ Piece {pieceNumber} cannot move {cardValue} steps - Move blocked or invalid!");
                return; // Move nahi kari shaksho
            }
        }

        // Backward card check: Agar piece home ma che ane card backward che to move nahi kari shaksho
        // Card 4 (Move -4) sirf tab use kari shaksho jyare piece already board par che
        if (IsAtHome() && cardValue < 0)
        {
            Debug.LogWarning($"❌ Cannot use backward card (Move {cardValue}) when piece is at home!");
            Debug.LogWarning($"   Piece must be on board first. Backward cards only work when piece is already on the path.");
            return;
        }
        
        // Backward card mate confirmation (piece board par che to backward move allowed)
        if (!IsAtHome() && cardValue < 0)
        {
            Debug.Log($"✅ Backward card (Move {cardValue}) - Piece is on board, backward movement allowed.");
        }

        // Piece click par direct move karo (card value anusar)
        gameManager.StopAllTurnPieceHighlights();

        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            bool sent = gameManager.TryOopsPlayCardMove(this, cardValue);
            // PlayWithOops is server-authoritative: never do local fallback.
            return;
        }
        MovePieceDirectly(cardValue);
    }

    void ShowCard11Destinations()
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
            return;
        }

        gameManager.SetSelectedPieceForCard11(this);

        ClearThisPieceHighlights();
        DisableSwapTargetHighlightForOpponent();

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1;

        if (gameManager.TryGetDestinationForMove(this, 11, out int forwardDestIndex, out Transform forwardDest, out string reason))
        {
            if (forwardDestIndex != routePathLastIndex && forwardDest != null)
            {
                highlightedDestinations.Add(forwardDest);
                HighlightDestination(forwardDest, 11);
                Debug.Log($"🔵 Card 11: ✅ Highlighted forward destination at index {forwardDestIndex} (+11 steps) - List element: {forwardDest.name}");
            }
        }

        if (IsOnOuterTrack())
        {
            EnableSwapTargetHighlightForOpponent();
        }
    }

    void TryHandleCard11SwapTargetClick()
    {
        if (gameManager == null || !gameManager.IsCard11Mode())
        {
            return;
        }

        if (isCard11SwapAnimating)
        {
            return;
        }

        PlayerPiece sourcePiece = gameManager.GetSelectedPieceForCard11();
        if (sourcePiece == null)
        {
            return;
        }

        if (!sourcePiece.IsOnOuterTrack())
        {
            return;
        }

        if (!IsOnOuterTrack())
        {
            return;
        }

        Transform aPos = sourcePiece.GetCurrentPositionTransform();
        int aIndex = sourcePiece.GetCurrentPathIndex();
        Transform bPos = GetCurrentPositionTransform();
        int bIndex = GetCurrentPathIndex();

        if (aPos == null || bPos == null || aIndex < 0 || bIndex < 0)
        {
            return;
        }

        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            bool sent = gameManager.TryOopsPlayCardSwap(sourcePiece, this);
            if (sent)
            {
                ClearAllPiecesHighlights();
                DisableSwapTargetHighlightForOpponent();
                if (gameManager != null)
                {
                    gameManager.CompleteCard11Mode();
                }
                return;
            }

            // PlayWithOops is server-authoritative: never do local fallback.
            return;
        }

        // IMPORTANT:
        // Do not parent a piece under the opponent's tile Transform directly.
        // Paths can be authored per-player (different Transform instances / hierarchies).
        // If we parent under the opponent tile, this piece may no longer be found in its own path,
        // which can break path-index syncing and cause incorrect wrap-around moves on later turns.
        bool mappedA = sourcePiece.TryMapOuterTrackAnchorToThisPlayersPath(bPos, out Transform mappedForSource, out int mappedIndexForSource);
        bool mappedB = TryMapOuterTrackAnchorToThisPlayersPath(aPos, out Transform mappedForTarget, out int mappedIndexForTarget);

        Transform finalPosForSource = null;
        int finalIndexForSource = -1;
        Transform finalPosForTarget = null;
        int finalIndexForTarget = -1;

        if (mappedA && mappedB)
        {
            finalPosForSource = mappedForSource;
            finalIndexForSource = mappedIndexForSource;
            finalPosForTarget = mappedForTarget;
            finalIndexForTarget = mappedIndexForTarget;
        }
        else
        {
            finalPosForSource = bPos;
            finalIndexForSource = bIndex;
            finalPosForTarget = aPos;
            finalIndexForTarget = aIndex;
        }

        StartCoroutine(AnimateCard11SwapAndFinalize(sourcePiece, finalPosForSource, finalIndexForSource, this, finalPosForTarget, finalIndexForTarget, true));
    }

    public IEnumerator AnimateCard11SwapAndFinalize(PlayerPiece aPiece, Transform aTargetPos, int aTargetIndex, PlayerPiece bPiece, Transform bTargetPos, int bTargetIndex, bool completeCard11Mode)
    {
        if (aPiece == null || bPiece == null || aTargetPos == null || bTargetPos == null || aTargetIndex < 0 || bTargetIndex < 0)
        {
            yield break;
        }

        if (isCard11SwapAnimating)
        {
            yield break;
        }

        isCard11SwapAnimating = true;

        aPiece.StopTurnHighlight();
        bPiece.StopTurnHighlight();
        if (gameManager != null)
        {
            gameManager.StopAllTurnPieceHighlights();
        }

        aPiece.isMoving = true;
        bPiece.isMoving = true;

        Transform aMovementRoot = aPiece.GetMovementRoot();
        if (aMovementRoot != null)
        {
            RectTransform aRt = aPiece.transform as RectTransform;
            if (aRt != null)
            {
                aRt.SetParent(aMovementRoot, true);
            }
            else
            {
                aPiece.transform.SetParent(aMovementRoot, true);
            }
        }
        else
        {
            aPiece.transform.SetParent(null, true);
        }

        Transform bMovementRoot = bPiece.GetMovementRoot();
        if (bMovementRoot != null)
        {
            RectTransform bRt = bPiece.transform as RectTransform;
            if (bRt != null)
            {
                bRt.SetParent(bMovementRoot, true);
            }
            else
            {
                bPiece.transform.SetParent(bMovementRoot, true);
            }
        }
        else
        {
            bPiece.transform.SetParent(null, true);
        }

        Vector3 aStart = aPiece.transform.position;
        Vector3 bStart = bPiece.transform.position;
        Vector3 aEnd = aPiece.GetWorldPositionWithYOffset(aTargetPos);
        Vector3 bEnd = bPiece.GetWorldPositionWithYOffset(bTargetPos);

        Vector3 aMid = (aStart + aEnd) * 0.5f;
        Vector3 bMid = (bStart + bEnd) * 0.5f;

        Vector3 aDir = aEnd - aStart;
        Vector3 bDir = bEnd - bStart;

        Vector3 aPerp = aDir.sqrMagnitude > 0.0001f ? Vector3.Cross(aDir.normalized, Vector3.forward) : Vector3.up;
        Vector3 bPerp = bDir.sqrMagnitude > 0.0001f ? Vector3.Cross(bDir.normalized, Vector3.forward) : Vector3.down;

        float curve = card11SwapCurveAmount;
        Vector3 aControl = aMid + (aPerp * curve);
        Vector3 bControl = bMid - (bPerp * curve);

        float duration = Mathf.Max(0.01f, card11SwapDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = u * u * (3f - 2f * u);

            aPiece.transform.position = EvaluateQuadraticBezier(aStart, aControl, aEnd, eased);
            bPiece.transform.position = EvaluateQuadraticBezier(bStart, bControl, bEnd, eased);
            yield return null;
        }

        aPiece.transform.position = aEnd;
        bPiece.transform.position = bEnd;

        int aFromIndex = aPiece.GetCurrentPathIndex();
        int bFromIndex = bPiece.GetCurrentPathIndex();

        aPiece.ForceSetPosition(aTargetPos, aTargetIndex);
        bPiece.ForceSetPosition(bTargetPos, bTargetIndex);

        if (gameManager != null)
        {
            gameManager.LogOopsMove(aPiece, aFromIndex, aTargetIndex, "SWAP");
            gameManager.LogOopsMove(bPiece, bFromIndex, bTargetIndex, "SWAP");
        }

        aPiece.RecalculateCurrentPathIndexFromParent();
        bPiece.RecalculateCurrentPathIndexFromParent();

        if (aPiece.pathManager != null)
        {
            List<Transform> aRoute = aPiece.pathManager.GetPlayerRoutePath(aPiece.playerNumber);
            List<Transform> aComplete = aPiece.pathManager.GetCompletePlayerPath(aPiece.playerNumber);
            int aRouteLen = aRoute != null ? aRoute.Count : (aComplete != null ? aComplete.Count : 0);
            if (aRouteLen > 0)
            {
                int aEntry = Mathf.Max(0, aRouteLen - 2);
                int aLast = aRouteLen - 1;
                yield return StartCoroutine(aPiece.PerformSlideIfNeeded(aTargetPos, aRouteLen, aEntry, aLast));
            }
        }

        if (bPiece.pathManager != null)
        {
            List<Transform> bRoute = bPiece.pathManager.GetPlayerRoutePath(bPiece.playerNumber);
            List<Transform> bComplete = bPiece.pathManager.GetCompletePlayerPath(bPiece.playerNumber);
            int bRouteLen = bRoute != null ? bRoute.Count : (bComplete != null ? bComplete.Count : 0);
            if (bRouteLen > 0)
            {
                int bEntry = Mathf.Max(0, bRouteLen - 2);
                int bLast = bRouteLen - 1;
                yield return StartCoroutine(bPiece.PerformSlideIfNeeded(bTargetPos, bRouteLen, bEntry, bLast));
            }
        }

        ClearAllPiecesHighlights();
        DisableSwapTargetHighlightForOpponent();

        if (completeCard11Mode && gameManager != null)
        {
            gameManager.CompleteCard11Mode();
        }

        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            gameManager.NotifyOopsServerSwapAnimationCompleted(aPiece);
            gameManager.NotifyOopsServerSwapAnimationCompleted(bPiece);
        }

        aPiece.isMoving = false;
        bPiece.isMoving = false;
        isCard11SwapAnimating = false;
    }

    public IEnumerator AnimateOopsBumpAndFinalize(PlayerPiece attacker, Transform attackerTargetPos, int attackerTargetIndex, PlayerPiece victim)
    {
        if (attacker == null || victim == null || attackerTargetPos == null || attackerTargetIndex < 0)
        {
            yield break;
        }

        if (isOopsBumpAnimating)
        {
            yield break;
        }

        Transform victimHome = victim.homeTransform;
        if (victimHome == null)
        {
            yield break;
        }

        isOopsBumpAnimating = true;

        attacker.StopTurnHighlight();
        victim.StopTurnHighlight();
        if (gameManager != null)
        {
            gameManager.StopAllTurnPieceHighlights();
        }

        attacker.isMoving = true;
        victim.isMoving = true;

        Transform aMovementRoot = attacker.GetMovementRoot();
        if (aMovementRoot != null)
        {
            RectTransform aRt = attacker.transform as RectTransform;
            if (aRt != null)
            {
                aRt.SetParent(aMovementRoot, true);
            }
            else
            {
                attacker.transform.SetParent(aMovementRoot, true);
            }
        }
        else
        {
            attacker.transform.SetParent(null, true);
        }

        Transform vMovementRoot = victim.GetMovementRoot();
        if (vMovementRoot != null)
        {
            RectTransform vRt = victim.transform as RectTransform;
            if (vRt != null)
            {
                vRt.SetParent(vMovementRoot, true);
            }
            else
            {
                victim.transform.SetParent(vMovementRoot, true);
            }
        }
        else
        {
            victim.transform.SetParent(null, true);
        }

        Vector3 aStart = attacker.transform.position;
        Vector3 vStart = victim.transform.position;
        Vector3 aEnd = attacker.GetWorldPositionWithYOffset(attackerTargetPos);
        Vector3 vEnd = victim.GetWorldPositionWithYOffset(victimHome);

        float duration = Mathf.Max(0.01f, card11SwapDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = u * u * (3f - 2f * u);

            attacker.transform.position = Vector3.Lerp(aStart, aEnd, eased);
            victim.transform.position = Vector3.Lerp(vStart, vEnd, eased);
            yield return null;
        }

        attacker.transform.position = aEnd;
        victim.transform.position = vEnd;

        float spinDuration = Mathf.Max(0.01f, duration * 0.35f);
        float spinTime = 0f;
        Quaternion aRotStart = attacker.transform.rotation;
        Quaternion aRotEnd = aRotStart * Quaternion.Euler(0f, 0f, 360f);
        while (spinTime < spinDuration)
        {
            spinTime += Time.deltaTime;
            float u = Mathf.Clamp01(spinTime / spinDuration);
            float eased = u * u * (3f - 2f * u);
            attacker.transform.rotation = Quaternion.Slerp(aRotStart, aRotEnd, eased);
            yield return null;
        }
        attacker.transform.rotation = aRotEnd;

        int attackerFromIndex = attacker.GetCurrentPathIndex();
        int victimFromIndex = victim.GetCurrentPathIndex();

        attacker.ForceSetPosition(attackerTargetPos, attackerTargetIndex);
        victim.ReturnToHome();

        if (attackerTargetPos != null)
        {
            attacker.transform.rotation = attackerTargetPos.rotation;
        }

        if (gameManager != null)
        {
            gameManager.LogOopsMove(attacker, attackerFromIndex, attackerTargetIndex, "BUMP");
            gameManager.LogOopsMove(victim, victimFromIndex, -1, "KILL");
        }

        attacker.RecalculateCurrentPathIndexFromParent();
        victim.RecalculateCurrentPathIndexFromParent();

        ClearAllPiecesHighlights();
        DisableSwapTargetHighlightForOpponent();

        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            gameManager.NotifyOopsServerSwapAnimationCompleted(attacker);
        }

        attacker.isMoving = false;
        victim.isMoving = false;
        isOopsBumpAnimating = false;
    }

    public IEnumerator AnimateOopsKillReturnHomeAndFinalize(PlayerPiece victim)
    {
        if (victim == null)
        {
            yield break;
        }

        if (isOopsKillReturnAnimating)
        {
            yield break;
        }

        Transform victimHome = victim.homeTransform;
        if (victimHome == null)
        {
            yield break;
        }

        isOopsKillReturnAnimating = true;

        victim.StopTurnHighlight();
        if (gameManager != null)
        {
            gameManager.StopAllTurnPieceHighlights();
        }

        victim.isMoving = true;

        Transform vMovementRoot = victim.GetMovementRoot();
        if (vMovementRoot != null)
        {
            RectTransform vRt = victim.transform as RectTransform;
            if (vRt != null)
            {
                vRt.SetParent(vMovementRoot, true);
            }
            else
            {
                victim.transform.SetParent(vMovementRoot, true);
            }
        }
        else
        {
            victim.transform.SetParent(null, true);
        }

        Vector3 vStart = victim.transform.position;
        Vector3 vEnd = victim.GetWorldPositionWithYOffset(victimHome);

        Vector3 vMid = (vStart + vEnd) * 0.5f;
        Vector3 vDir = vEnd - vStart;
        Vector3 vPerp = vDir.sqrMagnitude > 0.0001f ? Vector3.Cross(vDir.normalized, Vector3.forward) : Vector3.down;

        float curve = card11SwapCurveAmount;
        Vector3 vControl = vMid - (vPerp * curve);

        float duration = Mathf.Max(0.01f, card11SwapDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = u * u * (3f - 2f * u);

            victim.transform.position = EvaluateQuadraticBezier(vStart, vControl, vEnd, eased);
            yield return null;
        }

        victim.transform.position = vEnd;

        float spinDuration = Mathf.Max(0.01f, duration * 0.35f);
        float spinTime = 0f;
        Quaternion vRotStart = victim.transform.rotation;
        Quaternion vRotEnd = vRotStart * Quaternion.Euler(0f, 0f, 360f);
        while (spinTime < spinDuration)
        {
            spinTime += Time.deltaTime;
            float u = Mathf.Clamp01(spinTime / spinDuration);
            float eased = u * u * (3f - 2f * u);
            victim.transform.rotation = Quaternion.Slerp(vRotStart, vRotEnd, eased);
            yield return null;
        }
        victim.transform.rotation = vRotEnd;

        victim.ReturnToHome();
        if (victimHome != null)
        {
            victim.transform.rotation = victimHome.rotation;
        }
        victim.RecalculateCurrentPathIndexFromParent();

        ClearAllPiecesHighlights();
        DisableSwapTargetHighlightForOpponent();

        victim.isMoving = false;
        isOopsKillReturnAnimating = false;
    }

    public IEnumerator AnimateOopsMoveToTargetAndFinalize(PlayerPiece piece, Transform targetPos)
    {
        if (piece == null || targetPos == null)
        {
            yield break;
        }

        Transform movementRoot = piece.GetMovementRoot();
        if (movementRoot != null)
        {
            RectTransform rt = piece.transform as RectTransform;
            if (rt != null)
            {
                rt.SetParent(movementRoot, true);
            }
            else
            {
                piece.transform.SetParent(movementRoot, true);
            }
        }
        else
        {
            piece.transform.SetParent(null, true);
        }

        piece.isMoving = true;

        Vector3 start = piece.transform.position;
        Vector3 end = piece.GetWorldPositionWithYOffset(targetPos);
        Vector3 mid = (start + end) * 0.5f;

        Vector3 dir = end - start;
        Vector3 perp = dir.sqrMagnitude > 0.0001f ? Vector3.Cross(dir.normalized, Vector3.forward) : Vector3.up;
        float curve = card11SwapCurveAmount;
        Vector3 control = mid + (perp * curve);

        float duration = Mathf.Max(0.01f, card11SwapDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = u * u * (3f - 2f * u);

            piece.transform.position = EvaluateQuadraticBezier(start, control, end, eased);
            yield return null;
        }

        piece.transform.position = end;
        piece.ForceSetParentOnly(targetPos);
        piece.RecalculateCurrentPathIndexFromParent();
        piece.isMoving = false;
    }

    private static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    bool TryMapOuterTrackAnchorToThisPlayersPath(Transform otherPlayersOuterAnchor, out Transform mappedAnchor, out int mappedIndex)
    {
        mappedAnchor = null;
        mappedIndex = -1;

        if (otherPlayersOuterAnchor == null || pathManager == null)
        {
            return false;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        if (routePath == null || routePath.Count == 0)
        {
            return false;
        }

        // Swap is only allowed on the outer track, so we only search routePath.
        const float epsilon = 0.02f;
        float bestDist = float.PositiveInfinity;
        int bestIdx = -1;

        Vector3 otherPos = otherPlayersOuterAnchor.position;
        for (int i = 0; i < routePath.Count; i++)
        {
            Transform t = routePath[i];
            if (t == null) continue;

            float d = Vector3.Distance(t.position, otherPos);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        if (bestIdx < 0 || bestDist > epsilon)
        {
            return false;
        }

        mappedIndex = bestIdx;
        mappedAnchor = routePath[bestIdx];
        return mappedAnchor != null;
    }

    void ShowCard12Targets()
    {
        if (gameManager == null)
        {
            return;
        }

        if (pathManager == null)
        {
            return;
        }

        gameManager.SetSelectedPieceForCard12(this);

        ClearThisPieceHighlights();
        DisableCaptureTargetHighlightForOpponent();

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath != null && completePath.Count > 0)
        {
            List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
            int routePathLength = routePath != null ? routePath.Count : completePath.Count;
            int routePathLastIndex = routePathLength - 1;

            if (gameManager.TryGetDestinationForMove(this, 12, out int forwardDestIndex, out Transform forwardDest, out string reason))
            {
                if (forwardDestIndex != routePathLastIndex && forwardDest != null)
                {
                    highlightedDestinations.Add(forwardDest);
                    HighlightDestination(forwardDest, 12);
                }
            }
        }

        if (!IsAtHome())
        {
            EnableCaptureTargetHighlightForOpponent();
        }
    }

    void TryHandleCard12CaptureTargetClick()
    {
        if (gameManager == null || !gameManager.IsCard12Mode())
        {
            return;
        }

        PlayerPiece attacker = gameManager.GetSelectedPieceForCard12();
        if (attacker == null)
        {
            return;
        }

        SyncCurrentPathIndexFromTransform();

        if (IsOnHomePath())
        {
            return;
        }

        if (IsAtHome())
        {
            return;
        }

        Transform targetPos = GetCurrentPositionTransform();
        int targetIndex = GetCurrentPathIndex();
        if (targetPos == null || targetIndex < 0)
        {
            return;
        }

        StopTurnHighlight();
        if (gameManager != null)
        {
            gameManager.StopAllTurnPieceHighlights();
        }

        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            bool sent = gameManager.TryOopsPlayCardBump(attacker, this);
            if (sent)
            {
                ClearAllPiecesHighlights();
                DisableCaptureTargetHighlightForOpponent();
                gameManager.CompleteCard12Mode();
                return;
            }

            // PlayWithOops is server-authoritative: never do local fallback.
            return;
        }

        ReturnToHome();
        attacker.ForceSetPosition(targetPos, targetIndex);

        attacker.RecalculateCurrentPathIndexFromParent();

        ClearAllPiecesHighlights();
        DisableCaptureTargetHighlightForOpponent();

        gameManager.CompleteCard12Mode();
    }

    void EnableCaptureTargetHighlightForOpponent()
    {
        if (gameManager == null)
        {
            return;
        }

        int opponent = playerNumber == 1 ? 2 : 1;
        List<PlayerPiece> opponentPieces = opponent == 1 ? gameManager.player1Pieces : gameManager.player2Pieces;
        foreach (PlayerPiece p in opponentPieces)
        {
            if (p == null) continue;
            if (!p.IsOnOuterTrack())
            {
                continue;
            }

            p.ShowPiece();
            p.SetClickable(true);
            p.SetSwapTargetHighlight(true);
        }
    }

    public bool IsOnOuterTrack()
    {
        if (pathManager == null)
        {
            return false;
        }

        SyncCurrentPathIndexFromTransform();

        if (currentPathIndex < 0)
        {
            return false;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routeCount = routePath != null ? routePath.Count : 0;
        if (routeCount <= 0)
        {
            return false;
        }

        return currentPathIndex >= 0 && currentPathIndex < routeCount;
    }

    public bool IsOnHomePath()
    {
        if (pathManager == null)
        {
            return false;
        }

        SyncCurrentPathIndexFromTransform();
        if (currentPathIndex < 0) return false;

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routeCount = routePath != null ? routePath.Count : 0;

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        int completeCount = completePath != null ? completePath.Count : 0;

        return currentPathIndex >= routeCount && currentPathIndex < completeCount;
    }

    public bool IsFinishedInHomePath()
    {
        if (pathManager == null)
        {
            return false;
        }

        SyncCurrentPathIndexFromTransform();
        if (currentPathIndex < 0)
        {
            return false;
        }

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            return false;
        }

        if (!IsOnHomePath())
        {
            return false;
        }

        return currentPathIndex == completePath.Count - 1;
    }

    public string GetZoneLabel()
    {
        SyncCurrentPathIndexFromTransform();
        if (IsAtHome()) return "START";
        if (IsOnOuterTrack()) return "OUTER";
        if (IsOnHomePath()) return "HOME_PATH";
        return "UNKNOWN";
    }

    public string GetStatusString()
    {
        SyncCurrentPathIndexFromTransform();

        string parentName = transform.parent != null ? transform.parent.name : "<no-parent>";
        Transform pos = GetCurrentPositionTransform();
        string posName = pos != null ? pos.name : "<null-pos>";

        return $"P{playerNumber}-Piece{pieceNumber} zone={GetZoneLabel()} idx={currentPathIndex} parent={parentName} pos={posName}";
    }

    void DisableCaptureTargetHighlightForOpponent()
    {
        if (gameManager == null)
        {
            return;
        }

        foreach (PlayerPiece p in gameManager.GetOpponentPiecesForPlayer(playerNumber))
        {
            if (p == null) continue;
            p.SetSwapTargetHighlight(false);
        }
    }

    void ShowSorryTargets()
    {
        if (gameManager == null)
        {
            return;
        }

        SyncCurrentPathIndexFromTransform();

        if (!IsAtHome())
        {
            return;
        }

        gameManager.SetSelectedPieceForSorry(this);

        DisableSorryTargetHighlightForOpponent();
        EnableSorryTargetHighlightForOpponent();
    }

    void ShowSorryPlus4DestinationIfPossible()
    {
        if (gameManager == null || pathManager == null)
        {
            Debug.Log($"🔵 SORRY +4 highlight: Missing refs for P{playerNumber}-Piece{pieceNumber} (gm={(gameManager != null)}, pm={(pathManager != null)})");
            return;
        }

        SyncCurrentPathIndexFromTransform();
        if (IsAtHome() || IsOnHomePath() || IsFinishedInHomePath())
        {
            Debug.Log($"🔵 SORRY +4 highlight: Not eligible zone for P{playerNumber}-Piece{pieceNumber} (idx={currentPathIndex}, atHome={IsAtHome()}, onHomePath={IsOnHomePath()}, finished={IsFinishedInHomePath()})");
            return;
        }

        const int steps = 4;
        bool canPlus4 = gameManager.CheckIfMovePossible(this, steps);
        if (!canPlus4)
        {
            Debug.Log($"🔵 SORRY +4 highlight: CheckIfMovePossible false for P{playerNumber}-Piece{pieceNumber} (+4)");
            return;
        }

        ClearThisPieceHighlights();
        DisableSorryTargetHighlightForOpponent();

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.Log($"🔵 SORRY +4 highlight: completePath missing/empty for P{playerNumber}-Piece{pieceNumber}");
            return;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1;
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);

        int destIndex;
        if (currentPathIndex == -1)
        {
            destIndex = steps - 1;
        }
        else if (currentPathIndex >= 0 && currentPathIndex < routePathLength && currentPathIndex <= routeEntryIndex)
        {
            int stepsToEntry = routeEntryIndex - currentPathIndex;
            if (steps <= stepsToEntry)
            {
                destIndex = currentPathIndex + steps;
            }
            else
            {
                int remainingAfterEntryToHome = steps - stepsToEntry - 1;
                destIndex = routePathLength + remainingAfterEntryToHome;
            }
        }
        else
        {
            destIndex = currentPathIndex + steps;
        }

        if (destIndex < 0 || destIndex >= completePath.Count)
        {
            Debug.Log($"🔵 SORRY +4 highlight: destIndex out of range for P{playerNumber}-Piece{pieceNumber} (destIndex={destIndex}, pathCount={completePath.Count}, currentIdx={currentPathIndex})");
            return;
        }

        Transform destPos = completePath[destIndex];
        if (destPos == null)
        {
            Debug.Log($"🔵 SORRY +4 highlight: destPos null for P{playerNumber}-Piece{pieceNumber} (destIndex={destIndex})");
            return;
        }

        if (destIndex == routePathLastIndex)
        {
            return;
        }

        highlightedDestinations.Add(destPos);
        HighlightDestination(destPos, steps);
        Debug.Log($"🔵 SORRY +4 highlight: Highlighted dest '{destPos.name}' for P{playerNumber}-Piece{pieceNumber} (+4, destIndex={destIndex})");
    }

    void TryHandleSorryTargetClick()
    {
        if (gameManager == null || !gameManager.IsSorryMode())
        {
            return;
        }

        if (gameManager != null && gameManager.IsPlayWithOopsMode)
        {
            PlayerPiece startPawn0 = gameManager.GetSelectedPieceForSorry();
            if (startPawn0 == null) return;

            SyncCurrentPathIndexFromTransform();
            if (IsAtHome()) return;
            if (IsOnHomePath()) return;
            if (IsFinishedInHomePath()) return;

            StopTurnHighlight();
            startPawn0.StopTurnHighlight();
            gameManager.StopAllTurnPieceHighlights();

            ClearAllPiecesHighlights();
            DisableSorryTargetHighlightForOpponent();

            bool sent = gameManager.TryOopsPlayCardSorryReplace(startPawn0, this);
            if (sent)
            {
                gameManager.SetSelectedPieceForSorry(null);
            }
            return;
        }

        PlayerPiece startPawn = gameManager.GetSelectedPieceForSorry();
        if (startPawn == null)
        {
            return;
        }

        SyncCurrentPathIndexFromTransform();

        if (IsAtHome())
        {
            return;
        }

        if (IsOnHomePath())
        {
            return;
        }

        Transform targetPos = GetCurrentPositionTransform();
        if (targetPos == null)
        {
            return;
        }

        StopTurnHighlight();
        if (startPawn != null)
        {
            startPawn.StopTurnHighlight();
        }
        if (gameManager != null)
        {
            gameManager.StopAllTurnPieceHighlights();
        }

        ClearAllPiecesHighlights();
        DisableSorryTargetHighlightForOpponent();

        StartCoroutine(SorryReplaceAndSlideRoutine(startPawn, targetPos, this));
    }

    private IEnumerator SorryReplaceAndSlideRoutine(PlayerPiece startPawn, Transform targetPos, PlayerPiece victim)
    {
        if (startPawn == null || targetPos == null || gameManager == null)
        {
            yield break;
        }

        if (isOopsSorryReplaceAnimating)
        {
            yield break;
        }

        isOopsSorryReplaceAnimating = true;

        if (victim != null)
        {
            victim.SyncCurrentPathIndexFromTransform();
            if (!victim.IsAtHome() && !victim.IsBusy)
            {
                StartCoroutine(victim.AnimateOopsKillReturnHomeAndFinalize(victim));
            }
            else
            {
                victim.ReturnToHome();
            }
        }

        yield return StartCoroutine(AnimateOopsMoveToTargetAndFinalize(startPawn, targetPos));

        yield return null;

        yield return StartCoroutine(startPawn.PerformPostLandingSlideIfNeeded(targetPos));

        if (gameManager == null)
        {
            yield break;
        }

        gameManager.CompleteSorryMode();

        isOopsSorryReplaceAnimating = false;
    }

    void EnableSorryTargetHighlightForOpponent()
    {
        if (gameManager == null)
        {
            return;
        }

        foreach (PlayerPiece p in gameManager.GetOpponentPiecesForPlayer(playerNumber))
        {
            if (p == null) continue;

            p.SyncCurrentPathIndexFromTransform();
            if (p.IsAtHome()) continue;
            if (p.IsOnHomePath()) continue;
            if (p.IsFinishedInHomePath()) continue;

            p.ShowPiece();
            p.SetClickable(true);
            p.SetSwapTargetHighlight(true);
        }
    }

    void DisableSorryTargetHighlightForOpponent()
    {
        if (gameManager == null)
        {
            return;
        }

        foreach (PlayerPiece p in gameManager.GetAllActivePiecesForGameplay())
        {
            if (p == null) continue;
            p.SetSwapTargetHighlight(false);
        }
    }

    void EnableSwapTargetHighlightForOpponent()
    {
        if (gameManager == null)
        {
            return;
        }

        foreach (PlayerPiece p in gameManager.GetOpponentPiecesForPlayer(playerNumber))
        {
            if (p == null) continue;

            p.SyncCurrentPathIndexFromTransform();
            if (!p.IsOnOuterTrack())
            {
                continue;
            }

            p.ShowPiece();
            p.SetClickable(true);
            p.SetSwapTargetHighlight(true);
        }
    }

    void DisableSwapTargetHighlightForOpponent()
    {
        if (gameManager == null)
        {
            return;
        }

        foreach (PlayerPiece p in gameManager.GetAllActivePiecesForGameplay())
        {
            if (p == null) continue;
            p.SetSwapTargetHighlight(false);
        }
    }

    void SetSwapTargetHighlight(bool enabled)
    {
        IsSwapTargetHighlighted = enabled;

        Color highlightColor = Color.green;
        if (gameManager != null)
        {
            highlightColor = gameManager.pieceClickableHighlightColor;
        }

        bool shouldShowOpponentVfx = enabled && gameManager != null;
        if (shouldShowOpponentVfx)
        {
            SpawnOpponentClickableVfxIfNeeded();
        }
        else
        {
            ClearOpponentClickableVfx();
        }

        if (pieceImage != null)
        {
            pieceImage.color = enabled ? highlightColor : Color.white;
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.color = enabled ? highlightColor : Color.white;
        }

        Vector3 baseScale = GetModeBaseScale();
        bool is4P = gameManager != null && gameManager.GetActivePlayerCountPublic() >= 4;
        Vector3 targetScale = enabled ? (is4P ? baseScale : (baseScale * 1.1f)) : baseScale;
        transform.localScale = targetScale;
    }

    private void SpawnOpponentClickableVfxIfNeeded()
    {
        if (gameManager == null) return;

        if (opponentClickableGlowInstance == null && gameManager.opponentClickableGlowPrefab != null)
        {
            opponentClickableGlowInstance = Instantiate(gameManager.opponentClickableGlowPrefab, transform);
            opponentClickableGlowInstance.transform.localPosition = Vector3.zero;
            opponentClickableGlowInstance.transform.localRotation = Quaternion.identity;
            opponentClickableGlowInstance.transform.localScale = Vector3.one;

            Graphic[] graphics = opponentClickableGlowInstance.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                graphics[i].raycastTarget = false;
            }
        }

        if (opponentClickableRingInstance == null && gameManager.opponentClickableRingPrefab != null)
        {
            opponentClickableRingInstance = Instantiate(gameManager.opponentClickableRingPrefab, transform);
            opponentClickableRingInstance.transform.localPosition = Vector3.zero;
            opponentClickableRingInstance.transform.localRotation = Quaternion.identity;
            opponentClickableRingInstance.transform.localScale = Vector3.one;

            Graphic[] graphics = opponentClickableRingInstance.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                graphics[i].raycastTarget = false;
            }
        }

        if (opponentClickableRingInstance != null && (opponentClickableRingTween == null || !opponentClickableRingTween.IsActive()))
        {
            float speed = gameManager.opponentClickableRingRotateSpeed;
            if (speed <= 0f) speed = 25f;

            opponentClickableRingTween = opponentClickableRingInstance.transform
                .DORotate(new Vector3(0f, 0f, 360f), 360f / speed, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }
    }

    private void ClearOpponentClickableVfx()
    {
        if (opponentClickableRingTween != null)
        {
            opponentClickableRingTween.Kill();
            opponentClickableRingTween = null;
        }

        if (opponentClickableGlowInstance != null)
        {
            Destroy(opponentClickableGlowInstance);
            opponentClickableGlowInstance = null;
        }

        if (opponentClickableRingInstance != null)
        {
            Destroy(opponentClickableRingInstance);
            opponentClickableRingInstance = null;
        }
    }

    void ForceSetPosition(Transform targetPosition, int targetIndex)
    {
        if (targetPosition == null)
        {
            return;
        }

        Transform finalParent = targetPosition;
        if (gameManager != null && pathManager != null)
        {
            List<Transform> completePathForSpot = pathManager.GetCompletePlayerPath(playerNumber);
            if (completePathForSpot != null && completePathForSpot.Count > 0)
            {
                int lastIndexForSpot = completePathForSpot.Count - 1;
                if (targetIndex == lastIndexForSpot)
                {
                    Transform spot = gameManager.GetFinalHomeSpot(playerNumber, pieceNumber - 1);
                    if (spot != null)
                    {
                        finalParent = spot;
                    }
                }
            }
        }

        SetPieceParent(finalParent);
        transform.SetAsLastSibling();

        Vector3 localPos = Vector3.zero;
        if (pieceImage != null)
        {
            localPos.z = -1.0f;
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.sortingOrder = 45;
            localPos.z = -1.0f;
        }

        ApplyPieceLocalPosition(localPos);
        currentPathIndex = targetIndex;
        currentPositionTransform = targetPosition;
    }

    void ForceSetParentOnly(Transform targetPosition)
    {
        if (targetPosition == null)
        {
            return;
        }

        SetPieceParent(targetPosition);
        transform.SetAsLastSibling();

        Vector3 localPos = Vector3.zero;
        if (pieceImage != null)
        {
            localPos.z = -1.0f;
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.sortingOrder = 45;
            localPos.z = -1.0f;
        }

        ApplyPieceLocalPosition(localPos);
        currentPositionTransform = targetPosition;
    }

    void RecalculateCurrentPathIndexFromParent()
    {
        if (pathManager == null)
        {
            return;
        }

        Transform parent = transform.parent;
        if (parent == null)
        {
            return;
        }

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            return;
        }

        // Some boards parent pieces under a nested child (e.g., Tile/Content/Piece).
        // In that case, transform.parent won't be a direct member of completePath.
        // Walk up the hierarchy to find the first ancestor that exists in the path.
        Transform t = parent;
        while (t != null)
        {
            if (homeTransform != null && (t == homeTransform || t.IsChildOf(homeTransform)))
            {
                currentPathIndex = -1;
                currentPositionTransform = homeTransform;
                return;
            }

            int idx = completePath.IndexOf(t);
            if (idx >= 0)
            {
                currentPathIndex = idx;
                currentPositionTransform = t;
                return;
            }

            t = t.parent;
        }

        // If the piece was explicitly placed on a known path tile (e.g., final home spot layouts),
        // keep using that authoritative anchor rather than marking the index invalid.
        if (currentPositionTransform != null)
        {
            Transform anchor = currentPositionTransform;
            while (anchor != null)
            {
                int idx = completePath.IndexOf(anchor);
                if (idx >= 0)
                {
                    currentPathIndex = idx;
                    currentPositionTransform = anchor;
                    return;
                }
                anchor = anchor.parent;
            }
        }

        // Fallback: if our parent chain doesn't match any authored path anchor, try to resolve by position.
        // This avoids leaving a stale currentPathIndex, which can cause incorrect wrap-around moves.
        const float resolveEpsilon = 35f;
        float bestDist = float.PositiveInfinity;
        int bestIdx = -1;
        Vector3 p = parent.position;
        for (int i = 0; i < completePath.Count; i++)
        {
            Transform anchor = completePath[i];
            if (anchor == null) continue;
            float d = Vector3.Distance(anchor.position, p);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        if (bestIdx >= 0 && bestDist <= resolveEpsilon)
        {
            currentPathIndex = bestIdx;
            currentPositionTransform = completePath[bestIdx];
            return;
        }

        currentPathIndex = -999;
        currentPositionTransform = parent;
        Debug.LogWarning($"⚠️ Player {playerNumber} Piece {pieceNumber}: Could not resolve currentPathIndex from parent '{parent.name}'. Marking index invalid (currentPathIndex={currentPathIndex}).");
    }

    public void SyncCurrentPathIndexFromTransform()
    {
        RecalculateCurrentPathIndexFromParent();
    }

    /// <summary>
    /// Piece ne direct move karo (card value anusar - no destination highlight)
    /// 
    /// ============================================
    /// COMPLETE MOVE EXAMPLES - ALL POSSIBLE CASES
    /// ============================================
    /// 
    /// ASSUMPTIONS:
    /// - completePath = [Pos0, Pos1, Pos2, ..., Pos39, HomePos] (total 40 elements, indices 0-39)
    /// - routePathLastIndex = 39 (route path na last index, home path exclude)
    /// - currentPathIndex = -1 means piece home ma che
    /// 
    /// ============================================
    /// FORWARD MOVES (Positive Steps: +1, +2, +3, +5, +7, +8, +10, +11, +12)
    /// ============================================
    /// 
    /// Case 1: Piece at HOME (-1), steps = +3
    ///   - Calculation: destIndex = 3 - 1 = 2
    ///   - Result: Piece moves to index 2 (Pos2)
    ///   - List: completePath[2] = Pos2
    /// 
    /// Case 2: Piece at HOME (-1), steps = +10
    ///   - Calculation: destIndex = 10 - 1 = 9
    ///   - Result: Piece moves to index 9 (Pos9)
    ///   - List: completePath[9] = Pos9
    /// 
    /// Case 3: Piece at index 5, steps = +3
    ///   - Calculation: destIndex = 5 + 3 = 8
    ///   - Result: Piece moves to index 8 (Pos8)
    ///   - List: completePath[8] = Pos8
    /// 
    /// Case 4: Piece at index 35, steps = +10
    ///   - Calculation: destIndex = 35 + 10 = 45
    ///   - Boundary check: 45 >= 40 → clamp to 39
    ///   - Result: Piece moves to index 39 (Pos39, clamped to last)
    ///   - List: completePath[39] = Pos39
    /// 
    /// ============================================
    /// BACKWARD MOVES (Negative Steps: -1, -4)
    /// ============================================
    /// NEW LOGIC: Backward moves pehla index 0 sudhi jay, pachhi list na last elements levo
    /// 
    /// Case 1: Piece at index 1, steps = -4
    ///   - Steps to reach index 0: 1
    ///   - Remaining steps: -4 + 1 = -3
    ///   - Calculation: destIndex = 39 + (-3) + 1 = 37
    ///   - Movement: index 1 → index 0 → last 3 elements (39, 38, 37)
    ///   - Result: Piece moves to index 37 (Pos37)
    ///   - List: completePath[37] = Pos37
    /// 
    /// Case 2: Piece at index 2, steps = -4
    ///   - Steps to reach index 0: 2
    ///   - Remaining steps: -4 + 2 = -2
    ///   - Calculation: destIndex = 39 + (-2) + 1 = 38
    ///   - Movement: index 2 → index 1 → index 0 → last 2 elements (39, 38)
    ///   - Result: Piece moves to index 38 (Pos38)
    ///   - List: completePath[38] = Pos38
    /// 
    /// Case 3: Piece at index 3, steps = -4
    ///   - Steps to reach index 0: 3
    ///   - Remaining steps: -4 + 3 = -1
    ///   - Calculation: destIndex = 39 + (-1) + 1 = 39
    ///   - Movement: index 3 → index 2 → index 1 → index 0 → last 1 element (39)
    ///   - Result: Piece moves to index 39 (Pos39)
    ///   - List: completePath[39] = Pos39
    /// 
    /// Case 4: Piece at index 0, steps = -4
    ///   - Steps to reach index 0: 0 (already at index 0)
    ///   - Remaining steps: -4 + 0 = -4
    ///   - Calculation: destIndex = 39 + (-4) + 1 = 36
    ///   - Movement: index 0 → last 4 elements (39, 38, 37, 36)
    ///   - Result: Piece moves to index 36 (Pos36)
    ///   - List: completePath[36] = Pos36
    /// 
    /// Case 5: Piece at index 5, steps = -4
    ///   - Steps to reach index 0: 5
    ///   - Remaining steps: -4 + 5 = 1 (>= 0, so just stop at index 0)
    ///   - Calculation: destIndex = 0
    ///   - Movement: index 5 → index 4 → index 3 → index 2 → index 1 → index 0
    ///   - Result: Piece moves to index 0 (Pos0)
    ///   - List: completePath[0] = Pos0
    /// 
    /// Case 6: Piece at index 10, steps = -4
    ///   - Steps to reach index 0: 10
    ///   - Remaining steps: -4 + 10 = 6 (>= 0, so just stop at index 0)
    ///   - Calculation: destIndex = 0
    ///   - Movement: index 10 → ... → index 0
    ///   - Result: Piece moves to index 0 (Pos0)
    ///   - List: completePath[0] = Pos0
    /// 
    /// Case 7: Piece at index 1, steps = -1
    ///   - Steps to reach index 0: 1
    ///   - Remaining steps: -1 + 1 = 0 (>= 0, so just stop at index 0)
    ///   - Calculation: destIndex = 0
    ///   - Movement: index 1 → index 0
    ///   - Result: Piece moves to index 0 (Pos0)
    ///   - List: completePath[0] = Pos0
    /// 
    /// ============================================
    /// SUMMARY TABLE - QUICK REFERENCE
    /// ============================================
    /// 
    /// Current Index | Steps | Calculation | Result Index | List Element | Notes
    /// --------------|-------|-------------|--------------|--------------|------------------
    /// -1 (HOME)     | +3    | 3-1=2       | 2            | Pos2         | Forward from home
    /// -1 (HOME)     | +10   | 10-1=9      | 9            | Pos9         | Forward from home
    /// 0             | +5    | 0+5=5       | 5            | Pos5         | Forward move
    /// 0             | -4    | 39+(-4)+1=36| 36           | Pos36        | Backward: last 4 elements
    /// 0             | -1    | 39+(-1)+1=39| 39           | Pos39        | Backward: last 1 element
    /// 1             | +3    | 1+3=4       | 4            | Pos4         | Forward move
    /// 1             | -4    | 39+(-3)+1=37| 37           | Pos37        | Backward: 1→0, last 3 elements
    /// 1             | -1    | 0           | 0            | Pos0         | Backward: 1→0, stop at 0
    /// 2             | -4    | 39+(-2)+1=38| 38           | Pos38        | Backward: 2→1→0, last 2 elements
    /// 3             | -4    | 39+(-1)+1=39| 39           | Pos39        | Backward: 3→2→1→0, last 1 element
    /// 5             | -4    | 0           | 0            | Pos0         | Backward: 5→4→3→2→1→0, stop at 0
    /// 10            | -4    | 0           | 0            | Pos0         | Backward: 10→...→0, stop at 0
    /// 35            | +10   | 35+10=45    | 39 (clamped) | Pos39        | Clamped to last
    /// 
    /// ============================================
    /// VISUAL PATH DIAGRAM
    /// ============================================
    /// 
    /// Path Layout (completePath list):
    /// [Pos0] → [Pos1] → [Pos2] → ... → [Pos38] → [Pos39] → [HomePos]
    ///   0        1        2             38        39        40 (home)
    /// 
    /// Example Moves Visualization:
    /// 
    /// 1. Piece at HOME, +3 move:
    ///    HOME → [Pos0] → [Pos1] → [Pos2] ✓ (stops at index 2)
    /// 
    /// 2. Piece at index 1, +3 move:
    ///    [Pos1] → [Pos2] → [Pos3] → [Pos4] ✓ (stops at index 4)
    /// 
    /// 3. Piece at index 1, -4 move (NEW LOGIC):
    ///    [Pos1] → [Pos0] → [Pos39] → [Pos38] → [Pos37] ✓
    ///    Steps: 1 to reach 0, then last 3 elements from end
    /// 
    /// 4. Piece at index 2, -4 move (NEW LOGIC):
    ///    [Pos2] → [Pos1] → [Pos0] → [Pos39] → [Pos38] ✓
    ///    Steps: 2 to reach 0, then last 2 elements from end
    /// 
    /// 5. Piece at index 0, -4 move (NEW LOGIC):
    ///    [Pos0] → [Pos39] → [Pos38] → [Pos37] → [Pos36] ✓
    ///    Steps: Already at 0, then last 4 elements from end
    /// 
    /// 6. Piece at index 5, -4 move (NEW LOGIC):
    ///    [Pos5] → [Pos4] → [Pos3] → [Pos2] → [Pos1] → [Pos0] ✓
    ///    Steps: 5 to reach 0, remaining steps = 1 (>= 0), so stop at 0
    /// 
    /// 7. Piece at index 35, +10 move:
    ///    [Pos35] → [Pos36] → ... → [Pos39] ✓ (clamped to index 39, can't go beyond)
    /// 
    /// ============================================
    /// </summary>
    public void MovePieceDirectly(int steps)
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        SyncCurrentPathIndexFromTransform();
        if (IsFinishedInHomePath())
        {
            return;
        }

        if (gameManager != null)
        {
            bool canMove = gameManager.CheckIfMovePossible(this, steps);
            if (!canMove)
            {
                Debug.LogWarning($"❌ MovePieceDirectly ignored: Piece {pieceNumber} cannot move {steps} steps (blocked or invalid)");
                return;
            }
        }

        if (isMoving)
        {
            Debug.Log("Piece is already moving!");
            return;
        }

        // Use GameManager as the single source of truth for destination resolution.
        // This avoids desync between CheckIfMovePossible() and local dest-index math,
        // which can otherwise lead to a stuck turn (cardPicked stays true but move doesn't execute).
        if (gameManager != null)
        {
            if (!gameManager.TryGetDestinationForMove(this, steps, out int resolvedDestIndex, out Transform resolvedDest, out string reason) || resolvedDest == null)
            {
                Debug.LogWarning($"❌ MovePieceDirectly: destination resolve failed for Piece {pieceNumber} steps {steps}: {reason}");
                return;
            }

            gameManager.NotifyMoveStarted(this, steps);
            StartCoroutine(MoveToDestination(resolvedDest, resolvedDestIndex, steps));
            return;
        }

        // Complete path get karo (route + home)
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        // Route path get karo (wrap-around logic mate - sirf route path na last element use karo)
        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1; // Route path na last index (home path exclude)
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2); // NEW: second-last route tile is entry gate to home

        // Calculate destination index
        // ============================================
        // BACKWARD MOVE LOGIC - ALL INDICES
        // ============================================
        // NEW LOGIC: Backward moves (negative steps) ma:
        // 1. Pehla current index thi index 0 sudhi backward move karo
        // 2. Pachhi jo steps baki hoy (remainingSteps = steps + currentIndex) to:
        //    - If remainingSteps < 0: list na last elements levo (destIndex = routePathLastIndex + remainingSteps + 1)
        //    - If remainingSteps >= 0: sirf index 0 par pahochi gay (destIndex = 0)
        //
        // This logic applies to ALL indices (0, 1, 2, 3, etc.), not just index 0!
        //
        // ASSUMPTIONS:
        // - completePath = [Pos0, Pos1, Pos2, ..., Pos39, HomePos] (total 40 elements)
        // - routePathLastIndex = 39 (route path na last index, home path exclude)
        //
        // ============================================
        int destIndex;
        
        if (currentPathIndex == -1)
        {
            // Piece home/start ma che, to list na 0 number thi count kar
            // Example: steps = 5 → destIndex = 5 - 1 = 4 (0-based: 0,1,2,3,4 = 5 steps)
            destIndex = steps - 1;
        }
        else
        {
            // Piece already path par che, current index thi steps add karo
            int currentIndex = currentPathIndex;
            
            // ============================================
            // BACKWARD MOVE LOGIC - NEW IMPLEMENTATION
            // ============================================
            // IMPORTANT: Backward moves (negative steps) ma:
            // 1. Pehla index 0 sudhi backward move karo
            // 2. Pachhi index 0 pachhi jo steps baki hoy to list na last elements levo
            // 
            // FORMULA:
            // - Steps needed to reach index 0 = currentIndex
            // - Remaining steps after reaching index 0 = steps + currentIndex
            // - If remainingSteps < 0, then: destIndex = routePathLastIndex + remainingSteps
            // - If remainingSteps >= 0, then: destIndex = 0 (sirf index 0 par pahochi gay)
            //
            // EXAMPLES:
            // Example 1: Piece at index 1, steps = -4
            //   - Steps to reach index 0 = 1
            //   - Remaining steps = -4 + 1 = -3
            //   - destIndex = 39 + (-3) = 36
            //   - Movement: index 1 → index 0 → list na last 3 elements (indices 39, 38, 37)
            //   - Result: Index 36 (but user says 37, so maybe formula is: routePathLastIndex + remainingSteps + 1?)
            //
            // Example 2: Piece at index 2, steps = -4
            //   - Steps to reach index 0 = 2
            //   - Remaining steps = -4 + 2 = -2
            //   - destIndex = 39 + (-2) = 37
            //   - Movement: index 2 → index 1 → index 0 → list na last 2 elements (indices 39, 38)
            //   - Result: Index 37 (but user says 38)
            //
            // Example 3: Piece at index 3, steps = -4
            //   - Steps to reach index 0 = 3
            //   - Remaining steps = -4 + 3 = -1
            //   - destIndex = 39 + (-1) = 38
            //   - Movement: index 3 → index 2 → index 1 → index 0 → list na last 1 element (index 39)
            //   - Result: Index 38 (but user says 39)
            //
            // WAIT - Let me recalculate based on user's examples:
            // User says: "index 1 ae hoy to ae index 0 ane list na last 3 element leshe"
            //   - This means: from index 1, -4 move → go to index 0, then take last 3 elements
            //   - Last 3 elements from end: indices 39, 38, 37 (counting backward)
            //   - If we take last 3 elements, we end at index 37 (the 3rd from last)
            //
            // User says: "index 2 ae hoy to index 1, index 0, ne list na last na 2"
            //   - From index 2, -4 move → go to index 1, then index 0, then take last 2 elements
            //   - Last 2 elements: indices 39, 38
            //   - We end at index 38 (the 2nd from last)
            //
            // So the pattern is:
            // - Steps to reach index 0 = currentIndex
            // - Remaining steps = steps + currentIndex
            // - Elements to take from end = |remainingSteps|
            // - Final destination = routePathLastIndex - (|remainingSteps| - 1)
            //   OR: destIndex = routePathLastIndex + remainingSteps + 1
            //
            // Let me verify:
            // - Index 1, -4: remaining = -3, dest = 39 + (-3) + 1 = 37 ✓
            // - Index 2, -4: remaining = -2, dest = 39 + (-2) + 1 = 38 ✓
            // - Index 3, -4: remaining = -1, dest = 39 + (-1) + 1 = 39 ✓
            // - Index 0, -4: remaining = -4, dest = 39 + (-4) + 1 = 36
            //   But wait, from index 0, we should go: 0 → last 4 elements (39, 38, 37, 36) → index 36 ✓
            //
            // CORRECT FORMULA:
            // - For backward moves (steps < 0):
            //   - Steps to reach index 0 = currentIndex
            //   - Remaining steps = steps + currentIndex
            //   - If remainingSteps < 0: destIndex = routePathLastIndex + remainingSteps + 1
            //   - If remainingSteps >= 0: destIndex = 0 (just reached index 0, no further movement)
            // ============================================
            if (steps < 0)
            {
                // CASE: Backward move (negative steps)
                // Rule:
                // - Normal backward: destIndex = currentIndex + steps
                // - If it goes below 0, wrap from end of route path:
                //   destIndex = routePathLastIndex + destIndex + 1
                if (currentIndex >= routePathLength)
                {
                    int homeStartIndex = routePathLength;
                    int homeOffset = currentIndex - homeStartIndex;
                    int absSteps = -steps;

                    if (absSteps <= homeOffset)
                    {
                        destIndex = currentIndex - absSteps;
                    }
                    else
                    {
                        int remainingAfterExit = absSteps - (homeOffset + 1);
                        destIndex = routeEntryIndex - remainingAfterExit;
                        if (destIndex < 0)
                        {
                            destIndex = routeEntryIndex + destIndex + 1;
                        }
                    }
                }
                else
                {
                    destIndex = currentIndex + steps;

                    if (destIndex < 0)
                    {
                        destIndex = routePathLastIndex + destIndex + 1;
                    }
                }

                Debug.Log($"🔵 MovePieceDirectly: Piece at index {currentIndex}, backward move {steps} => destination index {destIndex}");
            }
            else
            {
                // CASE: Forward move (positive steps)
                // NEW RULE:
                // Route path na second-last index par aavya pachhi, next step thi direct home path consider thase.
                // (i.e. last route index ne forward movement mate skip kariye.)
                if (currentIndex >= 0 && currentIndex < routePathLength && currentIndex <= routeEntryIndex)
                {
                    int stepsToEntry = routeEntryIndex - currentIndex;
                    if (steps <= stepsToEntry)
                    {
                        destIndex = currentIndex + steps;
                    }
                    else
                    {
                        int remainingAfterEntryToHome = steps - stepsToEntry - 1; // 1 step = entryIndex -> home[0]
                        destIndex = routePathLength + remainingAfterEntryToHome; // home starts at index routePathLength
                    }
                }
                else
                {
                    destIndex = currentIndex + steps;
                }

                Debug.Log($"🔵 MovePieceDirectly: Piece at index {currentIndex}, FORWARD move {steps} steps = destination index {destIndex} (routeEntryIndex={routeEntryIndex})");
            }
        }

        // Boundary check (ensure destIndex is within valid range)
        // ============================================
        // BOUNDARY CHECK EXAMPLES
        // ============================================
        // The new backward movement logic already handles all cases correctly:
        // - Backward moves from any index will either:
        //   1. Stop at index 0 (if remainingSteps >= 0)
        //   2. Continue from end of list (if remainingSteps < 0)
        // - So destIndex should never be < 0 for backward moves
        // - Forward moves might exceed list bounds, so we clamp them
        //
        // Example 1: Piece at index 35, steps = +10
        //   - destIndex = 35 + 10 = 45
        //   - destIndex >= completePath.Count (40), so clamp to 39
        //   - Result: Piece moves to index 39 (last position)
        // ============================================
        if (destIndex < 0)
        {
            // This should rarely happen with new logic, but safety check
            destIndex = 0;
            Debug.LogWarning($"🔵 MovePieceDirectly: destIndex was negative ({destIndex}), clamped to 0");
        }
        else if (destIndex >= completePath.Count)
        {
            // HOME exact-count rule: overshoot is invalid.
            Debug.LogWarning($"❌ MovePieceDirectly: destination index {destIndex} out of bounds (path count: {completePath.Count}). Move invalid (exact-count HOME rule).");
            return;
        }

        // Destination position get karo
        Transform destPosition = completePath[destIndex];
        if (destPosition == null)
        {
            Debug.LogError($"Position at index {destIndex} is null!");
            return;
        }

        // Direct move karo (original steps pass karo - backward/forward detection mate)
        StartCoroutine(MoveToDestination(destPosition, destIndex, steps));
    }

    /// <summary>
    /// Piece ne card value anusar move karo (direct move - use nahi kariye, step by step use karo)
    /// </summary>
    public void MovePiece(int steps)
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        if (isMoving)
        {
            Debug.Log("Piece is already moving!");
            return;
        }

        // Complete path get karo (route + home)
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2); // second-last route tile is entry gate to home

        // Calculate new position index
        int newIndex;
        
        if (currentPathIndex == -1)
        {
            // Piece home/start ma che, to list na 0 number thi count kar
            // Example: card +3 = move to index 2 (positions 0, 1, 2 = 3 steps from start)
            newIndex = steps - 1; // steps = 3 to index 2 (0-based: 0, 1, 2)
        }
        else
        {
            // Piece already path par che, current index thi steps add karo
            if (steps > 0 && currentPathIndex >= 0 && currentPathIndex < routePathLength && currentPathIndex <= routeEntryIndex)
            {
                int stepsToEntry = routeEntryIndex - currentPathIndex;
                if (steps <= stepsToEntry)
                {
                    newIndex = currentPathIndex + steps;
                }
                else
                {
                    int remainingAfterEntryToHome = steps - stepsToEntry - 1;
                    newIndex = routePathLength + remainingAfterEntryToHome;
                }
            }
            else
            {
                newIndex = currentPathIndex + steps;
            }
        }

        // Boundary check
        if (newIndex < 0)
        {
            newIndex = 0;
        }
        else if (newIndex >= completePath.Count)
        {
            // HOME exact-count rule: overshoot is invalid.
            Debug.LogWarning($"❌ MovePiece: destination index {newIndex} out of bounds (path count: {completePath.Count}). Move invalid (exact-count HOME rule).");
            return;
        }

        // New position get karo
        Transform newPosition = completePath[newIndex];
        if (newPosition == null)
        {
            Debug.LogError($"Position at index {newIndex} is null!");
            return;
        }

        // Move piece to new position (direct movement - original steps pass karo)
        StartCoroutine(MoveToDestination(newPosition, newIndex, steps));
    }

    /// <summary>
    /// Destination par move karo (path par step-by-step jumping animation - Ludo style)
    /// </summary>
    /// <param name="targetPosition">Target position transform</param>
    /// <param name="targetIndex">Target index in path</param>
    /// <param name="originalSteps">Original steps value (positive = forward, negative = backward). Default 0 = unknown.</param>
    private IEnumerator MoveToDestination(Transform targetPosition, int targetIndex, int originalSteps = 0, bool suppressOnPieceMovedCallback = false)
    {
        if (isMoving)
        {
            yield break;
        }

        ClearAllPiecesHighlights();
        if (gameManager != null)
        {
            foreach (PlayerPiece p in gameManager.GetAllActivePiecesForGameplay())
            {
                if (p == null) continue;
                p.SetSwapTargetHighlight(false);
            }
        }

        StopTurnHighlight();
        if (gameManager != null)
        {
            gameManager.StopAllTurnPieceHighlights();
        }

        isMoving = true;

        Transform movementRoot = GetMovementRoot();
        if (movementRoot != null)
        {
            RectTransform rt = transform as RectTransform;
            if (rt != null)
            {
                rt.SetParent(movementRoot, true);
            }
            else
            {
                transform.SetParent(movementRoot, true);
            }
        }
        
        // Move start thayu che - Order in Layer 3 karo (piece top par render thase)
        Canvas pieceCanvas = GetComponent<Canvas>();
        int originalOrderInLayer = 1; // Default order
        if (pieceCanvas != null)
        {
            originalOrderInLayer = pieceCanvas.sortingOrder;
            pieceCanvas.sortingOrder = 45; // Move karta piece top par render thase
            Debug.Log($"🔵 Piece {pieceNumber} move start - Order in Layer set to 45");
        }
        
        // Store canvas reference for later use (move complete pachhi restore kariye)
        Canvas canvasForRestore = pieceCanvas;
        int orderToRestore = originalOrderInLayer;

        // Complete path get karo
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            isMoving = false;
            yield break;
        }

        // Route path get karo (wrap-around logic mate - sirf route path na last element use karo)
        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1; // Route path na last index (home path exclude)
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2); // NEW: second-last route tile is entry gate to home

        // Start index determine karo
        int startIndex;
        if (currentPathIndex == -1)
        {
            // Piece home ma che, to index 0 thi start karo
            startIndex = 0;
        }
        else
        {
            // Piece already path par che, current index thi start karo
            startIndex = currentPathIndex;
        }

        int endIndex = targetIndex;
        int lastIndex = completePath.Count - 1;

        Transform finalHomeSpot = null;
        if (gameManager != null && endIndex == lastIndex)
        {
            finalHomeSpot = gameManager.GetFinalHomeSpot(playerNumber, pieceNumber - 1);
        }
        
        // Wrap-around check: Agar piece index 0 par che ane endIndex route path na last index che, to wrap-around backward move che
        // IMPORTANT: Wrap-around logic sirf backward moves mate apply karo, forward moves mate nahi!
        // IMPORTANT: Route path na last index use karo, complete path na last index nahi (home path exclude)
        bool isWrapAroundBackward = (startIndex == 0 && endIndex == routePathLastIndex);
        
        // Wrap-around + backward: Sirf tab apply karo jyare:
        // 1. Piece index 0 par che
        // 2. endIndex < routePathLastIndex che (wrap-around pachhi backward move - route path ma)
        // 3. originalSteps < 0 che (backward move confirmed)
        bool isWrapAroundWithMoreSteps = false;
        if (startIndex == 0 && endIndex < routePathLastIndex && endIndex > 0)
        {
            // Original steps check: Agar originalSteps < 0 che, to backward move che (wrap-around + backward)
            // Agar originalSteps > 0 che ya 0 che (unknown), to forward move che (normal forward)
            if (originalSteps < 0)
            {
                // Backward move confirmed - wrap-around + backward
                isWrapAroundWithMoreSteps = true;
                Debug.Log($"🔵 MoveToDestination: Wrap-around + backward detected (startIndex={startIndex}, endIndex={endIndex}, routePathLastIndex={routePathLastIndex}, originalSteps={originalSteps})");
            }
            else
            {
                // Forward move - normal forward from index 0
                Debug.Log($"🔵 MoveToDestination: Forward move from index 0 (startIndex={startIndex}, endIndex={endIndex}, originalSteps={originalSteps})");
            }
        }

        // IMPORTANT:
        // Backward move ma endIndex ghano vakhhat startIndex karta moto hovi sake (wrap-around result)
        // Example: startIndex=1, steps=-4 => endIndex=37
        // A case ma endIndex < startIndex false thase, pan move backward j che.
        // Direction should be driven by the requested steps whenever possible.
        // For forward moves, endIndex can be lower than startIndex due to wrap-around (last -> start).
        // Do NOT treat that as a backward move.
        bool isWrapAroundForward = ((originalSteps > 0 || originalSteps == 0) && startIndex == routePathLastIndex && endIndex < startIndex && endIndex >= 0 && endIndex < routePathLength);

        bool isBackward = originalSteps < 0;
        if (originalSteps == 0)
        {
            bool inferredBackwardWrapFromNonZero = (startIndex > 0 && endIndex > startIndex && (endIndex - startIndex) > (routePathLength / 2));
            isBackward = (endIndex < startIndex && !isWrapAroundForward) || isWrapAroundBackward || isWrapAroundWithMoreSteps || inferredBackwardWrapFromNonZero;
        }

        // General wrap-around backward (startIndex > 0 but endIndex > startIndex due to wrap-around destination)
        // Example: startIndex=1, endIndex=37 => 1 -> 0 -> 39 -> 38 -> 37
        bool isWrapAroundBackwardFromNonZero = (originalSteps < 0 && startIndex > 0 && endIndex > startIndex);

        // Path par step-by-step move karo (ek ek step jump karti - Ludo style)
        if (isBackward)
        {
            // Backward move from HOME PATH should always exit directly to routeEntryIndex (second-last route tile)
            // and must never animate through routePathLastIndex.
            bool isBackwardOutOfHome = (startIndex >= routePathLength && endIndex < routePathLength);

            if (isBackwardOutOfHome)
            {
                for (int i = startIndex - 1; i >= routePathLength; i--)
                {
                    if (i < 0 || i >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[i];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    Transform targetAnchor = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot : nextPosition;
                    Vector3 targetPos = GetWorldPositionWithYOffset(targetAnchor);
                    if (finalHomeSpot != null && i == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }
                    BounceObjectWithDOTween(nextPosition);

                    if (i > routePathLength)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }

                Transform entryPosition = (routeEntryIndex >= 0 && routeEntryIndex < completePath.Count) ? completePath[routeEntryIndex] : null;
                if (entryPosition != null)
                {
                    yield return StartCoroutine(MoveOneStepWithJump(transform.position, GetWorldPositionWithYOffset(entryPosition)));
                    BounceObjectWithDOTween(entryPosition);
                    if (routeEntryIndex - 1 >= endIndex)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }

                for (int i = routeEntryIndex - 1; i >= endIndex; i--)
                {
                    if (i < 0 || i >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[i];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    Transform targetAnchor = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot : nextPosition;
                    Vector3 targetPos = GetWorldPositionWithYOffset(targetAnchor);
                    if (finalHomeSpot != null && i == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }
                    BounceObjectWithDOTween(nextPosition);

                    if (i > endIndex)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }
            }
            else if (isWrapAroundBackward)
            {
                // Wrap-around backward: index 0 thi last index (direct move to last index)
                // Example: Piece index 0 par che, -1 move = last index par jase (wrap-around)
                Debug.Log($"🔵 Wrap-around backward: Moving from index {startIndex} to last index {endIndex}");
                
                Transform lastPosition = completePath[endIndex];
                if (lastPosition != null)
                {
                    // Direct move to last position (wrap-around)
                    yield return StartCoroutine(MoveOneStepWithJump(transform.position, GetWorldPositionWithYOffset(lastPosition)));
                    BounceObjectWithDOTween(lastPosition);
                }
            }
            else if (isWrapAroundWithMoreSteps)
            {
                // Wrap-around + normal backward: index 0 thi route path na last index (wrap), pachhi route path last index thi endIndex sudhi (normal backward)
                // Example: Piece index 0 par che, -2 move = route path last index (wrap 1 step), pachhi route path last-1 (normal backward 1 step)
                // IMPORTANT: Route path na last index use karo, complete path na last index nahi (home path exclude)
                Debug.Log($"🔵 Wrap-around + backward: Moving from index {startIndex} to route path last index {routePathLastIndex}, then to {endIndex}");
                
                // Step 1: Wrap to route path na last index (home path exclude)
                Transform lastPosition = completePath[routePathLastIndex];
                if (lastPosition != null)
                {
                    yield return StartCoroutine(MoveOneStepWithJump(transform.position, GetWorldPositionWithYOffset(lastPosition)));
                    BounceObjectWithDOTween(lastPosition);
                    yield return new WaitForSeconds(delayBetweenSteps);
                }
                
                // Step 2: Continue backward normally from route path last index (NO wrap-around again)
                // Loop from routePathLastIndex-1 to endIndex
                for (int i = routePathLastIndex - 1; i >= endIndex; i--)
                {
                    if (i < 0 || i >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[i];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    // Ek step move karo (jumping animation sathe - Ludo style)
                    Transform targetAnchor = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot : nextPosition;
                    Vector3 targetPos = GetWorldPositionWithYOffset(targetAnchor);
                    if (finalHomeSpot != null && i == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }

                    // Landing par object scale bounce karo (DOTween thi - scale 1 -> 0.9 -> 1)
                    BounceObjectWithDOTween(nextPosition);

                    if (i > endIndex)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }
            }
            else if (isWrapAroundBackwardFromNonZero)
            {
                // Backward + wrap-around from non-zero index.
                // Example: startIndex=1, endIndex=37, routePathLastIndex=39
                // Movement: 1 -> 0 -> 39 -> 38 -> 37
                Debug.Log($"🔵 Wrap-around backward (non-zero start): Moving from index {startIndex} down to 0, wrap to {routePathLastIndex}, then to {endIndex}");

                // Step 1: Move backward from (startIndex-1) down to 0
                for (int i = startIndex - 1; i >= 0; i--)
                {
                    if (i < 0 || i >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[i];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    Vector3 targetPos = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot.position : nextPosition.position;
                    if (finalHomeSpot != null && i == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }
                    BounceObjectWithDOTween(nextPosition);

                    if (i > 0)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }

                // Step 2: Wrap to routePathLastIndex (single step)
                Transform lastPosition = completePath[routePathLastIndex];
                if (lastPosition != null)
                {
                    yield return StartCoroutine(MoveOneStepWithJump(transform.position, GetWorldPositionWithYOffset(lastPosition)));
                    BounceObjectWithDOTween(lastPosition);
                    yield return new WaitForSeconds(delayBetweenSteps);
                }

                // Step 3: Continue backward from routePathLastIndex-1 down to endIndex
                for (int i = routePathLastIndex - 1; i >= endIndex; i--)
                {
                    if (i < 0 || i >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[i];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    Vector3 targetPos = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot.position : nextPosition.position;
                    if (finalHomeSpot != null && i == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }
                    BounceObjectWithDOTween(nextPosition);

                    if (i > endIndex)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }
            }
            else
            {
                // Normal backward movement: current position pachhi na position thi start karo (forward jem)
                // Example: Piece index 5 par che, -3 move = index 2 par jase
                // Loop: index 4, 3, 2 (currentPathIndex - 1 thi endIndex sudhi)
                int loopStartIndex = startIndex - 1; // Current position pachhi na position
                
                for (int i = loopStartIndex; i >= endIndex; i--)
                {
                    if (i < 0 || i >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[i];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    // Ek step move karo (jumping animation sathe - Ludo style)
                    Vector3 targetPos = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot.position : nextPosition.position;
                    if (finalHomeSpot != null && i == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }

                    // Landing par object scale bounce karo (DOTween thi - scale 1 -> 0.9 -> 1)
                    BounceObjectWithDOTween(nextPosition);

                    // Small delay between steps (Inspector ma adjustable)
                    if (i > endIndex)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }
            }
        }
        else
        {
            // Forward movement: current position pachhi na position thi start karo
            // Example: Piece index 5 par che, +3 move = index 8 par jase
            // Loop: index 6, 7, 8 (currentPathIndex + 1 thi endIndex sudhi)
            int loopStartIndex = (currentPathIndex == -1) ? startIndex : startIndex + 1;

            // NEW RULE (animation):
            // If we are moving forward into home path, skip the route path last tile.
            // Jump from routeEntryIndex -> home[0] directly.
            bool isForwardIntoHome = (startIndex <= routeEntryIndex && endIndex >= routePathLength);

            int i = loopStartIndex;

            // Forward wrap-around (route last tile -> start of route).
            // Example: startIndex=routePathLastIndex (39), +3 => endIndex=2
            // Movement should be: 39 -> 0 -> 1 -> 2 (forward), not backward.
            if (isWrapAroundForward)
            {
                for (int w = 0; w <= endIndex; w++)
                {
                    if (w < 0 || w >= completePath.Count)
                    {
                        break;
                    }

                    Transform nextPosition = completePath[w];
                    if (nextPosition == null)
                    {
                        continue;
                    }

                    Transform targetAnchor = (finalHomeSpot != null && w == endIndex) ? finalHomeSpot : nextPosition;
                    Vector3 targetPos = GetWorldPositionWithYOffset(targetAnchor);
                    if (finalHomeSpot != null && w == endIndex)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }
                    BounceObjectWithDOTween(nextPosition);

                    if (w < endIndex)
                    {
                        yield return new WaitForSeconds(delayBetweenSteps);
                    }
                }

                // Skip the default forward loop below.
                i = endIndex + 1;
            }

            while (i <= endIndex)
            {
                if (i >= completePath.Count)
                {
                    break;
                }

                if (isForwardIntoHome && i == routePathLastIndex)
                {
                    i = routePathLength;
                    continue;
                }

                Transform nextPosition = completePath[i];
                if (nextPosition == null)
                {
                    i++;
                    continue;
                }

                // Ek step move karo (jumping animation sathe - Ludo style)
                Transform targetAnchor = (finalHomeSpot != null && i == endIndex) ? finalHomeSpot : nextPosition;
                Vector3 targetPos = GetWorldPositionWithYOffset(targetAnchor);
                if (finalHomeSpot != null && i == endIndex)
                {
                    yield return StartCoroutine(MoveOneStepWithJumpAndSpin(transform.position, targetPos));
                }
                else
                {
                    if (currentPathIndex == -1 && i == 0 && leaveStartSpinDegrees != 0f)
                    {
                        yield return StartCoroutine(MoveOneStepWithJumpAndSpinStartExit(transform.position, targetPos, leaveStartSpinDegrees));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveOneStepWithJump(transform.position, targetPos));
                    }
                }

                // Landing par object scale bounce karo (DOTween thi - scale 1 -> 0.9 -> 1)
                BounceObjectWithDOTween(nextPosition);

                // Small delay between steps (Inspector ma adjustable)
                if (i < endIndex)
                {
                    yield return new WaitForSeconds(delayBetweenSteps);
                }

                i++;
            }
        }

        // Final destination par piece ne target object ke child bano
        int previousIndex = currentPathIndex;
        
        Transform finalParent = targetPosition;
        if (gameManager != null && pathManager != null)
        {
            List<Transform> completePathForSpot = pathManager.GetCompletePlayerPath(playerNumber);
            if (completePathForSpot != null && completePathForSpot.Count > 0)
            {
                int lastIndexForSpot = completePathForSpot.Count - 1;
                if (targetIndex == lastIndexForSpot)
                {
                    Transform spot = gameManager.GetFinalHomeSpot(playerNumber, pieceNumber - 1);
                    if (spot != null)
                    {
                        finalParent = spot;
                    }
                }
            }
        }

        if (finalParent != null)
        {
            SetPieceParent(finalParent);
            
            // Piece ne last sibling bano (tile ni agal render thase - z-order fix)
            // Hierarchy ma last child = render order ma top par
            transform.SetAsLastSibling();
            
            // Local position reset karo (parent ke center ma)
            Vector3 localPos = Vector3.zero;
            
            // Z position ensure karo (piece tile ni agal raheshe)
            // UI Canvas mate: negative Z = closer to camera (front)
            // More negative = more in front (for Screen Space - Overlay Canvas)
            if (pieceImage != null)
            {
                // UI element - more negative Z for front (top row mate pn kaam kare)
                localPos.z = -1.0f; // Increased from -0.1f to -1.0f for better visibility
                
                // Canvas component check karo (agar piece na parent ma Canvas che to)
                // Note: pieceCanvas already declared at method start, reuse it
                Canvas pieceCanvasCheck = GetComponent<Canvas>();
                if (pieceCanvasCheck == null)
                {
                    // Parent tile ma Canvas check karo
                    Canvas parentCanvas = targetPosition.GetComponentInParent<Canvas>();
                    if (parentCanvas != null)
                    {
                        // Piece ne separate Canvas par move karo (agar needed hoy to)
                        // But ahiya sirf z-position adjust kariye
                    }
                }
            }
            else if (pieceSpriteRenderer != null)
            {
                // 2D Sprite - adjust sorting order instead
                pieceSpriteRenderer.sortingOrder = 45; // Higher sorting order = renders on top (increased from 10)
                // Z position bhi adjust karo
                localPos.z = -1.0f; // Increased from -0.1f
            }
            
            ApplyPieceLocalPosition(localPos);

            if (targetPosition != null)
            {
                transform.rotation = targetPosition.rotation;
            }
            
            // Force update (ensure z-order is applied)
            if (pieceImage != null)
            {
                // CanvasRenderer force update
                CanvasRenderer canvasRenderer = GetComponent<CanvasRenderer>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.SetColor(Color.white); // Force render update
                }
            }
            
            Debug.Log($"Player {playerNumber} Piece {pieceNumber} became child of '{finalParent.name}' (z-order fixed: z={localPos.z})");
        }

        // Final position update karo
        currentPathIndex = targetIndex;
        currentPositionTransform = targetPosition;

        if (gameManager != null)
        {
            try
            {
                gameManager.HandleBumpAfterMove(this, targetPosition);
            }
            catch (Exception ex)
            {
                Debug.LogError($"🧯 MoveToDestination: exception during HandleBumpAfterMove for P{playerNumber}-#{pieceNumber}: {ex}");
                isMoving = false;
                gameManager.ForceRecoverTurn("exception in HandleBumpAfterMove");
                yield break;
            }
        }

        if (gameManager != null && !gameManager.IsPlayWithOopsMode)
        {
            yield return StartCoroutine(SafeRunEnumerator(
                PerformSlideIfNeeded(targetPosition, routePathLength, routeEntryIndex, routePathLastIndex),
                ex =>
                {
                    Debug.LogError($" MoveToDestination: exception during PerformSlideIfNeeded for P{playerNumber}-#{pieceNumber}: {ex}");
                    isMoving = false;
                    gameManager.ForceRecoverTurn("exception in PerformSlideIfNeeded");
                }));

            if (gameManager == null)
            {
                yield break;
            }
        }

        // Move complete thayu che - Order in Layer 2 karo (placed piece above tiles but below moving pieces)
        if (canvasForRestore != null)
        {
            canvasForRestore.sortingOrder = orderToRestore;
            Debug.Log($"🔵 Piece {pieceNumber} move complete - Order in Layer restored to {orderToRestore}");
        }

        isMoving = false;

        // Steps used calculate karo
        int stepsUsed;
        if (originalSteps != 0)
        {
            stepsUsed = originalSteps;
        }
        else if (previousIndex == -1)
        {
            // Piece home ma thi start thayu, to targetIndex + 1 steps
            stepsUsed = targetIndex + 1;
        }
        else
        {
            // Piece already path par che, to difference calculate karo
            // Note: routePath, routePathLength, and routePathLastIndex already declared at method start
            // Wrap-around check: Agar piece index 0 thi route path na last index par move thayu, to -1 steps (backward)
            if (previousIndex == 0 && targetIndex == routePathLastIndex)
            {
                // Wrap-around backward move: -1 step (single wrap to route path last index)
                stepsUsed = -1;
                Debug.Log($"🔵 Wrap-around backward move: Steps used = {stepsUsed} (wrapped to route path last index {routePathLastIndex})");
            }
            else if (previousIndex == 0 && targetIndex < routePathLastIndex)
            {
                // Wrap-around + normal backward: index 0 -> route path last index (wrap 1 step), then route path last index -> targetIndex (normal backward)
                // Example: previousIndex = 0, targetIndex = 38, routePathLastIndex = 39
                // Wrap uses 1 step, then backward from 39 to 38 uses (39 - 38) = 1 step
                // Total: -1 - (routePathLastIndex - targetIndex) = -1 - (39 - 38) = -2
                int backwardSteps = routePathLastIndex - targetIndex;
                stepsUsed = -(1 + backwardSteps); // -1 for wrap, -backwardSteps for normal backward
                Debug.Log($"🔵 Wrap-around + backward move: Steps used = {stepsUsed} (wrap: -1, backward: -{backwardSteps}, routePathLastIndex: {routePathLastIndex})");
            }
            else
            {
                // Normal move: difference calculate karo
                stepsUsed = targetIndex - previousIndex;
            }
        }

        Debug.Log($"Player {playerNumber} Piece {pieceNumber} moved to position {targetIndex} ({stepsUsed} steps used) and became child of '{targetPosition.name}'");

        if (gameManager != null)
        {
            gameManager.LogOopsMove(this, previousIndex, targetIndex, "MOVE");
            if (!suppressOnPieceMovedCallback)
            {
                gameManager.OnPieceMoved(this, stepsUsed);
            }
        }
    }

    private IEnumerator MoveToBaseThenServerSlide(int baseIndex, int finalIndex, int originalSteps)
    {
        if (pathManager == null)
        {
            yield break;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.NotifyMoveStarted(this, originalSteps);
        }

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            if (gameManager != null)
            {
                gameManager.NotifyMoveCompleted();
            }
            yield break;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        if (routePathLength <= 0)
        {
            if (gameManager != null)
            {
                gameManager.NotifyMoveCompleted();
            }
            yield break;
        }

        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);
        int routePathLastIndex = routePathLength - 1;

        SyncCurrentPathIndexFromTransform();

        if (baseIndex < 0 || baseIndex >= completePath.Count || finalIndex < 0 || finalIndex >= completePath.Count)
        {
            if (gameManager != null)
            {
                gameManager.NotifyMoveCompleted();
            }
            yield break;
        }

        Transform baseTarget = pathManager.GetPathPosition(playerNumber, baseIndex);
        if (baseTarget == null)
        {
            if (gameManager != null)
            {
                gameManager.NotifyMoveCompleted();
            }
            yield break;
        }

        yield return StartCoroutine(MoveToDestination(baseTarget, baseIndex, originalSteps, true));

        SlideTrigger matchedTrigger = null;
        try
        {
            SlideTrigger[] triggers = baseTarget.GetComponentsInParent<SlideTrigger>(true);
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; i++)
                {
                    SlideTrigger t = triggers[i];
                    if (t != null && t.ownerPlayer == playerNumber)
                    {
                        matchedTrigger = t;
                        break;
                    }
                }
            }
        }
        catch { }

        GameObject slideVisual = matchedTrigger != null ? matchedTrigger.slideVisualObject : null;
        bool useVisualSwap = slideVisual != null;
        float slideVisualZForPath = 0f;
        Tween slideTween = null;

        if (finalIndex == baseIndex)
        {
            if (gameManager != null)
            {
                gameManager.OnPieceMoved(this, originalSteps);
            }
            yield break;
        }

        if (baseIndex >= routePathLength || finalIndex >= routePathLength)
        {
            MovePieceToPathIndex(finalIndex, originalSteps);
            yield break;
        }

        if (isMoving)
        {
            yield break;
        }

        isMoving = true;

        const float slideStepDuration = 0.15f;

        if (useVisualSwap)
        {
            SetPieceVisualActive(false);
            slideVisualZForPath = slideVisual.transform.position.z;
            slideVisual.SetActive(true);
            slideVisual.transform.position = new Vector3(transform.position.x, transform.position.y, slideVisualZForPath);
            slideVisual.transform.rotation = transform.rotation;

            List<Vector3> slidePoints = new List<Vector3>();
            Vector3 startP = slideVisual.transform.position;
            startP.z = slideVisualZForPath;
            slidePoints.Add(startP);

            int sim = baseIndex;
            while (sim != finalIndex)
            {
                int nextSim = (sim == routeEntryIndex) ? 0 : sim + 1;
                if (nextSim == routePathLastIndex)
                {
                    nextSim = (routePathLastIndex == routeEntryIndex) ? 0 : routeEntryIndex;
                }

                if (nextSim < 0 || nextSim >= completePath.Count)
                {
                    break;
                }

                Transform nextPosT = completePath[nextSim];
                if (nextPosT == null)
                {
                    break;
                }

                Vector3 wp = GetWorldPositionWithYOffset(nextPosT);
                wp.z = slideVisualZForPath;
                slidePoints.Add(wp);

                sim = nextSim;
            }

            if (slidePoints.Count >= 2)
            {
                slideVisual.transform.DOKill();
                slideTween = slideVisual.transform.DOPath(
                        slidePoints.ToArray(),
                        slideStepDuration * (slidePoints.Count - 1),
                        PathType.CatmullRom,
                        PathMode.TopDown2D)
                    .SetEase(Ease.InOutSine);
            }
        }

        int current = baseIndex;
        while (current != finalIndex)
        {
            int next = (current == routeEntryIndex) ? 0 : current + 1;
            if (next == routePathLastIndex)
            {
                next = (routePathLastIndex == routeEntryIndex) ? 0 : routeEntryIndex;
            }

            if (next < 0 || next >= completePath.Count)
            {
                break;
            }

            Transform nextPosition = completePath[next];
            if (nextPosition == null)
            {
                break;
            }

            if (!useVisualSwap)
            {
                yield return StartCoroutine(MoveOneStepLinear(transform.position, GetWorldPositionWithYOffset(nextPosition), slideStepDuration));
            }
            else
            {
                yield return new WaitForSeconds(slideStepDuration);
            }

            SetPieceParent(nextPosition);
            transform.SetAsLastSibling();

            Vector3 localPos = Vector3.zero;
            localPos.z = -1.0f;
            ApplyPieceLocalPosition(localPos);

            currentPathIndex = next;
            currentPositionTransform = nextPosition;

            if (gameManager != null)
            {
                gameManager.HandleBumpAnyPieceAtPosition(this, nextPosition, true);
            }

            current = next;

            if (current != finalIndex && delayBetweenSteps > 0f)
            {
                yield return new WaitForSeconds(delayBetweenSteps);
            }
        }

        if (slideTween != null && slideTween.active)
        {
            yield return slideTween.WaitForCompletion();
        }

        if (useVisualSwap)
        {
            slideVisual.SetActive(false);
            SetPieceVisualActive(true);
        }

        isMoving = false;

        if (gameManager != null)
        {
            gameManager.OnPieceMoved(this, originalSteps);
        }
    }

    private IEnumerator SafeRunEnumerator(IEnumerator routine, Action<Exception> onException)
    {
        if (routine == null)
        {
            yield break;
        }

        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext())
                {
                    yield break;
                }
                current = routine.Current;
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
                yield break;
            }

            yield return current;
        }
    }

    public IEnumerator PerformPostLandingSlideIfNeeded(Transform landedPosition)
    {
        if (landedPosition == null) yield break;
        if (pathManager == null) yield break;
        if (gameManager == null) yield break;

        List<Transform> route = pathManager.GetPlayerRoutePath(playerNumber);
        List<Transform> complete = pathManager.GetCompletePlayerPath(playerNumber);
        int routePathLength = route != null ? route.Count : (complete != null ? complete.Count : 0);
        if (routePathLength <= 0) yield break;

        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);
        int routePathLastIndex = routePathLength - 1;

        yield return StartCoroutine(SafeRunEnumerator(
            PerformSlideIfNeeded(landedPosition, routePathLength, routeEntryIndex, routePathLastIndex),
            ex =>
            {
                Debug.LogError($"🧯 PerformPostLandingSlideIfNeeded: exception during PerformSlideIfNeeded for P{playerNumber}-#{pieceNumber}: {ex}");
                gameManager.ForceRecoverTurn("exception in PerformPostLandingSlideIfNeeded");
            }));
    }

    private IEnumerator PerformSlideIfNeeded(Transform landedPosition, int routePathLength, int routeEntryIndex, int routePathLastIndex)
    {
        if (landedPosition == null)
        {
            yield break;
        }

        if (isSliding)
        {
            yield break;
        }

        isSliding = true;

        try
        {

        SyncCurrentPathIndexFromTransform();
        if (!IsOnOuterTrack())
        {
            yield break;
        }

        SlideTrigger[] triggers = landedPosition.GetComponentsInParent<SlideTrigger>(true);
        if (triggers == null || triggers.Length == 0)
        {
            yield break;
        }

        SlideTrigger matchedTrigger = null;
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null && triggers[i].ownerPlayer == playerNumber)
            {
                matchedTrigger = triggers[i];
                break;
            }
        }

        if (matchedTrigger == null)
        {
            yield break;
        }

        GameObject slideVisual = matchedTrigger.slideVisualObject;
        bool useVisualSwap = (slideVisual != null);

        int steps = Mathf.Max(0, matchedTrigger.slideSteps);
        if (steps <= 0)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.15f);

        if (useVisualSwap)
        {
            SetPieceVisualActive(false);

            float slideVisualZ = slideVisual.transform.position.z;

            slideVisual.SetActive(true);
            slideVisual.transform.position = new Vector3(transform.position.x, transform.position.y, slideVisualZ);
            slideVisual.transform.rotation = transform.rotation;
        }

        // Slide forward on outer track only.
        // Outer track end is routeEntryIndex; next after that wraps to 0.
        const float slideStepDuration = 0.15f;
        float betweenStepsDelay = useVisualSwap ? 0f : delayBetweenSteps;

        List<Transform> completePath = pathManager != null ? pathManager.GetCompletePlayerPath(playerNumber) : null;
        if (completePath == null || completePath.Count == 0)
        {
            yield break;
        }

        // Precompute the slide indices/positions so the visual can move smoothly in one tween.
        List<int> slideIndices = new List<int>(steps);
        List<Vector3> slidePoints = useVisualSwap ? new List<Vector3>(steps + 1) : null;
        float slideVisualZForPath = 0f;
        if (useVisualSwap)
        {
            slideVisualZForPath = slideVisual.transform.position.z;
            Vector3 startP = slideVisual.transform.position;
            startP.z = slideVisualZForPath;
            slidePoints.Add(startP);
        }

        int simulatedIndex = currentPathIndex;
        for (int s = 0; s < steps; s++)
        {
            if (simulatedIndex < 0 || simulatedIndex >= routePathLength)
            {
                break;
            }

            int nextIndex = (simulatedIndex == routeEntryIndex) ? 0 : simulatedIndex + 1;
            if (nextIndex == routePathLastIndex)
            {
                nextIndex = (routePathLastIndex == routeEntryIndex) ? 0 : routeEntryIndex;
            }

            if (nextIndex < 0 || nextIndex >= completePath.Count)
            {
                break;
            }

            Transform nextPosition = completePath[nextIndex];
            if (nextPosition == null)
            {
                break;
            }

            slideIndices.Add(nextIndex);
            simulatedIndex = nextIndex;

            if (useVisualSwap)
            {
                Vector3 p = GetWorldPositionWithYOffset(nextPosition);
                p.z = slideVisualZForPath;
                slidePoints.Add(p);
            }
        }

        Tween slideTween = null;
        if (useVisualSwap && slidePoints.Count >= 2)
        {
            slideVisual.transform.DOKill();
            slideTween = slideVisual.transform.DOPath(
                    slidePoints.ToArray(),
                    slideStepDuration * (slidePoints.Count - 1),
                    PathType.CatmullRom,
                    PathMode.TopDown2D)
                .SetEase(Ease.InOutSine);
        }

        if (slideHapticsCoroutine != null)
        {
            StopCoroutine(slideHapticsCoroutine);
            slideHapticsCoroutine = null;
        }
        slideHapticsCoroutine = StartCoroutine(SlideHapticsLoop());

        for (int s = 0; s < slideIndices.Count; s++)
        {
            int nextIndex = slideIndices[s];
            if (nextIndex < 0 || nextIndex >= completePath.Count)
            {
                break;
            }

            Transform nextPosition = completePath[nextIndex];
            if (nextPosition == null)
            {
                break;
            }

            if (!useVisualSwap)
            {
                yield return StartCoroutine(MoveOneStepLinear(transform.position, GetWorldPositionWithYOffset(nextPosition), slideStepDuration));
            }
            else
            {
                yield return new WaitForSeconds(slideStepDuration);
            }

            SetPieceParent(nextPosition);
            transform.SetAsLastSibling();

            Vector3 localPos = Vector3.zero;
            localPos.z = -1.0f;
            ApplyPieceLocalPosition(localPos);

            currentPathIndex = nextIndex;
            currentPositionTransform = nextPosition;

            if (gameManager != null)
            {
                gameManager.HandleBumpAnyPieceAtPosition(this, nextPosition);
            }

            if (s < slideIndices.Count - 1 && betweenStepsDelay > 0f)
            {
                yield return new WaitForSeconds(betweenStepsDelay);
            }
        }

        if (slideTween != null && slideTween.active)
        {
            yield return slideTween.WaitForCompletion();
        }

        if (useVisualSwap)
        {
            slideVisual.SetActive(false);
            SetPieceVisualActive(true);
        }
        }
        finally
        {
            if (slideHapticsCoroutine != null)
            {
                StopCoroutine(slideHapticsCoroutine);
                slideHapticsCoroutine = null;
            }
            isSliding = false;
        }
    }

    private IEnumerator SlideHapticsLoop()
    {
        while (isSliding)
        {
            yield return new WaitForSeconds(0.12f);
        }
    }

    /// <summary>
    /// Ek step move karo (jumping animation sathe - Ludo style)
    /// </summary>
    private IEnumerator MoveOneStepWithJump(Vector3 startPos, Vector3 targetPos)
    {
        float duration = Mathf.Max(0.01f, stepJumpDuration);
        float jumpHeight = stepJumpHeight * Mathf.Max(0.01f, jumpEffectMultiplier);

        // Make sure we don't stack tweens across steps.
        transform.DOKill();

        Vector3 baseScale = transform.localScale;

        // Build a simple 3-point arc: start -> mid(up) -> end
        Vector3 mid = (startPos + targetPos) * 0.5f;
        mid.y += jumpHeight;

        float anticipation = Mathf.Clamp(duration * 0.16f, 0.02f, 0.06f);
        float travel = duration - anticipation;

        float e = Mathf.Max(0.01f, jumpEffectMultiplier);
        Vector3 squashScale = new Vector3(baseScale.x * (1f + 0.10f * e), baseScale.y * (1f - 0.10f * e), baseScale.z);
        Vector3 stretchScale = new Vector3(baseScale.x * (1f - 0.08f * e), baseScale.y * (1f + 0.12f * e), baseScale.z);
        Vector3 landSquashScale = new Vector3(baseScale.x * (1f + 0.12f * e), baseScale.y * (1f - 0.12f * e), baseScale.z);

        bool completed = false;
        Sequence seq = DOTween.Sequence();

        // Anticipation (quick squash)
        seq.Append(transform.DOScale(squashScale, anticipation).SetEase(Ease.OutQuad));

        // Jump travel (arc + stretch)
        seq.Append(transform.DOPath(
                new[] { startPos, mid, targetPos },
                travel,
                PathType.CatmullRom,
                PathMode.TopDown2D)
            .SetEase(Ease.InOutSine));
        seq.Join(transform.DOScale(stretchScale, travel * 0.55f).SetEase(Ease.OutQuad));

        // Landing (squash then settle)
        seq.Append(transform.DOScale(landSquashScale, Mathf.Clamp(duration * 0.10f, 0.02f, 0.06f)).SetEase(Ease.InQuad));
        seq.Append(transform.DOScale(baseScale, Mathf.Clamp(duration * 0.12f, 0.03f, 0.08f)).SetEase(Ease.OutBack));

        seq.OnComplete(() => completed = true);

        while (!completed)
        {
            yield return null;
        }

        // Ensure final position/scale
        transform.position = targetPos;
        transform.localScale = baseScale;
    }

    private IEnumerator MoveOneStepWithJumpAndSpinStartExit(Vector3 startPos, Vector3 targetPos, float spinDegrees)
    {
        float duration = Mathf.Max(0.01f, stepJumpDuration);
        float jumpHeight = stepJumpHeight * Mathf.Max(0.01f, jumpEffectMultiplier);

        transform.DOKill();

        Vector3 baseScale = transform.localScale;
        Quaternion baseRot = transform.rotation;

        Vector3 mid = (startPos + targetPos) * 0.5f;
        mid.y += jumpHeight;

        float anticipation = Mathf.Clamp(duration * 0.16f, 0.02f, 0.06f);
        float travel = duration - anticipation;

        float e = Mathf.Max(0.01f, jumpEffectMultiplier);
        Vector3 squashScale = new Vector3(baseScale.x * (1f + 0.10f * e), baseScale.y * (1f - 0.10f * e), baseScale.z);
        Vector3 stretchScale = new Vector3(baseScale.x * (1f - 0.08f * e), baseScale.y * (1f + 0.12f * e), baseScale.z);
        Vector3 landSquashScale = new Vector3(baseScale.x * (1f + 0.12f * e), baseScale.y * (1f - 0.12f * e), baseScale.z);

        bool completed = false;
        Sequence seq = DOTween.Sequence();

        seq.Append(transform.DOScale(squashScale, anticipation).SetEase(Ease.OutQuad));

        seq.Append(transform.DOPath(
                new[] { startPos, mid, targetPos },
                travel,
                PathType.CatmullRom,
                PathMode.TopDown2D)
            .SetEase(Ease.InOutSine));
        seq.Join(transform.DOScale(stretchScale, travel * 0.55f).SetEase(Ease.OutQuad));

        if (spinDegrees != 0f)
        {
            float z = transform.eulerAngles.z + spinDegrees;
            seq.Join(transform.DORotate(new Vector3(0f, 0f, z), travel, RotateMode.FastBeyond360).SetEase(Ease.InOutSine));
        }

        seq.Append(transform.DOScale(landSquashScale, Mathf.Clamp(duration * 0.10f, 0.02f, 0.06f)).SetEase(Ease.InQuad));
        seq.Append(transform.DOScale(baseScale, Mathf.Clamp(duration * 0.12f, 0.03f, 0.08f)).SetEase(Ease.OutBack));

        seq.OnComplete(() => completed = true);

        while (!completed)
        {
            yield return null;
        }

        transform.position = targetPos;
        transform.localScale = baseScale;
        transform.rotation = baseRot;
    }

    private IEnumerator MoveOneStepWithJumpAndSpin(Vector3 startPos, Vector3 targetPos)
    {
        float duration = Mathf.Max(0.01f, stepJumpDuration) * Mathf.Max(0.01f, finalHomeDurationMultiplier);
        float jumpHeight = stepJumpHeight * Mathf.Max(0.01f, jumpEffectMultiplier) * Mathf.Max(0.01f, finalHomeJumpMultiplier);

        if (finalHomePreDelay > 0f)
        {
            yield return new WaitForSeconds(finalHomePreDelay);
        }

        // Make sure we don't stack tweens across steps.
        transform.DOKill();

        Vector3 baseScale = transform.localScale;
        Quaternion baseRot = transform.rotation;

        // Build a simple 3-point arc: start -> mid(up) -> end
        Vector3 mid = (startPos + targetPos) * 0.5f;
        mid.y += jumpHeight;

        float anticipation = Mathf.Clamp(duration * 0.16f, 0.02f, 0.08f);
        float travel = duration - anticipation;

        float e = Mathf.Max(0.01f, jumpEffectMultiplier);
        Vector3 squashScale = new Vector3(baseScale.x * (1f + 0.12f * e), baseScale.y * (1f - 0.12f * e), baseScale.z);
        Vector3 stretchScale = new Vector3(baseScale.x * (1f - 0.10f * e), baseScale.y * (1f + 0.18f * e), baseScale.z);
        Vector3 landSquashScale = new Vector3(baseScale.x * (1f + 0.16f * e), baseScale.y * (1f - 0.16f * e), baseScale.z);

        bool completed = false;
        Sequence seq = DOTween.Sequence();

        // Anticipation (quick squash)
        Tween anticipationMoveTween = transform.DOMoveY(startPos.y - Mathf.Min(0.12f, jumpHeight * 0.18f), anticipation).SetEase(Ease.InQuad);
        if (finalHomeUseScaleEffects)
        {
            seq.Append(transform.DOScale(squashScale, anticipation).SetEase(Ease.OutQuad));
            seq.Join(anticipationMoveTween);
        }
        else
        {
            seq.Append(anticipationMoveTween);
        }

        // Jump travel (arc + stretch + spin)
        seq.Append(transform.DOPath(
                new[] { startPos, mid, targetPos },
                travel,
                PathType.CatmullRom,
                PathMode.TopDown2D)
            .SetEase(Ease.InOutSine));
        if (finalHomeUseScaleEffects)
        {
            seq.Join(transform.DOScale(stretchScale, travel * 0.55f).SetEase(Ease.OutQuad));
        }

        if (finalHomeSpinDegrees != 0f)
        {
            float z = transform.eulerAngles.z + finalHomeSpinDegrees;
            seq.Join(transform.DORotate(new Vector3(0f, 0f, z), travel, RotateMode.FastBeyond360).SetEase(Ease.InOutSine));
        }

        // Landing (squash then settle)
        if (finalHomeUseScaleEffects)
        {
            seq.Append(transform.DOScale(landSquashScale, Mathf.Clamp(duration * 0.10f, 0.02f, 0.07f)).SetEase(Ease.InQuad));
            seq.Append(transform.DOScale(baseScale, Mathf.Clamp(duration * 0.14f, 0.03f, 0.10f)).SetEase(Ease.OutBack));
        }

        seq.OnComplete(() => completed = true);

        while (!completed)
        {
            yield return null;
        }

        // Ensure final position/scale/rotation
        transform.position = targetPos;
        transform.localScale = baseScale;
        transform.rotation = baseRot;
    }

    private IEnumerator MoveOneStepLinear(Vector3 startPos, Vector3 targetPos, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = targetPos;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
    }

    /// <summary>
    /// Object scale bounce animation (DOTween thi - scale max -> min -> max)
    /// </summary>
    private void BounceObjectWithDOTween(Transform targetObject)
    {
        if (targetObject == null) return;

        // Scale values Inspector ma thi (direct set kari shaksho)
        Vector3 maxScale = Vector3.one * objectMaxScale; // e.g., 1.0
        Vector3 minScale = Vector3.one * objectMinScale; // e.g., 0.9

        targetObject.DOKill(false);
        targetObject.localScale = maxScale;

        // DOTween sequence: scale max -> min -> max
        Sequence bounceSequence = DOTween.Sequence();
        bounceSequence.SetId(targetObject);
        
        // Phase 1: Scale down (max -> min) - e.g., 1.0 -> 0.9
        bounceSequence.Append(targetObject.DOScale(minScale, objectBounceDuration / 2f).SetEase(Ease.InQuad));
        
        // Phase 2: Scale up (min -> max) - e.g., 0.9 -> 1.0
        bounceSequence.Append(targetObject.DOScale(maxScale, objectBounceDuration / 2f).SetEase(Ease.OutQuad));
        bounceSequence.OnComplete(() =>
        {
            if (targetObject != null)
            {
                targetObject.localScale = maxScale;
            }
        });
    }


    /// <summary>
    /// Piece ne show karo (game start pachhi)
    /// </summary>
    public void ShowPiece()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
           // Debug.Log($"Piece {pieceNumber} of Player {playerNumber} is now visible: {gameObject.name}");
        }
        else
        {
           // Debug.Log($"Piece {pieceNumber} of Player {playerNumber} is already visible: {gameObject.name}");
        }
    }

    /// <summary>
    /// Piece ne hide karo
    /// </summary>
    public void HidePiece()
    {
        StopTurnHighlight();
        gameObject.SetActive(false);
    }

    public void PauseHidePiece()
    {
        StopTurnHighlight();
        if (!pauseScaleStored)
        {
            pauseStoredScale = transform.localScale;
            pauseScaleStored = true;
        }

        transform.localScale = Vector3.zero;
    }

    public void PauseShowPiece()
    {
        if (pauseScaleStored)
        {
            transform.localScale = pauseStoredScale;
            pauseScaleStored = false;
        }
    }

    /// <summary>
    /// Current path index get karo
    /// </summary>
    public int GetCurrentPathIndex()
    {
        return currentPathIndex;
    }

    /// <summary>
    /// Piece home/start ma che ke nahi check karo
    /// </summary>
    public bool IsAtHome()
    {
        return currentPathIndex == -1;
    }

    /// <summary>
    /// Current position transform get karo
    /// </summary>
    public Transform GetCurrentPositionTransform()
    {
        return currentPositionTransform;
    }

    public void SetHomeTransform(Transform home)
    {
        homeTransform = home;
    }

    public void SetPieceSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        if (pieceImage != null)
        {
            pieceImage.sprite = sprite;
            pieceImage.enabled = true;
            return;
        }

        if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.sprite = sprite;
            pieceSpriteRenderer.enabled = true;
        }
    }

    public void ReturnToHome()
    {
        if (homeTransform == null)
        {
            return;
        }

        SetPieceParent(homeTransform);
        transform.SetAsLastSibling();

        Vector3 localPos = Vector3.zero;
        if (pieceImage != null)
        {
            localPos.z = -1.0f;
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.sortingOrder = 45;
            localPos.z = -1.0f;
        }

        ApplyPieceLocalPosition(localPos);
        currentPathIndex = -1;
        currentPositionTransform = homeTransform;
        ResetPieceVisuals();
    }

    private Vector3 GetWorldPositionWithYOffset(Transform parent)
    {
        if (parent == null)
        {
            return transform.position;
        }

        if (pieceImage != null)
        {
            RectTransform parentRt = parent as RectTransform;
            if (parentRt != null)
            {
                Vector2 anchor = new Vector2(0.5f, 0.5f);
                Rect r = parentRt.rect;
                Vector3 localAnchorPoint = new Vector3(
                    (anchor.x - parentRt.pivot.x) * r.width,
                    (anchor.y - parentRt.pivot.y) * r.height,
                    0f);

                Vector3 worldAnchorPoint = parentRt.TransformPoint(localAnchorPoint);
                worldAnchorPoint.z = transform.position.z;
                return worldAnchorPoint;
            }
        }

        Vector3 world = parent.position;
        world.z = transform.position.z;
        return world;
    }

    private Vector3 GetModeBaseScale()
    {
        float s = 1f;
        if (gameManager != null && gameManager.GetActivePlayerCountPublic() >= 4)
        {
            s = 0.7f;
        }
        return Vector3.one * s;
    }

    /// <summary>
    /// Piece highlight enable karo (move possible - visual feedback)
    /// </summary>
    public void EnableHighlight()
    {
        Debug.Log($"🔵 EnableHighlight called for Player {playerNumber} Piece {pieceNumber}");
        
        // Visual highlight add karo (e.g., glow, scale, color change)
        if (pieceImage != null)
        {
            // Color bright karo (highlight effect)
            pieceImage.color = new Color(1f, 1f, 0.8f, 1f); // Light yellow tint
            Debug.Log($"✅ Piece {pieceNumber}: Image color set to yellow, scale set to 1.1");
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.color = new Color(1f, 1f, 0.8f, 1f); // Light yellow tint
            Debug.Log($"✅ Piece {pieceNumber}: SpriteRenderer color set to yellow, scale set to 1.1");
        }
        else
        {
            Debug.LogWarning($"⚠️ Piece {pieceNumber}: No Image or SpriteRenderer found for highlighting!");
        }

        // Scale thoduk moto karo (highlight effect)
        Vector3 baseScale = GetModeBaseScale();
        bool is4P = gameManager != null && gameManager.GetActivePlayerCountPublic() >= 4;
        Vector3 newScale = is4P ? baseScale : (baseScale * 1.1f);
        transform.localScale = newScale;
        Debug.Log($"✅ Piece {pieceNumber}: Scale set to {newScale}");
    }

    /// <summary>
    /// Piece highlight disable karo (move not possible)
    /// </summary>
    public void DisableHighlight()
    {
        Debug.Log($"🔴 DisableHighlight called for Player {playerNumber} Piece {pieceNumber}");
        
        // Visual highlight remove karo
        if (pieceImage != null)
        {
            pieceImage.color = Color.white; // Normal color
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.color = Color.white; // Normal color
        }

        // Scale normal karo
        transform.localScale = GetModeBaseScale();
        Debug.Log($"✅ Piece {pieceNumber}: Highlight disabled (color white, scale 1.0)");
    }

    public void StartTurnHighlight()
    {
        if (!isActiveAndEnabled) return;
        if (!gameObject.activeInHierarchy) return;
        if (isMoving || isSliding) return;

        SyncCurrentPathIndexFromTransform();
        if (IsFinishedInHomePath()) return;

        Vector3 desiredBaseScale = GetModeBaseScale();

        if (!turnHighlightBaseScaleStored || turnHighlightBaseScale != desiredBaseScale)
        {
            turnHighlightBaseScale = desiredBaseScale;
            turnHighlightBaseScaleStored = true;
        }

        if (pieceImage != null)
        {
            turnHighlightBaseColor = pieceImage.color;
            turnHighlightBaseColorStored = true;
        }
        else if (pieceSpriteRenderer != null)
        {
            turnHighlightBaseColor = pieceSpriteRenderer.color;
            turnHighlightBaseColorStored = true;
        }

        if (turnHighlightTween != null)
        {
            turnHighlightTween.Kill(false);
            turnHighlightTween = null;
        }

        if (turnHighlightBaseScaleStored)
        {
            transform.localScale = turnHighlightBaseScale;
        }

        ResolveTurnGlow();
        if (turnGlow != null)
        {
            if (!turnGlowBaseScaleStored)
            {
                turnGlowBaseScale = turnGlow.localScale;
                turnGlowBaseScaleStored = true;
            }

            if (turnGlowAnimator == null)
            {
                turnGlowAnimator = turnGlow.GetComponent<Animator>();
            }

            turnGlow.gameObject.SetActive(true);

            if (turnGlowBaseScaleStored)
            {
                turnGlow.localScale = turnGlowBaseScale;
            }

            if (turnGlowAnimator != null)
            {
                turnGlowAnimator.enabled = true;
                turnGlowAnimator.Rebind();
                turnGlowAnimator.Update(0f);
                turnGlowAnimator.Play(0, -1, 0f);
            }
        }
        else
        {
            Vector3 baseScale = turnHighlightBaseScaleStored ? turnHighlightBaseScale : transform.localScale;
            bool is4P = gameManager != null && gameManager.GetActivePlayerCountPublic() >= 4;
            Vector3 low = is4P ? (baseScale * 0.96f) : baseScale;
            Vector3 peak = is4P ? baseScale : (baseScale * 1.08f);
            float d = 0.45f;

            transform.localScale = low;

            turnHighlightTween = transform.DOScale(peak, d)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(this);
        }
    }

    public void StopTurnHighlight()
    {
        if (turnHighlightTween != null)
        {
            turnHighlightTween.Kill(false);
            turnHighlightTween = null;
        }

        if (turnGlow != null)
        {
            if (turnGlowBaseScaleStored)
            {
                turnGlow.localScale = turnGlowBaseScale;
            }

            if (turnGlowAnimator != null)
            {
                turnGlowAnimator.enabled = false;
            }
            turnGlow.gameObject.SetActive(false);
        }

        turnHighlightBaseScale = GetModeBaseScale();
        turnHighlightBaseScaleStored = true;
        transform.localScale = turnHighlightBaseScale;

        if (turnHighlightBaseColorStored)
        {
            if (pieceImage != null)
            {
                pieceImage.color = turnHighlightBaseColor;
            }
            else if (pieceSpriteRenderer != null)
            {
                pieceSpriteRenderer.color = turnHighlightBaseColor;
            }
        }
    }

    void ResetPieceVisuals()
    {
        if (pieceImage != null)
        {
            pieceImage.color = Color.white;
        }
        else if (pieceSpriteRenderer != null)
        {
            pieceSpriteRenderer.color = Color.white;
        }

        transform.localScale = GetModeBaseScale();
    }

    /// <summary>
    /// Piece clickable enable/disable karo
    /// </summary>
    public void SetClickable(bool clickable)
    {
        if (pieceButton != null)
        {
            pieceButton.interactable = clickable;
            pieceButton.enabled = clickable;
        }
        if (pieceCollider != null)
        {
            pieceCollider.enabled = clickable;
        }

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = clickable;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        if (graphics != null)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null) continue;
                g.raycastTarget = clickable;
            }
        }

        if (pieceButton == null && pieceCollider == null)
        {
            Debug.LogWarning($"PlayerPiece: '{name}' cannot be made clickable because it has no Button and no Collider2D.");
        }

        if (!clickable)
        {
            ClearOpponentClickableVfx();
        }
    }

    /// <summary>
    /// Mouse click handler (2D sprite pieces mate)
    /// </summary>
    void OnMouseDown()
    {
        if (pieceSpriteRenderer != null)
        {
            OnPieceClicked();
        }
    }

    /// <summary>
    /// Split mode mate destination highlights show karo (Card 7)
    /// </summary>
    void ShowSplitDestinations(int totalSteps)
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
            return;
        }

        // Pehle sirf is piece na existing highlights clear karo (biji pieces na highlights raheshe)
        ClearThisPieceHighlights();

        // Complete path get karo
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1;
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);

        int GetForwardDestIndexForSteps(int steps)
        {
            if (steps <= 0) return currentPathIndex;

            // From home/start
            if (currentPathIndex == -1)
            {
                return steps - 1;
            }

            // If we are on route and at/before entry gate, apply the new rule that skips route last tile
            if (currentPathIndex >= 0 && currentPathIndex < routePathLength && currentPathIndex <= routeEntryIndex)
            {
                int stepsToEntry = routeEntryIndex - currentPathIndex;
                if (steps <= stepsToEntry)
                {
                    return currentPathIndex + steps;
                }

                int remainingAfterEntryToHome = steps - stepsToEntry - 1;
                return routePathLength + remainingAfterEntryToHome;
            }

            // Otherwise normal forward
            return currentPathIndex + steps;
        }

        // totalSteps destinations highlight karo
        for (int step = 1; step <= totalSteps; step++)
        {
            // If move is blocked (e.g. your own piece occupies destination), do NOT highlight.
            if (!gameManager.CheckIfMovePossible(this, step))
            {
                continue;
            }

            int destIndex = GetForwardDestIndexForSteps(step);
            if (destIndex < 0 || destIndex >= completePath.Count)
            {
                continue;
            }

            if (destIndex == routePathLastIndex)
            {
                continue;
            }

            Transform destPos = completePath[destIndex];
            if (destPos == null)
            {
                continue;
            }

            highlightedDestinations.Add(destPos);
            HighlightDestination(destPos, step);
        }

        Debug.Log($"🔵 Split Mode: {highlightedDestinations.Count} destinations highlighted for piece {pieceNumber}");
        
        // NOTE: Static dictionary ma add karo HighlightDestination() method ma thase (pehle add thase)
    }

    void ShowSplitDestinationExact(int steps)
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
            return;
        }

        ClearThisPieceHighlights();

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1;
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);

        if (steps <= 0)
        {
            return;
        }

        if (!gameManager.CheckIfMovePossible(this, steps))
        {
            return;
        }

        int destIndex;
        if (currentPathIndex == -1)
        {
            destIndex = steps - 1;
        }
        else if (currentPathIndex >= 0 && currentPathIndex < routePathLength && currentPathIndex <= routeEntryIndex)
        {
            int stepsToEntry = routeEntryIndex - currentPathIndex;
            if (steps <= stepsToEntry)
            {
                destIndex = currentPathIndex + steps;
            }
            else
            {
                int remainingAfterEntryToHome = steps - stepsToEntry - 1;
                destIndex = routePathLength + remainingAfterEntryToHome;
            }
        }
        else
        {
            destIndex = currentPathIndex + steps;
        }

        if (destIndex < 0 || destIndex >= completePath.Count)
        {
            return;
        }

        if (destIndex == routePathLastIndex)
        {
            return;
        }

        Transform destPos = completePath[destIndex];
        if (destPos == null)
        {
            return;
        }

        highlightedDestinations.Add(destPos);
        HighlightDestination(destPos, steps);
        Debug.Log($"🔵 Split Mode: 1 destination highlighted for piece {pieceNumber} (exact {steps} steps)");
    }

    /// <summary>
    /// Destination highlight karo (green color/visual effect)
    /// </summary>
    void HighlightDestination(Transform destination, int stepNumber)
    {
        if (destination == null) return;

        Color resolvedDestinationHighlightColor = destinationHighlightColor;
        if (gameManager != null)
        {
            resolvedDestinationHighlightColor = gameManager.destinationTileHighlightColor;
        }

        // Static dictionary ma add karo (tracking mate - which pieces highlighted each destination)
        // IMPORTANT: Pehle dictionary ma add karo, pachhi highlight karo
        if (!destinationToPieces.ContainsKey(destination))
        {
            destinationToPieces[destination] = new List<PlayerPiece>();
        }
        if (!destinationToPieces[destination].Contains(this))
        {
            destinationToPieces[destination].Add(this);
        }

        // Image component find karo (UI object mate)
        Image destImage = destination.GetComponent<Image>();
        if (destImage != null)
        {
            if (!destinationToOriginalImageColor.ContainsKey(destination))
            {
                destinationToOriginalImageColor[destination] = destImage.color;
            }

            destImage.color = resolvedDestinationHighlightColor;
            destImage.raycastTarget = true;
            
            // Click handler add karo
            Button destButton = destination.GetComponent<Button>();
            if (destButton == null)
            {
                destButton = destination.gameObject.AddComponent<Button>();
            }

            destButton.enabled = true;
            destButton.interactable = true;
            
            // Agar already highlighted che to existing listeners remove karo (last piece na click handler active raheshe)
            // Pan agar nahi highlighted che to pan remove karo (clean state)
            destButton.onClick.RemoveAllListeners();
            int steps = stepNumber; // Capture step number
            destButton.onClick.AddListener(() => OnDestinationClicked(destination, steps));
            
            destinationHighlightObjects.Add(destination.gameObject);
        }
        else
        {
            // SpriteRenderer find karo (2D sprite object mate)
            SpriteRenderer destSprite = destination.GetComponent<SpriteRenderer>();
            if (destSprite != null)
            {
                if (!destinationToOriginalSpriteColor.ContainsKey(destination))
                {
                    destinationToOriginalSpriteColor[destination] = destSprite.color;
                }

                destSprite.color = resolvedDestinationHighlightColor;
                
                // Collider2D add karo (click detection mate)
                Collider2D destCollider = destination.GetComponent<Collider2D>();
                if (destCollider == null)
                {
                    destCollider = destination.gameObject.AddComponent<BoxCollider2D>();
                }
                
                // Click handler add karo (OnMouseDown use kariye)
                // Agar already highlighted che to existing handler remove karo (last piece na click handler active raheshe)
                // Pan agar nahi highlighted che to pan remove karo (clean state)
                DestinationClickHandler existingHandler = destination.GetComponent<DestinationClickHandler>();
                if (existingHandler != null)
                {
                    Destroy(existingHandler);
                }
                
                DestinationClickHandler clickHandler = destination.gameObject.AddComponent<DestinationClickHandler>();
                clickHandler.Initialize(this, destination, stepNumber);
                
                destinationHighlightObjects.Add(destination.gameObject);
            }
        }
    }

    /// <summary>
    /// Destination click handler (UI Button mate)
    /// </summary>
    public void OnDestinationClicked(Transform destination, int steps)
    {
        // Card 10 mode ya Split mode check karo
        if (gameManager != null && gameManager.IsCard10Mode())
        {
            Debug.Log($"🔵 Card 10 Mode: Destination clicked - {steps} steps");
        }
        else
        {
            Debug.Log($"🔵 Split Mode: Destination clicked - {steps} steps");
        }

        if (gameManager != null)
        {
            bool canMove = gameManager.CheckIfMovePossible(this, steps);
            if (!canMove)
            {
                Debug.LogWarning($"❌ Destination click ignored: Piece {pieceNumber} cannot move {steps} steps (blocked or invalid)");
                return;
            }
        }
        
        // Sabhi pieces na highlights clear karo (destination select thayu che)
        ClearAllPiecesHighlights();

        if (gameManager != null && gameManager.IsPlayWithOopsMode && gameManager.IsSplitMode() && !gameManager.IsFirstPieceMovedInSplit())
        {
            bool stored = gameManager.TryOopsPlayCardSplitFirst(this, steps);
            if (stored)
            {
                return;
            }

            // PlayWithOops is server-authoritative: do not do local fallback.
            return;
        }

        if (gameManager != null && gameManager.IsPlayWithOopsMode && gameManager.IsSplitMode() && gameManager.IsFirstPieceMovedInSplit())
        {
            bool sent = gameManager.TryOopsPlayCardSplitSecond(this, steps);
            // PlayWithOops is server-authoritative: never do local fallback.
            return;
        }

        if (gameManager != null && gameManager.IsPlayWithOopsMode && (gameManager.IsCard10Mode() || gameManager.IsCard11Mode() || gameManager.IsSorryMode()))
        {
            // Card 10 (+10/-1), Card 11 (+11), and SORRY +4 destination clicks.
            // Always server-authoritative in PlayWithOops.
            gameManager.TryOopsPlayCardMove(this, steps);
            return;
        }
        
        // Piece ne selected destination par move karo
        MovePieceToDestination(destination, steps);
    }

    /// <summary>
    /// Piece ne selected destination par move karo (Card 10 mode ya split mode)
    /// </summary>
    void MovePieceToDestination(Transform destination, int steps)
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        if (gameManager != null)
        {
            bool canMove = gameManager.CheckIfMovePossible(this, steps);
            if (!canMove)
            {
                Debug.LogWarning($"❌ MovePieceToDestination ignored: Piece {pieceNumber} cannot move {steps} steps (blocked or invalid)");
                return;
            }
        }

        if (isMoving)
        {
            Debug.Log("Piece is already moving!");
            return;
        }

        // Complete path get karo
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        // Destination index find karo
        int destIndex = -1;
        for (int i = 0; i < completePath.Count; i++)
        {
            if (completePath[i] == destination)
            {
                destIndex = i;
                break;
            }
        }

        if (destIndex == -1)
        {
            Debug.LogError("Destination not found in path!");
            return;
        }

        // Card 10 mode ya Split mode check karo
        if (gameManager != null && gameManager.IsCard10Mode())
        {
            Debug.Log($"🔵 Card 10 Mode: Moving piece {pieceNumber} to destination index {destIndex} ({steps} steps)");
        }
        else
        {
            Debug.Log($"🔵 Split Mode: Moving piece {pieceNumber} to destination index {destIndex} ({steps} steps)");
        }
        
        if (gameManager != null)
        {
            gameManager.NotifyMoveStarted(this, steps);
        }

        // Move karo
        StartCoroutine(MoveToDestinationWithSteps(destination, destIndex, steps));
    }

    /// <summary>
    /// Move to destination with steps tracking (split mode)
    /// NOTE: MoveToDestination already calls OnPieceMoved, so duplicate call nahi kariye
    /// </summary>
    IEnumerator MoveToDestinationWithSteps(Transform targetPosition, int targetIndex, int stepsUsed)
    {
        // MoveToDestination already calls OnPieceMoved at the end with calculated steps
        // So ahiya duplicate call nahi kariye
        // stepsUsed is the original steps value (e.g., +10, -1, 7) - pass it for backward/forward detection
        yield return StartCoroutine(MoveToDestination(targetPosition, targetIndex, stepsUsed));
    }

    /// <summary>
    /// Card 10 mate destination highlights show karo (both +10 and -1 backward)
    /// 
    /// LIST ELEMENTS NA HISABE CALCULATION EXAMPLES:
    /// 
    /// Example 1: Piece Home ma che (currentIndex = -1)
    ///   - completePath = [Pos0, Pos1, Pos2, ..., Pos39, HomePos]
    ///   - Forward +10: home thi 10 steps = index 9 (0-based: 0,1,2,3,4,5,6,7,8,9 = 10 steps)
    ///   - Backward -1: home ma che to backward nahi (backwardDestIndex = -1, highlight nahi thase)
    /// 
    /// Example 2: Piece Index 0 par che (currentIndex = 0)
    ///   - completePath = [Pos0, Pos1, Pos2, ..., Pos39, HomePos]
    ///   - routePathLastIndex = 39 (route path na last index, home path exclude)
    ///   - Forward +10: 0 + 10 = index 10 (Pos10 par jase)
    ///   - Backward -1: index 0 thi backward = wrap-around to route path last index = 39 (Pos39 par jase)
    /// 
    /// Example 3: Piece Index 5 par che (currentIndex = 5)
    ///   - completePath = [Pos0, Pos1, Pos2, Pos3, Pos4, Pos5, Pos6, ..., Pos39, HomePos]
    ///   - Forward +10: 5 + 10 = index 15 (Pos15 par jase)
    ///   - Backward -1: 5 - 1 = index 4 (Pos4 par jase)
    /// 
    /// Example 4: Piece Index 35 par che (currentIndex = 35)
    ///   - completePath = [Pos0, ..., Pos35, Pos36, Pos37, Pos38, Pos39, HomePos]
    ///   - Forward +10: 35 + 10 = index 45 (agar completePath.Count = 40 to index 39 par clamp thase)
    ///   - Backward -1: 35 - 1 = index 34 (Pos34 par jase)
    /// 
    /// Example 5: Piece Index 39 par che (route path na last index)
    ///   - completePath = [Pos0, ..., Pos39, HomePos]
    ///   - Forward +10: 39 + 10 = index 49 (agar completePath.Count = 40 to index 39 par clamp thase)
    ///   - Backward -1: 39 - 1 = index 38 (Pos38 par jase)
    /// </summary>
    void ShowCard10Destinations()
    {
        if (pathManager == null)
        {
            Debug.LogError("PlayerPathManager not found!");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
            return;
        }

        // Pehle sirf is piece na existing highlights clear karo (biji pieces na highlights raheshe)
        ClearThisPieceHighlights();

        // Complete path get karo (route path + home path)
        // Example: completePath = [Pos0, Pos1, Pos2, ..., Pos39, HomePos] (total 40 elements)
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            Debug.LogError($"Player {playerNumber} path not found!");
            return;
        }

        // Route path get karo (wrap-around logic mate - sirf route path na last element use karo)
        // Example: routePath = [Pos0, Pos1, Pos2, ..., Pos39] (home path exclude, total 40 elements)
        // routePathLastIndex = 39 (route path na last index, home path exclude)
        List<Transform> routePath = pathManager.GetPlayerRoutePath(playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1; // Route path na last index (home path exclude)

        // Current position thi possible destinations calculate karo
        // currentIndex = -1 means piece home ma che
        // currentIndex = 0+ means piece path par che (list na index)
        int currentIndex = currentPathIndex;

        bool canMoveForward = gameManager.TryGetDestinationForMove(this, 10, out int forwardDestIndex, out Transform forwardDest, out string forwardReason);
        bool canMoveBackward = gameManager.TryGetDestinationForMove(this, -1, out int backwardDestIndex, out Transform backwardDest, out string backwardReason);

        // ============================================
        // FORWARD DESTINATION HIGHLIGHT (+10 steps)
        // ============================================
        // List bounds check: forwardDestIndex must be >= 0 and < completePath.Count
        // Example: completePath.Count = 40, forwardDestIndex = 15 → valid (0 <= 15 < 40)
        // Example: completePath.Count = 40, forwardDestIndex = 45 → invalid (45 >= 40, clamp thase)
        if (canMoveForward && forwardDest != null)
        {
            if (forwardDestIndex != routePathLastIndex)
            {
                highlightedDestinations.Add(forwardDest);
                HighlightDestination(forwardDest, 10);
                Debug.Log($"🔵 Card 10: ✅ Highlighted forward destination at index {forwardDestIndex} (+10 steps) - List element: {forwardDest.name}");
            }
        }
        else if (!canMoveForward)
        {
            Debug.Log($"🔵 Card 10: ❌ Forward destination not available ({forwardReason})");
        }

        // ============================================
        // BACKWARD DESTINATION HIGHLIGHT (-1 step)
        // ============================================
        // List bounds check: backwardDestIndex must be >= 0 and < completePath.Count
        // Example: completePath.Count = 40, backwardDestIndex = 4 → valid (0 <= 4 < 40)
        if (canMoveBackward && backwardDest != null)
        {
            if (backwardDestIndex != routePathLastIndex)
            {
                highlightedDestinations.Add(backwardDest);
                HighlightDestination(backwardDest, -1);
                Debug.Log($"🔵 Card 10: ✅ Highlighted backward destination at index {backwardDestIndex} (-1 step) - List element: {backwardDest.name}");
            }
        }
        else if (!canMoveBackward)
        {
            Debug.Log($"🔵 Card 10: ❌ Backward destination not available ({backwardReason})");
        }

        // ============================================
        // FINAL RESULT SUMMARY
        // ============================================
        // highlightedDestinations list ma sabhi valid destinations add thai gay che
        // Example: highlightedDestinations = [Pos15, Pos4] (forward +10 ane backward -1 dono valid hoy to)
        // Example: highlightedDestinations = [Pos15] (sirf forward valid hoy to)
        // Example: highlightedDestinations = [] (dono blocked hoy to empty list)
        Debug.Log($"🔵 Card 10: Total {highlightedDestinations.Count} destination(s) highlighted for piece {pieceNumber}");
        
        // List elements print karo (debugging mate)
        if (highlightedDestinations.Count > 0)
        {
            string destNames = "";
            for (int i = 0; i < highlightedDestinations.Count; i++)
            {
                if (highlightedDestinations[i] != null)
                {
                    destNames += highlightedDestinations[i].name;
                    if (i < highlightedDestinations.Count - 1) destNames += ", ";
                }
            }
            Debug.Log($"🔵 Card 10: Highlighted destinations: [{destNames}]");
        }
        
        // NOTE: Static dictionary ma add karo HighlightDestination() method ma thase (pehle add thase)
        // destinationToPieces dictionary ma track kari shaksho ke kon piece kon destination highlight kari che
        
        // Agar koi pan destination highlight nahi thayu to warning
        if (highlightedDestinations.Count == 0)
        {
            Debug.LogWarning($"⚠️ Card 10: No valid destinations for piece {pieceNumber} - All moves blocked!");
            Debug.LogWarning($"   Forward destination index: {forwardDestIndex}, Backward destination index: {backwardDestIndex}");
            Debug.LogWarning($"   Complete path count: {completePath.Count}, Current index: {currentIndex}");
        }
    }

    /// <summary>
    /// Sirf is piece na destination highlights clear karo (biji pieces na highlights raheshe)
    /// </summary>
    void ClearThisPieceHighlights()
    {
        // Sirf is piece na highlighted destinations restore karo
        // Pan check karo ke biji pieces ne pan highlight kari che ke nahi
        foreach (var dest in highlightedDestinations)
        {
            if (dest != null)
            {
                // Pehle check karo ke koi biji piece pan highlight kari che ke nahi
                bool otherPiecesHighlighting = false;
                if (destinationToPieces.ContainsKey(dest))
                {
                    // Check karo ke is piece na sivay biji piece pan che ke nahi
                    otherPiecesHighlighting = destinationToPieces[dest].Count > 1 || 
                                              (destinationToPieces[dest].Count == 1 && !destinationToPieces[dest].Contains(this));
                    
                    // Static dictionary mathi is piece ne remove karo
                    destinationToPieces[dest].Remove(this);
                    
                    // Agar koi biji piece nahi che to dictionary mathi pan remove karo
                    if (destinationToPieces[dest].Count == 0)
                    {
                        destinationToPieces.Remove(dest);
                    }
                }
                
                // Agar koi biji piece pan highlight kari che to clear nahi kariye
                if (otherPiecesHighlighting)
                {
                    // Biji pieces ne pan highlight kari che, to clear nahi kariye
                    continue;
                }
                
                // Koi biji piece highlight nahi kari, to clear karo
                Image destImage = dest.GetComponent<Image>();
                if (destImage != null)
                {
                    if (destinationToOriginalImageColor.TryGetValue(dest, out Color originalColor))
                    {
                        destImage.color = originalColor;
                        destinationToOriginalImageColor.Remove(dest);
                    }
                    
                    // Button remove karo (optional - agar nahi chahiye to)
                    Button destButton = dest.GetComponent<Button>();
                    if (destButton != null)
                    {
                        destButton.onClick.RemoveAllListeners();
                    }
                }
                else
                {
                    SpriteRenderer destSprite = dest.GetComponent<SpriteRenderer>();
                    if (destSprite != null)
                    {
                        if (destinationToOriginalSpriteColor.TryGetValue(dest, out Color originalColor))
                        {
                            destSprite.color = originalColor;
                            destinationToOriginalSpriteColor.Remove(dest);
                        }
                    }
                    
                    // DestinationClickHandler remove karo
                    DestinationClickHandler clickHandler = dest.GetComponent<DestinationClickHandler>();
                    if (clickHandler != null)
                    {
                        Destroy(clickHandler);
                    }
                }
            }
        }

        highlightedDestinations.Clear();
        destinationHighlightObjects.Clear();
    }
    
    /// <summary>
    /// Sabhi pieces na destination highlights clear karo (static method - sabhi pieces mate)
    /// </summary>
    public static void ClearAllPiecesHighlights()
    {
        // Sabhi highlighted destinations restore karo
        foreach (var kvp in destinationToPieces)
        {
            Transform dest = kvp.Key;
            if (dest != null)
            {
                // Destination restore karo (original color)
                Image destImage = dest.GetComponent<Image>();
                if (destImage != null)
                {
                    if (destinationToOriginalImageColor.TryGetValue(dest, out Color originalColor))
                    {
                        destImage.color = originalColor;
                    }
                    
                    // Button remove karo
                    Button destButton = dest.GetComponent<Button>();
                    if (destButton != null)
                    {
                        destButton.onClick.RemoveAllListeners();
                    }
                }
                else
                {
                    SpriteRenderer destSprite = dest.GetComponent<SpriteRenderer>();
                    if (destSprite != null)
                    {
                        if (destinationToOriginalSpriteColor.TryGetValue(dest, out Color originalColor))
                        {
                            destSprite.color = originalColor;
                        }
                    }
                    
                    // DestinationClickHandler remove karo
                    DestinationClickHandler clickHandler = dest.GetComponent<DestinationClickHandler>();
                    if (clickHandler != null)
                    {
                        Destroy(clickHandler);
                    }
                }
            }
        }
        
        // Sabhi pieces na local lists clear karo
        PlayerPiece[] allPieces = FindObjectsOfType<PlayerPiece>();
        foreach (var piece in allPieces)
        {
            if (piece != null)
            {
                piece.highlightedDestinations.Clear();
                piece.destinationHighlightObjects.Clear();
                piece.ResetPieceVisuals();
            }
        }
        
        destinationToPieces.Clear();
        destinationToOriginalImageColor.Clear();
        destinationToOriginalSpriteColor.Clear();
    }
    
    /// <summary>
    /// Destination highlights clear karo (backward compatibility - sirf is piece mate)
    /// </summary>
    void ClearDestinationHighlights()
    {
        ClearThisPieceHighlights();
    }
}

/// <summary>
/// Destination click handler (2D sprite objects mate)
/// </summary>
public class DestinationClickHandler : MonoBehaviour
{
    private PlayerPiece playerPiece;
    private Transform destination;
    private int steps;

    public void Initialize(PlayerPiece piece, Transform dest, int stepCount)
    {
        playerPiece = piece;
        destination = dest;
        steps = stepCount;
    }

    void OnMouseDown()
    {
        if (playerPiece != null && destination != null)
        {
            playerPiece.OnDestinationClicked(destination, steps);
        }
    }
}

