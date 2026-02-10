/*
 * Game Manager - Game state, turn management, aur card value tracking
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using FancyScrollView.Example09;
using System.Text;
using DG.Tweening;
using My.UI;
using TMPro;
using NewGame.API;
using NewGame.Socket;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [Tooltip("Current player turn (1 ya 2)")]
    private int currentPlayer = 1;

    public Color pieceClickableHighlightColor = Color.green;
    public Color destinationTileHighlightColor = new Color(0f, 1f, 0f, 0.35f);

    [Header("Opponent Clickable Highlight Vfx")]
    public GameObject opponentClickableGlowPrefab;
    public GameObject opponentClickableRingPrefab;
    public float opponentClickableRingRotateSpeed = 25f;

    [Header("Daily Bonus (Optional)")]
    [SerializeField] private GameObject dailyBonusPanelPrefab = null;
    [SerializeField] private Transform dailyBonusSpawnParent = null;
    [SerializeField] private float dailyBonusSpawnDelaySeconds = 0.15f;

    [Header("Auto Login (Optional)")]
    [SerializeField] private float homeAutoLoginInternetPollSeconds = 0.5f;
    [SerializeField] private float homeAutoLoginReconnectDelaySeconds = 1.5f;
    [SerializeField] private float homeAutoLoginStabilizeSeconds = 2f;
    [SerializeField] private bool showHomeAutoLoginLoader = true;
    [SerializeField] private string homeAutoLoginLoaderBaseText = "Logging in";

    [Header("Screens")]
    [SerializeField] private ScreenManager screenManager = null;
    [SerializeField] private GameObject loginScreen = null;
    [SerializeField] private GameObject lobbyPanel = null;

    [Header("Gameplay Connection Handling (Optional)")]
    [SerializeField] private GameObject gameplayConnectionPopupRoot = null;
    [SerializeField] private TMP_Text gameplayConnectionPopupMessageText = null;
    [SerializeField] private Button gameplayConnectionPopupOkButton = null;
    [SerializeField] private string gameplayDisconnectedMessage = "Connection lost. Returning to home...";
    [SerializeField] private string gameplayErrorMessagePrefix = "Connection error. Returning to home...";

    [Header("Lobby (Optional)")]
    [SerializeField] private bool useStaticLobbiesForFriendsMode = true;

    [Header("Bot Settings")]
    [SerializeField] private bool player1IsBot = false;
    [SerializeField] private bool player2IsBot = false;
    [SerializeField] private bool player3IsBot = false;
    [SerializeField] private bool player4IsBot = false;
    [SerializeField] private float botThinkDelay = 0.35f;

    [SerializeField] private bool vsBotMode = false;

    [SerializeField] private bool localOfflineFriendsMode = false;

    [SerializeField] private bool offlineExpertMode = false;

    private bool pendingApplyOfflineFriendProfiles;
    private int pendingApplyOfflineFriendProfileCount;

    private bool gameplayConnectionPopupOpen;
    private bool gameplayConnectionReturnQueued;

    public bool IsVsBotMode => vsBotMode;
    public bool IsLocalOfflineFriendsMode => localOfflineFriendsMode;
    public bool IsOfflineExpertMode => offlineExpertMode;

    private bool IsOnlineFriendsMode => !vsBotMode && !localOfflineFriendsMode;

    private string currentRoomId = string.Empty;
    public string CurrentRoomId => currentRoomId;

    private Coroutine homeInternetAutoLoginRoutine;
    private bool homeAutoLoginInFlight;
    private bool homeAutoLoginSucceeded;
    private bool homeAutoLoginCreatedUser;
    private bool homeAutoLoginPendingAfterReconnect;
    private int homeAutoLoginFailureCount;
    private float homeAutoLoginNextAllowedTime;
    private GameLoaderPanelAnimator homeAutoLoginLoaderPanel;
    private NetworkReachability lastHomeReachability = NetworkReachability.NotReachable;

    [Serializable]
    private class HomeAutoLoginRequest
    {
        public string user_id;
        public string username;
        public bool isGuest;
    }

    [Serializable]
    private class HomeAutoLoginResponseMeta
    {
        public bool success;
        public string message;
    }

    public bool IsPlayWithOopsMode => vsBotMode && !localOfflineFriendsMode;

    private CardClickHandler pendingOopsOpenCard;
    private bool hasOopsCardOpenListener;

    private bool hasOopsPlayingCardListener;

    private bool oopsAutoCardOpenSentThisTurn;
    private bool oopsAutoMoveSentThisTurn;
    private bool oopsAutoSplitFirstSentThisTurn;
    private bool oopsAutoSplitSecondSentThisTurn;

    private void ForceAdvanceOopsTurnLocally(string reason)
    {
        if (!IsPlayWithOopsMode) return;

        Debug.LogWarning($"PlayWithOops: ForceAdvanceOopsTurnLocally: {reason}");

        // Best-effort: unlock input + card animation lock so UI can continue.
        NotifyMoveCompleted();
        cardAnimationLock = false;
        pendingSwitchTurn = false;
        suppressHumanInput = false;

        // Return the card if possible.
        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        // Reset card + modes.
        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;

        isCard10Mode = false;
        isCard11Mode = false;
        selectedPieceForCard11 = null;
        isCard12Mode = false;
        selectedPieceForCard12 = null;
        isSorryMode = false;
        selectedPieceForSorry = null;

        extraTurnPending = false;

        StopAllTurnPieceHighlights();

        // Locally advance player turn (server ideally should broadcast, but this prevents soft-lock).
        int count = GetActivePlayerCount();
        if (count <= 2)
        {
            currentPlayer = currentPlayer == 1 ? 2 : 1;
        }
        else
        {
            currentPlayer++;
            if (currentPlayer > count)
            {
                currentPlayer = 1;
            }
        }

        suppressHumanInput = currentPlayer != 1;
        UpdateTurnIndicatorUI();
        UpdateDeckTintForTurn();
        UpdatePiecesInteractivityForOopsTurn();
        StartCardPickReminderIfNeeded();
        StartTurnCountdownForCurrentPlayer();
    }

    private readonly Dictionary<int, string> serverUserIdByMappedPlayerNumber = new Dictionary<int, string>();

    private bool oopsHasDeferredTurnChange;
    private int oopsDeferredCurrentPlayer;

    private bool oopsSplitAwaitingSecondPiece;
    private bool oopsSplitMoveSent;
    private int oopsSplitFirstPawnId;
    private int oopsSplitFirstSteps;

    private readonly Dictionary<string, int> oopsPendingOriginalStepsByPawnKey = new Dictionary<string, int>();

    private void ResetOopsCardAndModesForNextTurn()
    {
        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;
        oopsSplitAwaitingSecondPiece = false;
        oopsSplitMoveSent = false;

        isCard10Mode = false;
        isCard11Mode = false;
        selectedPieceForCard11 = null;
        isCard12Mode = false;
        selectedPieceForCard12 = null;
        isSorryMode = false;
        selectedPieceForSorry = null;
    }

    private int oopsRoomUpdateSequence;
    private Coroutine oopsDeferredBaseApplyCoroutine;

    public bool TryOopsPlayCardMove(PlayerPiece piece, int steps)
    {
        if (!IsPlayWithOopsMode) return false;
        if (piece == null) return false;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected)
        {
            Debug.LogWarning("PlayWithOops: Move blocked (socket not connected)");
            return false;
        }

        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogWarning("PlayWithOops: Move blocked (roomId empty)");
            return false;
        }

        if (currentCardHandler == null)
        {
            Debug.LogWarning("PlayWithOops: Move blocked (currentCardHandler null)");
            return false;
        }
        string cardId = currentCardHandler.serverCardId;
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("PlayWithOops: Move blocked (server cardId empty)");
            return false;
        }

        string chosenMoveType = steps < 0 ? "BACKWARD" : "FORWARD";

        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "chosenMoveType", chosenMoveType },
            { "pawnId", piece.pieceNumber },
            { "roomId", currentRoomId }
        };
        if (string.Equals(chosenMoveType, "BACKWARD", StringComparison.OrdinalIgnoreCase))
        {
            payload["targetPawnId"] = "";
            payload["targetUserId"] = "";
        }

        EnsureOopsPlayingCardListener();

        oopsPendingOriginalStepsByPawnKey[$"{piece.playerNumber}:{piece.pieceNumber}"] = steps;
        socket.SendWithAck("playCard", payload, OnOopsPlayCardAckReceived);
        return true;
    }

    public bool TryOopsPlayCardSplitFirst(PlayerPiece firstPiece, int firstSteps)
    {
        if (!IsPlayWithOopsMode) return false;
        if (firstPiece == null) return false;
        if (oopsSplitMoveSent) return false;
        if (!isSplitMode) return false;
        if (firstSteps <= 0 || firstSteps > 7) return false;
        if (!CheckIfMovePossible(firstPiece, firstSteps)) return false;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected) return false;

        if (string.IsNullOrEmpty(currentRoomId)) return false;

        if (currentCardHandler == null) return false;
        string cardId = currentCardHandler.serverCardId;
        if (string.IsNullOrEmpty(cardId)) return false;

        oopsSplitFirstPawnId = firstPiece.pieceNumber;
        oopsSplitFirstSteps = firstSteps;
        remainingSteps = 7 - firstSteps;
        selectedPieceForSplit = firstPiece;
        oopsSplitAwaitingSecondPiece = remainingSteps > 0;

        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "chosenMoveType", "SPLIT" },
            { "roomId", currentRoomId },
            { "splits", new object[]
                {
                    new Dictionary<string, object>
                    {
                        { "pawnId", firstPiece.pieceNumber },
                        { "steps", firstSteps }
                    }
                }
            }
        };

        oopsSplitMoveSent = true;
        oopsPendingOriginalStepsByPawnKey[$"{firstPiece.playerNumber}:{firstPiece.pieceNumber}"] = firstSteps;

        List<PlayerPiece> pieces = GetPiecesForPlayer(firstPiece.playerNumber);
        if (pieces != null)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].SetClickable(false);
            }
        }

        EnsureOopsPlayingCardListener();
        socket.SendWithAck("playCard", payload, OnOopsPlayCardAckReceived);
        return true;
    }

    public bool TryOopsPlayCardSplitSecond(PlayerPiece secondPiece, int steps)
    {
        if (!IsPlayWithOopsMode) return false;
        if (secondPiece == null)
        {
            Debug.LogWarning("PlayWithOops: SplitSecond blocked (secondPiece is null)");
            return false;
        }
        if (oopsSplitMoveSent)
        {
            Debug.LogWarning($"PlayWithOops: SplitSecond blocked (oopsSplitMoveSent=true) pawnId={secondPiece.pieceNumber} steps={steps} remaining={remainingSteps}");
            return false;
        }
        if (!oopsSplitAwaitingSecondPiece)
        {
            // Server updates/ACKs can arrive in different orders; keep the UI responsive.
            // If we still have a split remainder, allow the second send.
            if (isSplitMode && selectedPieceForSplit != null && remainingSteps > 0)
            {
                oopsSplitAwaitingSecondPiece = true;
            }
            else
            {
                Debug.LogWarning($"PlayWithOops: SplitSecond blocked (!oopsSplitAwaitingSecondPiece) pawnId={secondPiece.pieceNumber} steps={steps} remaining={remainingSteps}");
                return false;
            }
        }
        if (!isSplitMode)
        {
            Debug.LogWarning($"PlayWithOops: SplitSecond blocked (!isSplitMode) pawnId={secondPiece.pieceNumber} steps={steps}");
            return false;
        }
        if (selectedPieceForSplit != null && secondPiece == selectedPieceForSplit)
        {
            Debug.LogWarning($"PlayWithOops: SplitSecond blocked (cannot use same pawn twice) pawnId={secondPiece.pieceNumber}");
            return false;
        }
        if (remainingSteps <= 0)
        {
            Debug.LogWarning($"PlayWithOops: SplitSecond blocked (remainingSteps<=0) remaining={remainingSteps} pawnId={secondPiece.pieceNumber}");
            return false;
        }
        if (steps != remainingSteps)
        {
            Debug.LogWarning($"PlayWithOops: SplitSecond blocked (steps!=remainingSteps) steps={steps} remaining={remainingSteps} pawnId={secondPiece.pieceNumber}");
            return false;
        }
        if (!CheckIfMovePossible(secondPiece, steps))
        {
            Debug.LogWarning($"PlayWithOops: SplitSecond blocked (move not possible) steps={steps} pawnId={secondPiece.pieceNumber}");
            return false;
        }

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected)
        {
            Debug.LogWarning("PlayWithOops: SplitSecond blocked (socket not connected)");
            return false;
        }

        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogWarning("PlayWithOops: SplitSecond blocked (roomId empty)");
            return false;
        }

        if (currentCardHandler == null)
        {
            Debug.LogWarning("PlayWithOops: SplitSecond blocked (currentCardHandler null)");
            return false;
        }
        string cardId = currentCardHandler.serverCardId;
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("PlayWithOops: SplitSecond blocked (server cardId empty)");
            return false;
        }

        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "chosenMoveType", "SPLIT" },
            { "roomId", currentRoomId },
            { "splits", new object[]
                {
                    new Dictionary<string, object>
                    {
                        { "pawnId", secondPiece.pieceNumber },
                        { "steps", steps }
                    }
                }
            }
        };

        oopsSplitMoveSent = true;
        oopsSplitAwaitingSecondPiece = false;
        remainingSteps = 0;
        oopsPendingOriginalStepsByPawnKey[$"{secondPiece.playerNumber}:{secondPiece.pieceNumber}"] = steps;

        List<PlayerPiece> pieces = GetPiecesForPlayer(secondPiece.playerNumber);
        if (pieces != null)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].SetClickable(false);
            }
        }

        EnsureOopsPlayingCardListener();
        socket.SendWithAck("playCard", payload, OnOopsPlayCardAckReceived);
        return true;
    }

    public bool TryOopsPlayCardSwap(PlayerPiece attacker, PlayerPiece target)
    {
        if (!IsPlayWithOopsMode) return false;
        if (attacker == null || target == null) return false;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected) return false;

        if (string.IsNullOrEmpty(currentRoomId)) return false;

        if (currentCardHandler == null) return false;
        string cardId = currentCardHandler.serverCardId;
        if (string.IsNullOrEmpty(cardId)) return false;

        if (!serverUserIdByMappedPlayerNumber.TryGetValue(target.playerNumber, out string targetUserId) || string.IsNullOrEmpty(targetUserId))
        {
            return false;
        }

        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "chosenMoveType", "SWAP" },
            { "pawnId", attacker.pieceNumber },
            { "roomId", currentRoomId },
            { "targetPawnId", target.pieceNumber },
            { "targetUserId", targetUserId }
        };

        EnsureOopsPlayingCardListener();
        socket.SendWithAck("playCard", payload, OnOopsPlayCardAckReceived);
        return true;
    }

    public bool TryOopsPlayCardBump(PlayerPiece attacker, PlayerPiece target)
    {
        if (!IsPlayWithOopsMode) return false;
        if (attacker == null || target == null) return false;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected) return false;

        if (string.IsNullOrEmpty(currentRoomId)) return false;

        if (currentCardHandler == null) return false;
        string cardId = currentCardHandler.serverCardId;
        if (string.IsNullOrEmpty(cardId)) return false;

        if (!serverUserIdByMappedPlayerNumber.TryGetValue(target.playerNumber, out string targetUserId) || string.IsNullOrEmpty(targetUserId))
        {
            return false;
        }

        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { "cardId", cardId },
            { "chosenMoveType", "BUMP" },
            { "pawnId", attacker.pieceNumber },
            { "roomId", currentRoomId },
            { "targetPawnId", target.pieceNumber },
            { "targetUserId", targetUserId }
        };

        EnsureOopsPlayingCardListener();
        socket.SendWithAck("playCard", payload, OnOopsPlayCardAckReceived);
        return true;
    }

    private void EnsureOopsPlayingCardListener()
    {
        if (!IsPlayWithOopsMode) return;
        if (hasOopsPlayingCardListener) return;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected) return;

        Debug.Log("PlayWithOops: attaching room update listeners (playCard + playingCard variants)");
        socket.ListenReplace("playCard", OnOopsPlayingCardReceived);
        hasOopsPlayingCardListener = true;
    }
    private void OnOopsPlayCardAckReceived(object ack)
    {
        if (!IsPlayWithOopsMode) return;
        oopsSplitMoveSent = false;
        StopOopsAutoMoveWatchdog();
        Debug.Log($"PlayWithOops: playCard ACK received (type={(ack != null ? ack.GetType().Name : "<null>")})");
        ApplyOopsRoomStateFromServerUpdate(ack, true);
    }

    private void OnOopsPlayingCardReceived(object data)
    {
        if (!IsPlayWithOopsMode) return;
        oopsSplitMoveSent = false;
        StopOopsAutoMoveWatchdog();
        Debug.Log($"PlayWithOops: playingCard update received (type={(data != null ? data.GetType().Name : "<null>")})");
        ApplyOopsRoomStateFromServerUpdate(data);
    }

    private void ApplyOopsRoomStateFromServerUpdate(object data, bool fromAck = false)
    {
        if (!IsPlayWithOopsMode) return;

        // Spectator devices might never click a card, so we must attach this listener on any room update.
        EnsureOopsCardOpenListener();

        if (data == null)
        {
            Debug.LogWarning("PlayWithOops: room update ignored (null payload)");
            return;
        }

        if (data is string s)
        {
            bool parseOk = true;
            object decoded = BestHTTP.JSON.Json.Decode(s, ref parseOk);
            if (!parseOk || decoded == null)
            {
                Debug.LogWarning($"PlayWithOops: room update payload is string (decode failed): {s}");
                return;
            }
            data = decoded;
        }

        object payload = data;
        if (payload is IList list)
        {
            // Socket.IO sometimes sends event args as object[] with multiple elements.
            // Find the first element that looks like a room update object.
            object best = null;
            for (int i = 0; i < list.Count; i++)
            {
                object candidate = list[i];
                IDictionary<string, object> d = AsStringObjectDict(candidate);
                if (d == null) continue;

                if (d.ContainsKey("room") || d.ContainsKey("players") || d.ContainsKey("data") || d.ContainsKey("success"))
                {
                    best = candidate;
                    break;
                }
            }

            if (best != null)
            {
                payload = best;
            }
            else if (list.Count > 0)
            {
                payload = list[0];
            }
        }

        IDictionary<string, object> root = AsStringObjectDict(payload);
        if (root == null)
        {
            Debug.LogWarning($"PlayWithOops: room update payload not a dict (type={payload.GetType().Name})");
            return;
        }

        Debug.Log($"PlayWithOops: room update received (keys={string.Join(",", root.Keys)})");

        if (root.TryGetValue("success", out object successObj) && successObj is bool b && b == false)
        {
            string err = ExtractServerErrorMessage(root);
            if (string.IsNullOrWhiteSpace(err)) err = "Something went wrong.";
            Debug.LogWarning($"PlayWithOops: room update indicates success=false | {err}");
            ShowTransientLoaderError(err);
            return;
        }

        if (fromAck)
        {
            return;
        }

        oopsRoomUpdateSequence++;
        int updateSeq = oopsRoomUpdateSequence;

        IDictionary<string, object> wrapped = GetDict(root, "data");
        if (wrapped != null)
        {
            root = wrapped;
        }

        IDictionary<string, object> room = GetDict(root, "room") ?? root;

        IList players = GetList(room, "players");
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("PlayWithOops: room update missing players list");
            return;
        }

        string roomId = GetString(room, "roomId", string.Empty);
        if (string.IsNullOrEmpty(roomId)) roomId = GetString(room, "_id", string.Empty);
        if (!string.IsNullOrEmpty(roomId)) currentRoomId = roomId;

        localPlayerNumber = 1;

        Dictionary<string, int> userIdToMappedPlayer = BuildOopsUserIdToMappedPlayer(players);
        Dictionary<int, string> mappedPlayerToUserId = BuildOopsMappedPlayerToUserId(userIdToMappedPlayer);

        serverUserIdByMappedPlayerNumber.Clear();
        for (int p = 1; p <= 4; p++)
        {
            if (mappedPlayerToUserId.TryGetValue(p, out string id) && !string.IsNullOrEmpty(id))
            {
                serverUserIdByMappedPlayerNumber[p] = id;
            }
        }

        int previousPlayer = currentPlayer;
        string turnUserId = ResolveTurnUserId(room, players, string.Empty);
        int desiredPlayer = ResolveMappedPlayerFromTurnUserId(turnUserId, userIdToMappedPlayer, players);

        if (moveInputLockActive && moveInputLockPlayer > 0)
        {
            oopsHasDeferredTurnChange = desiredPlayer != moveInputLockPlayer;
            oopsDeferredCurrentPlayer = desiredPlayer;
            currentPlayer = moveInputLockPlayer;
        }
        else
        {
            oopsHasDeferredTurnChange = false;
            currentPlayer = desiredPlayer;
        }

        if (!moveInputLockActive && previousPlayer != currentPlayer)
        {
            ResetOopsCardAndModesForNextTurn();
        }

        Debug.Log($"<color=#00BCD4>PlayWithOops</color>: roomId={currentRoomId} players={players.Count} turnUserId={turnUserId} mappedCurrentPlayer={currentPlayer}");
        LogOopsMapping("RoomUpdate", turnUserId, currentPlayer, mappedPlayerToUserId);

        // In PlayWithOops, suppress local input when it's not the local player's turn.
        suppressHumanInput = currentPlayer != 1;

        List<PlayerPiece> deferredBasePieces = new List<PlayerPiece>();
        List<PlayerPiece> movedPiecesThisUpdate = new List<PlayerPiece>();

        for (int i = 0; i < players.Count; i++)
        {
            IDictionary<string, object> sp = AsStringObjectDict(players[i]);
            if (sp == null) continue;

            string serverUserId = GetString(sp, "user_id", string.Empty);
            int mappedPlayerNumber = ResolveMappedPlayerForServerUserId(serverUserId, userIdToMappedPlayer, i);

            if (!string.IsNullOrEmpty(serverUserId))
            {
                serverUserIdByMappedPlayerNumber[mappedPlayerNumber] = serverUserId;
            }

            IList pawns = GetList(sp, "pawns");
            if (pawns == null) continue;

            List<PlayerPiece> pieces = GetPiecesForPlayer(mappedPlayerNumber);
            if (pieces == null || pieces.Count == 0) continue;

            for (int p = 0; p < pawns.Count; p++)
            {
                IDictionary<string, object> pawn = AsStringObjectDict(pawns[p]);
                if (pawn == null) continue;

                int pawnId = GetInt(pawn, "pawnId", 0);
                int position = GetInt(pawn, "position", -1);
                string status = GetString(pawn, "status", string.Empty);
                bool isMove = GetBool(pawn, "isMove", false);

                if (pawnId <= 0) continue;

                PlayerPiece piece = FindPieceByPawnId(pieces, pawnId);
                if (piece == null)
                {
                    Debug.LogWarning($"PlayWithOops: could not find piece for mappedP={mappedPlayerNumber} pawnId={pawnId}");
                    continue;
                }

                piece.playerNumber = mappedPlayerNumber;

                bool wantsBase = (position == -1 && string.Equals(status, "BASE", StringComparison.OrdinalIgnoreCase)) || position < 0;
                if (wantsBase)
                {
                    deferredBasePieces.Add(piece);
                }
                else if (position >= 0)
                {
                    if (isMove)
                    {
                        if (piece.IsBusy)
                        {
                            Debug.Log($"PlayWithOops: skipping animate for busy piece mappedP={mappedPlayerNumber} pawnId={pawnId} -> position={position}");
                            continue;
                        }
                        Debug.Log($"PlayWithOops: animate mappedP={mappedPlayerNumber} pawnId={pawnId} -> position={position}");
                        int originalSteps = 0;
                        string key = $"{mappedPlayerNumber}:{pawnId}";
                        if (oopsPendingOriginalStepsByPawnKey.TryGetValue(key, out int storedSteps))
                        {
                            originalSteps = storedSteps;
                            oopsPendingOriginalStepsByPawnKey.Remove(key);
                        }
                        int animateToIndex = position;
                        if (originalSteps != 0 && originalSteps > 0)
                        {
                            piece.SyncCurrentPathIndexFromTransform();
                            if (TryGetDestinationForMove(piece, originalSteps, out int preSlideIndex, out Transform preSlideTransform, out string reason))
                            {
                                if (preSlideTransform != null)
                                {
                                    SlideTrigger matched = FindMatchingSlideTrigger(preSlideTransform, mappedPlayerNumber);
                                    if (matched != null)
                                    {
                                        int slideSteps = Mathf.Max(0, matched.slideSteps);
                                        int routeLen = GetOopsRoutePathLengthForPlayer(mappedPlayerNumber);
                                        if (slideSteps > 0 && routeLen > 0 && preSlideIndex >= 0 && preSlideIndex < routeLen && position >= 0 && position < routeLen)
                                        {
                                            int routeEntryIndex = Mathf.Max(0, routeLen - 2);
                                            int slideEndIndex = SimulateSlideEndIndex(preSlideIndex, slideSteps, routeEntryIndex);
                                            if (slideEndIndex == position)
                                            {
                                                animateToIndex = preSlideIndex;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        piece.MovePieceToPathIndex(animateToIndex, originalSteps);
                        movedPiecesThisUpdate.Add(piece);
                    }
                    else
                    {
                        piece.ApplyServerPathIndexState(position);
                    }
                }
                else
                {
                    deferredBasePieces.Add(piece);
                }
            }
        }

        if (deferredBasePieces.Count > 0)
        {
            if (movedPiecesThisUpdate.Count == 0)
            {
                ApplyDeferredOopsBaseStatesNow(deferredBasePieces);
            }
            else
            {
                if (oopsDeferredBaseApplyCoroutine != null)
                {
                    StopCoroutine(oopsDeferredBaseApplyCoroutine);
                    oopsDeferredBaseApplyCoroutine = null;
                }
                oopsDeferredBaseApplyCoroutine = StartCoroutine(ApplyDeferredOopsBaseStatesAfterMoves(updateSeq, deferredBasePieces, movedPiecesThisUpdate));
            }
        }

        if (previousPlayer != currentPlayer)
        {
            StopAllTurnPieceHighlights();
        }

        UpdateTurnIndicatorUI();
        UpdateDeckTintForTurn();
        UpdatePiecesInteractivityForOopsTurn();
        StartCardPickReminderIfNeeded();

        RefreshOopsDebugFields();
    }

    private IEnumerator OopsAutoSendSplitSecondNextFrame()
    {
        // Wait one frame so the split state is fully applied (selectedPieceForSplit/remainingSteps).
        yield return null;

        if (!IsPlayWithOopsMode) yield break;
        if (gameOver || !modeSelected) yield break;
        if (currentPlayer != LocalPlayerNumber) yield break;
        if (!isSplitMode) yield break;
        if (selectedPieceForSplit == null) yield break;
        if (remainingSteps <= 0) yield break;
        // First split send may still be awaiting server ACK/update; wait briefly so we don't drop the remainder.
        float waitForAckSeconds = 2f;
        while (oopsSplitMoveSent && waitForAckSeconds > 0f)
        {
            if (!IsPlayWithOopsMode) yield break;
            if (gameOver || !modeSelected) yield break;
            if (currentPlayer != LocalPlayerNumber) yield break;
            if (!isSplitMode) yield break;
            if (selectedPieceForSplit == null) yield break;
            if (remainingSteps <= 0) yield break;

            waitForAckSeconds -= Time.deltaTime;
            yield return null;
        }

        if (oopsSplitMoveSent) yield break;

        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null || pieces.Count == 0)
        {
            ForceAdvanceOopsTurnLocally("auto split second: no pieces");
            yield break;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece second = pieces[i];
            if (second == null) continue;
            if (second == selectedPieceForSplit) continue;
            if (!CheckIfMovePossible(second, remainingSteps)) continue;

            bool sentSecond = TryOopsPlayCardSplitSecond(second, remainingSteps);
            if (sentSecond)
            {
                StartOopsAutoMoveWatchdog("auto split second (OnPieceMoved)");
                yield break;
            }
        }

        ForceAdvanceOopsTurnLocally("auto split second: no legal second pawn");
    }

    private void StartOopsAutoMoveWatchdog(string reason)
    {
        if (!IsPlayWithOopsMode) return;

        oopsAutoMoveWatchdogToken++;
        if (oopsAutoMoveWatchdogCoroutine != null)
        {
            StopCoroutine(oopsAutoMoveWatchdogCoroutine);
            oopsAutoMoveWatchdogCoroutine = null;
        }

        int token = oopsAutoMoveWatchdogToken;
        oopsAutoMoveWatchdogCoroutine = StartCoroutine(OopsAutoMoveWatchdog(token, reason));
    }

    private void StopOopsAutoMoveWatchdog()
    {
        oopsAutoMoveWatchdogToken++;
        if (oopsAutoMoveWatchdogCoroutine != null)
        {
            StopCoroutine(oopsAutoMoveWatchdogCoroutine);
            oopsAutoMoveWatchdogCoroutine = null;
        }
    }

    private float GetEffectiveTurnCountdownSeconds()
    {
        if (IsPlayWithOopsMode && oopsUseFastTurnTimers)
        {
            return oopsTurnCountdownSeconds;
        }

        return turnCountdownSeconds;
    }

    private float GetEffectiveTurnCountdownExtraSeconds()
    {
        if (IsPlayWithOopsMode && oopsUseFastTurnTimers)
        {
            return oopsTurnCountdownExtraSeconds;
        }

        return turnCountdownExtraSeconds;
    }

    private IEnumerator OopsAutoMoveWatchdog(int token, string reason)
    {
        float timeout = 6f;
        while (timeout > 0f)
        {
            if (token != oopsAutoMoveWatchdogToken) yield break;
            if (gameOver || !modeSelected) yield break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        if (token != oopsAutoMoveWatchdogToken) yield break;

        Debug.LogError($"🧯 OOPS AUTO WATCHDOG: No server update after auto playCard. Forcing recovery. reason={reason}, currentPlayer={currentPlayer}, cardPicked={cardPicked}, card={currentCardValue}, isSplitMode={isSplitMode}, remainingSteps={remainingSteps}");
        ForceRecoverTurn("oops auto watchdog timeout");
    }

    private void ApplyDeferredOopsBaseStatesNow(List<PlayerPiece> basePieces)
    {
        if (basePieces == null || basePieces.Count == 0) return;

        for (int i = 0; i < basePieces.Count; i++)
        {
            PlayerPiece p = basePieces[i];
            if (p == null) continue;
            p.ApplyServerBaseState();
        }
    }

    private IEnumerator ApplyDeferredOopsBaseStatesAfterMoves(int updateSeq, List<PlayerPiece> basePieces, List<PlayerPiece> movedPieces)
    {
        float timeout = 6f;
        float t = 0f;
        while (t < timeout)
        {
            bool anyBusy = false;
            if (movedPieces != null)
            {
                for (int i = 0; i < movedPieces.Count; i++)
                {
                    PlayerPiece p = movedPieces[i];
                    if (p == null) continue;
                    if (p.IsBusy)
                    {
                        anyBusy = true;
                        break;
                    }
                }
            }

            if (!anyBusy)
            {
                break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        ApplyDeferredOopsBaseStatesNow(basePieces);
    }

    private int GetOopsRoutePathLengthForPlayer(int playerNumber)
    {
        if (pathManager == null) return 0;
        List<Transform> route = pathManager.GetPlayerRoutePath(playerNumber);
        if (route != null && route.Count > 0) return route.Count;
        List<Transform> complete = pathManager.GetCompletePlayerPath(playerNumber);
        return complete != null ? complete.Count : 0;
    }

    private SlideTrigger FindMatchingSlideTrigger(Transform landedTransform, int playerNumber)
    {
        if (landedTransform == null) return null;
        SlideTrigger[] triggers = landedTransform.GetComponentsInParent<SlideTrigger>(true);
        if (triggers == null || triggers.Length == 0) return null;
        for (int i = 0; i < triggers.Length; i++)
        {
            SlideTrigger t = triggers[i];
            if (t == null) continue;
            if (t.ownerPlayer == playerNumber) return t;
        }
        return null;
    }

    private int SimulateSlideEndIndex(int startIndex, int slideSteps, int routeEntryIndex)
    {
        int idx = Mathf.Max(0, startIndex);
        int steps = Mathf.Max(0, slideSteps);
        int entry = Mathf.Max(0, routeEntryIndex);
        for (int i = 0; i < steps; i++)
        {
            idx = idx < entry ? idx + 1 : 0;
        }
        return idx;
    }

    private void ShowTransientLoaderError(string message, float autoHideSeconds = 5f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        GameLoaderPanelAnimator panel = ResolveActiveLoaderPanelAnimator();
        if (panel == null) return;

        if (!panel.gameObject.activeSelf)
        {
            panel.gameObject.SetActive(true);
        }

        panel.ShowError(message.Trim(), autoHideSeconds);
    }

    private static GameLoaderPanelAnimator ResolveActiveLoaderPanelAnimator()
    {
        GameLoaderPanelAnimator[] all = Resources.FindObjectsOfTypeAll<GameLoaderPanelAnimator>();
        if (all == null || all.Length == 0) return null;

        Scene activeScene = SceneManager.GetActiveScene();
        GameLoaderPanelAnimator best = null;
        GameLoaderPanelAnimator fallback = null;

        for (int i = 0; i < all.Length; i++)
        {
            GameLoaderPanelAnimator anim = all[i];
            if (anim == null) continue;

            GameObject go = anim.gameObject;
            if (go == null) continue;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (go.hideFlags != HideFlags.None) continue;

            bool inActiveScene = go.scene == activeScene;
            if (!inActiveScene && fallback == null)
            {
                fallback = anim;
            }

            string n = go.name;
            string ln = !string.IsNullOrEmpty(n) ? n.ToLowerInvariant() : string.Empty;
            bool looksLikeGameLoader = ln.Contains("gameloader") || (ln.Contains("loader") && !ln.Contains("internet"));

            if (inActiveScene && looksLikeGameLoader)
            {
                best = anim;
                break;
            }

            if (inActiveScene && best == null)
            {
                best = anim;
            }
        }

        return best != null ? best : fallback;
    }

    private static string ExtractServerErrorMessage(IDictionary<string, object> root)
    {
        if (root == null) return string.Empty;

        if (root.TryGetValue("message", out object msg) && msg != null)
        {
            string s = msg.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        if (root.TryGetValue("error", out object err) && err != null)
        {
            string s = err.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        if (root.TryGetValue("reason", out object reason) && reason != null)
        {
            string s = reason.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        IDictionary<string, object> data = GetDict(root, "data");
        if (data != null && !ReferenceEquals(data, root))
        {
            return ExtractServerErrorMessage(data);
        }

        return string.Empty;
    }

    public void TrySendCardOpenEvent()
    {
        if (!IsPlayWithOopsMode) return;

        if (cardPicked) return;

        if (currentPlayer != LocalPlayerNumber) return;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected) return;

        if (pendingOopsOpenCard == null) return;

        string userId = UserId;
        if (string.IsNullOrEmpty(userId)) return;

        string roomId = currentRoomId;
        if (string.IsNullOrEmpty(roomId)) return;

        socket.SendWithAck("cardOpen", new { user_id = userId, roomId = roomId });
    }

    public void RequestOopsCardOpen(CardClickHandler card)
    {
        if (!IsPlayWithOopsMode) return;
        if (card == null) return;

        if (cardPicked) return;

        if (currentPlayer != LocalPlayerNumber) return;

        if (pendingOopsOpenCard != null && pendingOopsOpenCard != card)
        {
            if (!pendingOopsOpenCard.isActiveAndEnabled)
            {
                pendingOopsOpenCard = null;
            }
            else
            {
                return;
            }
        }

        pendingOopsOpenCard = card;

        EnsureOopsCardOpenListener();
        TrySendCardOpenEvent();
    }

    private void EnsureOopsCardOpenListener()
    {
        if (!IsPlayWithOopsMode) return;
        if (hasOopsCardOpenListener) return;

        SocketConnection socket = SocketConnection.Instance;
        if (socket == null || socket.CurrentState != SocketState.Connected) return;

        socket.ListenReplace("cardOpen", OnOopsCardOpenReceived);
        hasOopsCardOpenListener = true;
    }

    private void OnOopsCardOpenReceived(object data)
    {
        if (!IsPlayWithOopsMode) return;

        object payload = data;
        if (payload is IList list)
        {
            object best = null;
            for (int i = 0; i < list.Count; i++)
            {
                object candidate = list[i];
                IDictionary<string, object> d = AsStringObjectDict(candidate);
                if (d == null) continue;
                best = candidate;
                if (d.ContainsKey("card") || d.ContainsKey("data") || d.ContainsKey("user_id"))
                {
                    break;
                }
            }

            if (best != null)
            {
                payload = best;
            }
            else if (list.Count > 0)
            {
                payload = list[0];
            }
        }

        IDictionary<string, object> root = AsStringObjectDict(payload);
        if (root == null)
        {
            Debug.LogWarning($"PlayWithOops: cardOpen ignored (unexpected payload type={(payload != null ? payload.GetType().Name : "<null>")})");
            return;
        }

        IDictionary<string, object> wrapped = GetDict(root, "data");
        if (wrapped != null)
        {
            root = wrapped;
        }

        string uid = GetString(root, "user_id", string.Empty);

        string localUser = UserId;
        bool isLocalUid = !string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(localUser) && string.Equals(uid, localUser, StringComparison.OrdinalIgnoreCase);

        CardClickHandler target = pendingOopsOpenCard;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = CardClickHandler.CurrentClickableCard;
        }

        if (!isLocalUid)
        {
            RefreshActiveCardDeckAnimator();
            if (activeCardDeckAnimator != null)
            {
                CardClickHandler candidate = activeCardDeckAnimator.GetLastCardHandler();
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                {
                    target = candidate;
                }
            }

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = FindLastDeckCardHandlerFallback();
            }

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"PlayWithOops: cardOpen spectator ignored (no active deck card). uid={uid}");
                return;
            }

            if (!target.enabled)
            {
                // Spectator device may disable scripts for input suppression; we still need animation.
                target.enabled = true;
            }
        }
        else
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }
        }

        IDictionary<string, object> card = GetDict(root, "card");
        if (card == null)
        {
            Debug.LogWarning($"PlayWithOops: cardOpen ignored (missing card). uid={uid}");
            return;
        }

        string cardId = GetString(card, "card_id", string.Empty);
        if (string.IsNullOrEmpty(cardId)) cardId = GetString(card, "_id", string.Empty);
        if (string.IsNullOrEmpty(cardId)) cardId = GetString(card, "cardId", string.Empty);
        if (string.IsNullOrEmpty(cardId)) cardId = GetString(card, "id", string.Empty);

        string moveType = GetString(card, "move_type", string.Empty);
        int cardValue = GetInt(card, "card_value", 0);
        int forwardSteps = GetInt(card, "forward_steps", 0);
        int backwardSteps = GetInt(card, "backward_steps", 0);
        bool isSplit = GetBool(card, "is_split", false);
        bool isSwap = GetBool(card, "is_swap", false);

        string power1 = string.Empty;
        string power2 = string.Empty;
        bool hasDual = false;
        int value1 = 0;
        int value2 = 0;

        bool isSorry = string.Equals(moveType, "SORRY", StringComparison.OrdinalIgnoreCase);
        if (isSorry)
        {
            power1 = "SORRY!";
            power2 = "+4";
            hasDual = true;
            value1 = 0;
            value2 = 4;
        }
        else if (cardValue == 10 && backwardSteps > 0)
        {
            power1 = "Move +10";
            power2 = "OR -1 backward";
            hasDual = true;
            value1 = 10;
            value2 = -Mathf.Abs(backwardSteps);
        }
        else if (string.Equals(moveType, "BACKWARD", StringComparison.OrdinalIgnoreCase) || cardValue < 0)
        {
            int back = backwardSteps;
            if (back <= 0) back = Mathf.Abs(cardValue);
            if (back <= 0) back = 4;
            power1 = $"Move -{back}";
            power2 = "Backward only";
            hasDual = true;
            value1 = -Mathf.Abs(back);
            value2 = 0;
        }
        else if (isSplit || string.Equals(moveType, "SPLIT", StringComparison.OrdinalIgnoreCase) || cardValue == 7)
        {
            int f = forwardSteps > 0 ? forwardSteps : (cardValue != 0 ? Mathf.Abs(cardValue) : 7);
            power1 = $"Move +{f}";
            power2 = "OR Split";
            hasDual = true;
            value1 = f;
            value2 = 0;
        }
        else if (isSwap || string.Equals(moveType, "SWAP", StringComparison.OrdinalIgnoreCase) || cardValue == 11)
        {
            int f = forwardSteps > 0 ? forwardSteps : (cardValue != 0 ? Mathf.Abs(cardValue) : 11);
            power1 = $"Move +{f}";
            power2 = "OR Swap";
            hasDual = true;
            value1 = f;
            value2 = 0;
        }
        else
        {
            int f = forwardSteps;
            if (f <= 0) f = cardValue != 0 ? Mathf.Abs(cardValue) : 1;
            power1 = $"Move +{f}";
            power2 = string.Empty;
            hasDual = false;
            value1 = f;
            value2 = 0;
        }

        if (!isLocalUid)
        {
            int mappedPickerPlayer = 0;
            if (!string.IsNullOrEmpty(uid) && serverUserIdByMappedPlayerNumber != null && serverUserIdByMappedPlayerNumber.Count > 0)
            {
                foreach (var kv in serverUserIdByMappedPlayerNumber)
                {
                    if (!string.IsNullOrEmpty(kv.Value) && string.Equals(kv.Value, uid, StringComparison.OrdinalIgnoreCase))
                    {
                        mappedPickerPlayer = kv.Key;
                        break;
                    }
                }
            }
            if (mappedPickerPlayer <= 0) mappedPickerPlayer = currentPlayer;

            int spriteVariant = GetCardSpriteVariantForPlayer(Mathf.Clamp(mappedPickerPlayer, 1, 4));
            Sprite face = target.ResolveCardFaceSpriteFromServer(power1, power2, hasDual, spriteVariant);
            if (face != null)
            {
                target.SetCardSprite(face);
            }

            Debug.Log($"PlayWithOops: spectator cardOpen -> flip target='{target.gameObject.name}' enabled={target.enabled} face={(face != null ? face.name : "<null>")} power1='{power1}' power2='{power2}'");
        }

        if (pendingOopsOpenCard == target)
        {
            pendingOopsOpenCard = null;
        }

        // For spectator/opponent turns we still need a handler reference so we can return the open card
        // after the move (including +7 split which completes in 2 server events).
        if (!isLocalUid)
        {
            currentCardHandler = target;
            cardPicked = true;
            currentCardValue = cardValue != 0 ? cardValue : value1;
            currentCardPower1 = power1 ?? "";
            currentCardPower2 = power2 ?? "";

            isSplitMode = (cardValue == 7) || isSplit || string.Equals(moveType, "SPLIT", StringComparison.OrdinalIgnoreCase);
            remainingSteps = isSplitMode ? 7 : 0;
            selectedPieceForSplit = null;
        }
        target.OnOopsCardOpenResponse(cardId, power1, power2, hasDual, value1, value2);
    }

    public void StartLocalOfflineGame(int desiredPlayerCount)
    {
        desiredPlayerCount = desiredPlayerCount == 4 ? 4 : 2;

        vsBotMode = false;
        localOfflineFriendsMode = true;
        offlineExpertMode = false;

        if (desiredPlayerCount == 4) Select4Player();
        else Select2Player();

        player1IsBot = false;
        player2IsBot = false;
        player3IsBot = false;
        player4IsBot = false;

        hasAppliedSocketGameStartData = false;
        gameplayInitialized = false;

        currentPlayer = 1;
        localPlayerNumber = 1;
        gameOver = false;
        winningPlayer = 0;

        cardPicked = false;
        currentCardValue = 0;
        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;
        isCard10Mode = false;
        isCard11Mode = false;
        isCard12Mode = false;
        isSorryMode = false;
        selectedPieceForCard11 = null;
        selectedPieceForCard12 = null;
        selectedPieceForSorry = null;

        EnsureHomeSlotsResolvedIfMissing();
        EnsurePiecesSpawnedIfMissing();
        ApplyDefaultPieceSpritesToExistingPieces();
        ApplyPieceScaleToExistingPieces();
        AssignHomePositionsFromStartPoints();
        StopAllTurnPieceHighlights();
        UpdateTurnIndicatorUI();

        pendingApplyOfflineFriendProfiles = true;
        pendingApplyOfflineFriendProfileCount = desiredPlayerCount;

        currentRoomId = string.Empty;
    }

    public void StartOfflineVsBotExpert()
    {
        StartOfflineVsBotExpertGame(2);
    }

    public void StartOfflineVsBotExpertGame(int desiredPlayerCount)
    {
        desiredPlayerCount = desiredPlayerCount == 4 ? 4 : 2;

        vsBotMode = true;
        localOfflineFriendsMode = true;
        offlineExpertMode = true;

        botDifficulty = BotDifficulty.Hard;

        if (desiredPlayerCount == 4) Select4Player();
        else Select2Player();

        player1IsBot = false;
        player2IsBot = true;
        player3IsBot = desiredPlayerCount >= 3;
        player4IsBot = desiredPlayerCount >= 4;

        suppressHumanInput = false;
        hasAppliedSocketGameStartData = false;
        gameplayInitialized = false;

        currentPlayer = 1;
        localPlayerNumber = 1;
        gameOver = false;
        winningPlayer = 0;

        cardPicked = false;
        currentCardValue = 0;
        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;
        isCard10Mode = false;
        isCard11Mode = false;
        isCard12Mode = false;
        isSorryMode = false;
        selectedPieceForCard11 = null;
        selectedPieceForCard12 = null;
        selectedPieceForSorry = null;

        EnsureHomeSlotsResolvedIfMissing();
        EnsurePiecesSpawnedIfMissing();
        ApplyDefaultPieceSpritesToExistingPieces();
        ApplyPieceScaleToExistingPieces();
        AssignHomePositionsFromStartPoints();
        StopAllTurnPieceHighlights();
        UpdateTurnIndicatorUI();

        pendingApplyOfflineFriendProfiles = false;
        pendingApplyOfflineFriendProfileCount = 0;

        LoadPlayerProfileFromPrefs();
        if (pathManager == null)
        {
            pathManager = FindObjectOfType<PlayerPathManager>();
        }
        if (pathManager != null)
        {
            string p1Name = !string.IsNullOrWhiteSpace(PlayerName) ? PlayerName : "Player 1";
            Sprite p1Avatar = PlayerAvatarSprite != null ? PlayerAvatarSprite : ResolveAvatarSpriteForIndex(PlayerAvatarIndex);
            pathManager.SetPlayerProfile(1, p1Name, p1Avatar);

            int spriteCount = playerAvatarSprites != null ? playerAvatarSprites.Count : 0;
            HashSet<int> usedAvatarIndices = new HashSet<int> { PlayerAvatarIndex };

            for (int p = 2; p <= desiredPlayerCount; p++)
            {
                Sprite botAvatar = null;
                if (spriteCount > 0)
                {
                    int tries = 0;
                    while (tries < spriteCount * 2)
                    {
                        int idx = UnityEngine.Random.Range(0, spriteCount);
                        tries++;
                        if (usedAvatarIndices.Contains(idx)) continue;
                        botAvatar = ResolveAvatarSpriteForIndex(idx);
                        if (botAvatar == null) continue;
                        usedAvatarIndices.Add(idx);
                        break;
                    }
                }

                string botName = desiredPlayerCount == 2 ? "Expert Bot" : $"Expert Bot {p - 1}";
                pathManager.SetPlayerProfile(p, botName, botAvatar);
            }
        }

        currentRoomId = string.Empty;

        UpdateDeckTintForTurn();
        StartCardPickReminderIfNeeded();

        ScreenManager sm = screenManager != null ? screenManager : FindObjectOfType<ScreenManager>();
        if (sm != null)
        {
            sm.OpenScreenByName("GamePlayScreen");
        }
        else
        {
            GameObject gameplay = GameObject.Find("GamePlayScreen");
            if (gameplay != null)
            {
                gameplay.SetActive(true);
                OnGameplayScreenOpened();
            }
        }
    }

    private void ApplyOfflineFriendProfilesIfNeeded()
    {
        if (!localOfflineFriendsMode) return;
        if (!pendingApplyOfflineFriendProfiles) return;

        if (pathManager == null)
        {
            pathManager = FindObjectOfType<PlayerPathManager>();
        }
        if (pathManager == null) return;

        int count = pendingApplyOfflineFriendProfileCount == 4 ? 4 : 2;

        HashSet<int> used = new HashSet<int>();
        int spriteCount = playerAvatarSprites != null ? playerAvatarSprites.Count : 0;

        for (int p = 1; p <= count; p++)
        {
            Sprite s = null;
            if (spriteCount > 0)
            {
                int tries = 0;
                while (tries < spriteCount * 2)
                {
                    int idx = UnityEngine.Random.Range(0, spriteCount);
                    tries++;
                    if (used.Contains(idx)) continue;
                    s = ResolveAvatarSpriteForIndex(idx);
                    if (s == null) continue;
                    used.Add(idx);
                    break;
                }
            }

            pathManager.SetPlayerProfile(p, $"Player {p}", s);
        }

        pendingApplyOfflineFriendProfiles = false;
    }
    private enum BotDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    void EnsureHomeSlotsResolvedIfMissing()
    {
        int count = GetActivePlayerCount();
        for (int p = 1; p <= count; p++)
        {
            Transform[] slots = GetHomeSlotsForPlayer(p);
            if (HasAnyValidSlot(slots))
            {
                continue;
            }

            Transform[] resolved = TryResolveHomeSlotsFromBoardRoot(p);
            if (resolved != null)
            {
                if (p == 1) player1HomeSlots = resolved;
                else if (p == 2) player2HomeSlots = resolved;
                else if (p == 3) player3HomeSlots = resolved;
                else if (p == 4) player4HomeSlots = resolved;

                if (debugAutoSpawnPieces)
                {
                    Debug.Log($"GameManager: Resolved home slots for player {p} from board root. [{resolved[0]?.name}, {resolved[1]?.name}, {resolved[2]?.name}]");
                }
            }
            else if (debugAutoSpawnPieces)
            {
                Debug.LogWarning($"GameManager: Failed to resolve home slots for player {p}. activeBoardRoot={(activeBoardRoot != null ? activeBoardRoot.name : "<null>")}");
            }
        }
    }

    bool HasAnyValidSlot(Transform[] slots)
    {
        if (slots == null || slots.Length == 0) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) return true;
        }
        return false;
    }

    Transform[] TryResolveHomeSlotsFromBoardRoot(int playerNumber)
    {
        GameObject boardRoot = activeBoardRoot;
        if (boardRoot == null)
        {
            // Fallback: if ApplyModeSetup wasn't called for some reason, use the active ModeSetup.
            ModeSetup active = (playerCount <= 2) ? setup2P : setup4P;
            if (active != null && active.boardRoot != null && active.boardRoot.activeInHierarchy)
            {
                boardRoot = active.boardRoot;
            }
        }

        if (boardRoot == null)
        {
            return null;
        }

        Transform root = boardRoot.transform;
        if (root == null)
        {
            return null;
        }

        string startName = $"Player{playerNumber}StartPoint";
        Transform start = FindByNameRecursive(root, startName);
        if (start == null)
        {
            return null;
        }

        Transform[] slots = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            string slotName = $"PlayerHome{i + 1}";
            Transform slot = start.Find(slotName);
            if (slot == null)
            {
                slot = FindByNameRecursive(start, slotName);
            }
            slots[i] = slot;
        }

        return HasAnyValidSlot(slots) ? slots : null;
    }

    Transform FindByNameRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindByNameRecursive(child, name);
            if (found != null) return found;
        }

        return null;
    }

    [Header("Card Reminder")]
    [SerializeField] private bool enableHumanCardPickReminder = true;
    [SerializeField] private float cardPickReminderInitialDelay = 3f;
    [SerializeField] private float cardPickReminderRepeatInterval = 2.5f;
    [SerializeField] private float cardPickReminderShakeDuration = 0.35f;
    [SerializeField] private float cardPickReminderShakeStrength = 14f;
    [SerializeField] private int cardPickReminderShakeVibrato = 18;
    [SerializeField] private bool debugCardPickReminder = false;

    [FormerlySerializedAs("cardDeckAnimator")]
    [SerializeField] private CardDeckAnimator cardDeckAnimator2P = null;
    [SerializeField] private CardDeckAnimator cardDeckAnimator4P = null;
    private CardDeckAnimator activeCardDeckAnimator = null;

    [Header("Opponent Deck Tint")]
    [SerializeField] private bool enableOpponentDeckTint = false;
    [SerializeField] private int localPlayerNumber = 1;
    [SerializeField] private Color opponentDeckTintColor = Color.gray;

    [Header("Lobby Rewards")]
    [SerializeField] private long selectedLobbyWinningCoin = 0;
    [SerializeField] private long selectedLobbyWinningDiamond = 0;

    private GameObject activeBoardRoot = null;

    private bool warnedMissingDeckAnimatorForArrow = false;

    private Coroutine cardPickReminderCoroutine = null;
    private int cardPickReminderToken = 0;

    private const string DailyBonusNextUtcTicksKey = "DAILY_BONUS_NEXT_UTC_TICKS";
    private const string PlayerCoinsKey = "PLAYER_COINS";

    public const string PlayerNameKey = "PLAYER_NAME";
    public const string PlayerAvatarIndexKey = "PLAYER_AVATAR_INDEX";

    [SerializeField] private List<Sprite> playerAvatarSprites = new List<Sprite>();

    public string PlayerName { get; private set; } = string.Empty;
    public int PlayerAvatarIndex { get; private set; } = 0;
    public Sprite PlayerAvatarSprite { get; private set; } = null;

    public string UserId => PlayerPrefs.GetString(UserSession.UserIdKey, string.Empty);
    public string Username => PlayerPrefs.GetString(UserSession.UsernameKey, PlayerName);
    public bool IsGuestUser => PlayerPrefs.GetInt(UserSession.IsGuestKey, 0) == 1;
    public string JwtToken => PlayerPrefs.GetString(UserSession.JwtTokenKey, string.Empty);
    public int Coins => Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_COINS", 0));
    public int Diamonds => Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_DIAMONDS", 0));

    [Header("Session Debug")]
    [SerializeField] private string inspectorUserId;
    [SerializeField] private string inspectorUsername;
    [SerializeField] private bool inspectorIsGuest;
    [SerializeField] private string inspectorJwtToken;
    [SerializeField] private int inspectorCoins;
    [SerializeField] private int inspectorDiamonds;

    [Header("PlayWithOops Debug")]
    [SerializeField] private string inspectorOopsRoomId;
    [SerializeField] private int inspectorOopsLocalPlayerNumber;
    [SerializeField] private int inspectorOopsCurrentPlayer;
    [SerializeField] private bool inspectorOopsSuppressHumanInput;
    [SerializeField] private string inspectorOopsP1UserId;
    [SerializeField] private string inspectorOopsP2UserId;
    [SerializeField] private string inspectorOopsP3UserId;
    [SerializeField] private string inspectorOopsP4UserId;

    public event Action ProfileChanged;

    public void RefreshSessionDebugFields()
    {
        inspectorUserId = UserId;
        inspectorUsername = Username;
        inspectorIsGuest = IsGuestUser;
        inspectorJwtToken = JwtToken;
        inspectorCoins = Coins;
        inspectorDiamonds = Diamonds;

        RefreshOopsDebugFields();
    }

    private void RefreshOopsDebugFields()
    {
        inspectorOopsRoomId = currentRoomId;
        inspectorOopsLocalPlayerNumber = LocalPlayerNumber;
        inspectorOopsCurrentPlayer = currentPlayer;
        inspectorOopsSuppressHumanInput = suppressHumanInput;

        serverUserIdByMappedPlayerNumber.TryGetValue(1, out inspectorOopsP1UserId);
        serverUserIdByMappedPlayerNumber.TryGetValue(2, out inspectorOopsP2UserId);
        serverUserIdByMappedPlayerNumber.TryGetValue(3, out inspectorOopsP3UserId);
        serverUserIdByMappedPlayerNumber.TryGetValue(4, out inspectorOopsP4UserId);
    }

    public int LocalPlayerNumber => Mathf.Clamp(localPlayerNumber, 1, 4);
    public long SelectedLobbyWinningCoin => selectedLobbyWinningCoin;
    public long SelectedLobbyWinningDiamond => selectedLobbyWinningDiamond;

    private GameObject activeDailyBonusInstance = null;
    private Coroutine dailyBonusCoroutine = null;

    private bool moveInputLockActive = false;
    private int moveInputLockPlayer = 0;

    private bool pausePopupOpen = false;

    [Header("Gameplay UI (Optional)")]
    [SerializeField] private Button gameplaySettingsButton = null;
    [SerializeField] private float gameplaySettingsButtonDisableOnOpenSeconds = 0.35f;
    private bool gameplaySettingsButtonOpenLockActive = false;
    private Coroutine gameplaySettingsButtonOpenLockCoroutine = null;
    private int gameplaySettingsButtonExternalLockCount = 0;
    private bool warnedMissingGameplaySettingsButton;

    public void SetGameplaySettingsButtonExternalLock(bool locked)
    {
        if (locked)
        {
            gameplaySettingsButtonExternalLockCount++;
        }
        else
        {
            gameplaySettingsButtonExternalLockCount = Mathf.Max(0, gameplaySettingsButtonExternalLockCount - 1);
        }

        UpdateGameplaySettingsButtonInteractivity();
    }

    public void SetPausePopupOpen(bool open)
    {
        pausePopupOpen = open;

        if (open)
        {
            suppressHumanInput = true;

            UpdateGameplaySettingsButtonInteractivity();

            foreach (var p in GetAllActivePieces())
            {
                if (p == null) continue;
                p.SetClickable(false);
                p.PauseHidePiece();
            }

            StopTurnPieceHighlightsForCurrentPlayer();
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.StopTurnHighlight();
            }

            return;
        }

        if (gameOver || !modeSelected)
        {
            return;
        }

        if (IsPlayWithOopsMode)
        {
            suppressHumanInput = currentPlayer != 1;
        }
        else
        {
            suppressHumanInput = IsBotPlayer(currentPlayer);
        }

        foreach (var p in GetAllActivePieces())
        {
            if (p == null) continue;
            p.PauseShowPiece();
        }

        UpdatePiecesInteractivityForTurn();
        UpdateGameplaySettingsButtonInteractivity();
        if (cardPicked)
        {
            ApplyInteractivityForCard(currentCardValue);

            if (isSplitMode && selectedPieceForSplit != null && remainingSteps > 0)
            {
                UpdateTurnPieceHighlightsForSplitRemainder(remainingSteps, selectedPieceForSplit);
            }
            else
            {
                UpdateTurnPieceHighlights();
            }
        }
        else
        {
            UpdateTurnPieceHighlights();
        }
    }

     public void NotifyMoveStarted(PlayerPiece piece, int stepsPlanned)
     {
         moveInputLockActive = true;
         moveInputLockPlayer = piece != null ? piece.playerNumber : currentPlayer;

         // Freeze turn countdown while a pawn is animating.
         // Without this, a server/client turn update can cause the countdown to restart for the next player mid-move.
         StopTurnCountdown();

         List<PlayerPiece> lockedPieces = GetPiecesForPlayer(moveInputLockPlayer);
         if (lockedPieces != null)
         {
             for (int i = 0; i < lockedPieces.Count; i++)
             {
                 PlayerPiece p = lockedPieces[i];
                 if (p == null) continue;
                 p.SetClickable(false);
             }
         }

         moveWatchdogToken++;
         if (moveWatchdogCoroutine != null)
         {
             StopCoroutine(moveWatchdogCoroutine);
             moveWatchdogCoroutine = null;
         }

         int token = moveWatchdogToken;
         moveWatchdogCoroutine = StartCoroutine(MoveWatchdog(token, piece, stepsPlanned));
     }

     public void NotifyMoveCompleted()
     {
         moveInputLockActive = false;
         moveInputLockPlayer = 0;

         moveWatchdogToken++;
         if (moveWatchdogCoroutine != null)
         {
             StopCoroutine(moveWatchdogCoroutine);
             moveWatchdogCoroutine = null;
         }

         // If a turn switch was requested while a move was in progress, run it now.
         // This guarantees turn pass only happens after the pawn finishes its MoveToDestination animation.
         if (pendingSwitchTurn && !cardAnimationLock)
         {
             pendingSwitchTurn = false;
             SwitchTurn();
         }

         if (IsPlayWithOopsMode && oopsHasDeferredTurnChange)
         {
             oopsHasDeferredTurnChange = false;
             ResetOopsCardAndModesForNextTurn();
             currentPlayer = oopsDeferredCurrentPlayer;
             suppressHumanInput = currentPlayer != LocalPlayerNumber;
             UpdateTurnIndicatorUI();
             UpdateDeckTintForTurn();
             UpdatePiecesInteractivityForOopsTurn();
             StartCardPickReminderIfNeeded();
             StartTurnCountdownForCurrentPlayer();
         }
     }
    
     IEnumerator MoveWatchdog(int token, PlayerPiece piece, int stepsPlanned)
     {
         float timeout = 10f;
         while (timeout > 0f)
         {
             if (token != moveWatchdogToken)
             {
                 yield break;
             }

             if (gameOver || !modeSelected)
             {
                 yield break;
             }

             timeout -= Time.deltaTime;
             yield return null;
         }

         if (token != moveWatchdogToken)
         {
             yield break;
         }

         string p = piece != null ? $"P{piece.playerNumber}-#{piece.pieceNumber}" : "<null piece>";
         Debug.LogError($"🧯 MOVE WATCHDOG: Move did not complete in time. Forcing recovery. currentPlayer={currentPlayer}, cardPicked={cardPicked}, card={currentCardValue}, piece={p}, stepsPlanned={stepsPlanned}");
         ForceRecoverTurn("move watchdog timeout");
     }

     public void ForceRecoverTurn(string reason)
     {
         Debug.LogError($"🧯 ForceRecoverTurn: {reason}");

         // Stop any pending watchdog.
         NotifyMoveCompleted();
         StopOopsAutoMoveWatchdog();

         // Clear highlights (safe no-op in this project)
         StopAllTurnPieceHighlights();

         // Best-effort: unlock input + card animation lock so SwitchTurn can run.
         cardAnimationLock = false;
         pendingSwitchTurn = false;
         suppressHumanInput = false;

         // Return the card if possible.
         if (currentCardHandler != null)
         {
             currentCardHandler.ReturnCardToStart();
         }

         // Reset card + modes.
         cardPicked = false;
         currentCardValue = 0;
         currentCardPower1 = "";
         currentCardPower2 = "";
         currentCardHandler = null;

         isSplitMode = false;
         remainingSteps = 0;
         selectedPieceForSplit = null;

         isCard10Mode = false;
         isCard11Mode = false;
         selectedPieceForCard11 = null;
         isCard12Mode = false;
         selectedPieceForCard12 = null;
         isSorryMode = false;
         selectedPieceForSorry = null;

         extraTurnPending = false;

         SwitchTurn();
     }

    bool TryExecuteBotSplit7()
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null) return false;

        // Priority for bot: if any pawn can do a direct +7, do it immediately.
        if (TryPickBotMoveForSteps(7, out PlayerPiece directFirst))
        {
            directFirst.MovePieceDirectly(7);
            return true;
        }

        PlayerPiece bestA = null;
        PlayerPiece bestB = null;
        int bestAsteps = 0;
        int bestBsteps = 0;
        float bestScore = float.NegativeInfinity;

        for (int aSteps = 1; aSteps <= 6; aSteps++)
        {
            int bSteps = 7 - aSteps;

            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece a = pieces[i];
                if (a == null) continue;
                if (!CheckIfMovePossible(a, aSteps)) continue;

                for (int j = 0; j < pieces.Count; j++)
                {
                    if (i == j) continue;
                    PlayerPiece b = pieces[j];
                    if (b == null) continue;
                    if (!CheckIfMovePossible(b, bSteps)) continue;

                    float score = ScoreMoveForPiece(a, aSteps) + ScoreMoveForPiece(b, bSteps);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestA = a;
                        bestB = b;
                        bestAsteps = aSteps;
                        bestBsteps = bSteps;
                    }
                }
            }
        }

        if (bestA == null || bestB == null)
        {
            return false;
        }

        selectedPieceForSplit = null;
        remainingSteps = 7;

        bestA.MovePieceDirectly(bestAsteps);

        StartCoroutine(BotFinishSplitAfterFirstMove(bestA, bestB, bestBsteps));
        return true;
    }

    IEnumerator BotFinishSplitAfterFirstMove(PlayerPiece first, PlayerPiece second, int secondSteps)
    {
        float timeout = 8f;
        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;

            if (gameOver || !modeSelected)
            {
                yield break;
            }

            if (!isSplitMode)
            {
                yield break;
            }

            if (!IsBotPlayer(currentPlayer))
            {
                yield break;
            }

            if (selectedPieceForSplit == first && remainingSteps > 0)
            {
                break;
            }

            yield return null;
        }

        if (gameOver || !modeSelected) yield break;
        if (!isSplitMode) yield break;
        if (!IsBotPlayer(currentPlayer)) yield break;

        if (second == null) yield break;
        if (!CheckIfMovePossible(second, secondSteps))
        {
            for (int i = 0; i < 6; i++)
            {
                List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
                if (pieces == null) yield break;
                PlayerPiece alt = null;
                for (int p = 0; p < pieces.Count; p++)
                {
                    if (pieces[p] == null) continue;
                    if (pieces[p] == selectedPieceForSplit) continue;
                    if (CheckIfMovePossible(pieces[p], remainingSteps))
                    {
                        alt = pieces[p];
                        break;
                    }
                }
                if (alt != null)
                {
                    alt.MovePieceDirectly(remainingSteps);
                    yield break;
                }
                yield return null;
            }

            yield break;
        }

        second.MovePieceDirectly(secondSteps);
    }

    [SerializeField] private BotDifficulty botDifficulty = BotDifficulty.Normal;
    [SerializeField, Range(0f, 1f)] private float botHumanRandomness = 0.35f;
    private Coroutine botTurnCoroutine;
    private bool botTurnInProgress = false;

    private Coroutine delayedTurnHighlightCoroutine;

    private bool cardAnimationLock = false;
    private bool pendingSwitchTurn = false;

    private bool extraTurnPending = false;

     private Coroutine moveWatchdogCoroutine;
     private int moveWatchdogToken = 0;

    private Coroutine oopsAutoMoveWatchdogCoroutine;
    private int oopsAutoMoveWatchdogToken = 0;

    private bool suppressHumanInput = false;

    private float lastDeckReadyNotifyTime = -999f;

    [SerializeField] private float cardPickReminderAfterDeckReadyDelay = 0.35f;
    private Coroutine deckReadyReminderCoroutine = null;

    [Tooltip("Total active players (2 ya 4)")]
    [Range(2, 4)]
    [HideInInspector]
    [SerializeField] private int playerCount = 2;

    private bool gameOver = false;
    private int winningPlayer = 0;

    private bool gameplayInitialized = false;

    private bool modeSelected = false;

    private bool hasAppliedSocketGameStartData = false;

    [Header("Final Home Slot Layout")]
    [SerializeField] private float finalHomeSlotOffset = 25f;
    [HideInInspector] [SerializeField] private Transform[] player1FinalHomeSpots = new Transform[3];
    [HideInInspector] [SerializeField] private Transform[] player2FinalHomeSpots = new Transform[3];
    [HideInInspector] [SerializeField] private Transform[] player3FinalHomeSpots = new Transform[3];
    [HideInInspector] [SerializeField] private Transform[] player4FinalHomeSpots = new Transform[3];

    [Tooltip("Card currently picked che ke nahi")]
    private bool cardPicked = false;

    [Tooltip("Current card value (e.g., +3 = 3)")]
    private int currentCardValue = 0;

    [Tooltip("Current card power1 text (e.g., 'Start pawn', 'Move +5')")]
    private string currentCardPower1 = "";

    [Tooltip("Current card power2 text (e.g., '+1 move', 'Extra turn')")]
    private string currentCardPower2 = "";

    [Header("Turn Indicator UI")]
    [HideInInspector] [SerializeField] private GameObject player1TurnImage;
    [HideInInspector] [SerializeField] private GameObject player2TurnImage;
    [HideInInspector] [SerializeField] private GameObject player3TurnImage;
    [HideInInspector] [SerializeField] private GameObject player4TurnImage;
    private TMP_Text player1TurnTimerText;
    private TMP_Text player2TurnTimerText;
    private TMP_Text player3TurnTimerText;
    private TMP_Text player4TurnTimerText;
    [SerializeField] private float turnPulseScale = 1.15f;
    [SerializeField] private float turnPulseDuration = 0.35f;
    private Coroutine turnPulseCoroutine;
    [SerializeField] private bool enableTurnPulseAnimation = false;

    [SerializeField] private float turnCountdownSeconds = 30f;
    [SerializeField] private float turnCountdownExtraSeconds = 20f;
    [SerializeField] private float turnCountdownRedThresholdSeconds = 5f;
    [SerializeField] private Color turnCountdownRedColor = Color.red;
    [SerializeField] private bool enableTurnTimerForBots = true;
    [SerializeField] private bool delayTurnUiUntilDeckReady = true;
    [SerializeField] private bool enableTurnTimerInPlayWithOops = true;
    [SerializeField] private bool oopsUseFastTurnTimers = false;
    [SerializeField] private float oopsTurnCountdownSeconds = 6f;
    [SerializeField] private float oopsTurnCountdownExtraSeconds = 4f;
    private Coroutine turnCountdownCoroutine;
    private int turnCountdownPlayer;
    private bool turnCountdownExtraGranted;
    private readonly Dictionary<Image, Color> turnCountdownBaseColorByImage = new Dictionary<Image, Color>();
    private bool deckReadyForTurnCountdown;

    [Header("Power Button UI")]
    [SerializeField] private Button powerButton;

    [Header("Card Sprite Variant Mapping")]
    [SerializeField, Range(1, 4)] private int player1CardSpriteVariant = 1;
    [SerializeField, Range(1, 4)] private int player2CardSpriteVariant = 2;
    [SerializeField, Range(1, 4)] private int player3CardSpriteVariant = 3;
    [SerializeField, Range(1, 4)] private int player4CardSpriteVariant = 4;

    [Header("Split Mode (Card 7)")]
    [Tooltip("Split mode active che ke nahi (Card 7 mate)")]
    private bool isSplitMode = false;

    [Tooltip("Remaining steps after first piece move (Card 7 split)")]
    private int remainingSteps = 0;

    [Tooltip("First piece jo move thayu (Card 7 split)")]
    private PlayerPiece selectedPieceForSplit = null;

    public Transform GetFinalHomeSpot(int playerNumber, int spotIndex)
    {
        Transform[] spots = GetFinalHomeSpotsForPlayer(playerNumber);
        if (spots == null || spots.Length == 0)
        {
            return null;
        }

        if (spotIndex < 0) spotIndex = 0;
        if (spotIndex >= spots.Length) spotIndex = spots.Length - 1;

        return spots[spotIndex];
    }

     bool IsAnyOtherPieceCanUseSplitRemainder(int steps, PlayerPiece excludePiece)
     {
         if (steps <= 0)
         {
             return false;
         }

         List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
         if (currentPieces == null) return false;

         foreach (var p in currentPieces)
         {
             if (p == null) continue;
             if (p == excludePiece) continue;
             if (CheckIfMovePossible(p, steps)) return true;
         }

         return false;
     }

     void ApplyInteractivityForSplitRemainder(int steps, PlayerPiece excludePiece)
     {
         List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
         if (currentPieces == null) return;

         foreach (var p in currentPieces)
         {
             if (p == null) continue;
             if (p == excludePiece)
             {
                 p.SetClickable(false);
                 continue;
             }

             p.SetClickable(CheckIfMovePossible(p, steps));
         }
     }

     void UpdateTurnPieceHighlightsForSplitRemainder(int steps, PlayerPiece excludePiece)
     {
         if (pausePopupOpen) return;
         if (cardAnimationLock) return;
         if (!cardPicked) return;

         List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
         if (currentPieces == null) return;

         for (int i = 0; i < currentPieces.Count; i++)
         {
             PlayerPiece p = currentPieces[i];
             if (p == null) continue;

             if (p == excludePiece)
             {
                 p.StopTurnHighlight();
                 continue;
             }

             if (CheckIfMovePossible(p, steps))
             {
                 p.StartTurnHighlight();
             }
             else
             {
                 p.StopTurnHighlight();
             }
         }

         foreach (var opp in GetOpponentPieces(currentPlayer))
         {
             if (opp == null) continue;
             opp.StopTurnHighlight();
         }
     }

    [Header("Card 10 Dual Power Mode")]
    [Tooltip("Card 10 dual power mode active che ke nahi (Move +10 OR -1 backward)")]
    private bool isCard10Mode = false;

    [Header("Card 11 Dual Power Mode")]
    [Tooltip("Card 11 dual power mode active che ke nahi (Move +11 OR Swap)")]
    private bool isCard11Mode = false;

    private PlayerPiece selectedPieceForCard11 = null;

    [Header("Card 12 Capture Mode")]
    [Tooltip("Card 12 capture mode active che ke nahi (Capture opponent piece and take its place)")]
    private bool isCard12Mode = false;

    private PlayerPiece selectedPieceForCard12 = null;

    [Header("SORRY! Attack Mode")]
    [Tooltip("SORRY!/Attack card mode active che ke nahi (send opponent piece home)")]
    private bool isSorryMode = false;

    private PlayerPiece selectedPieceForSorry = null;

    [Header("Player Pieces")]
    [SerializeField] private bool autoSpawnPiecesIfMissing = true;
    [SerializeField] private bool debugAutoSpawnPieces = false;
    [SerializeField] private PlayerPiece defaultPiecePrefab = null;

    [Header("Cosmetic Piece Prefabs")]
    [SerializeField] private List<PlayerPiece> cosmeticPiecePrefabs = new List<PlayerPiece>();

    [SerializeField] private Sprite defaultLocalPlayerPieceSprite = null;
    [SerializeField] private Sprite defaultOpponentPieceSprite = null;
    [SerializeField] private Sprite defaultPlayer3PieceSprite = null;
    [SerializeField] private Sprite defaultPlayer4PieceSprite = null;

    private const string CosmeticPurchasedKeyPrefix = "COSMETIC_PURCHASED_";
    private const string CosmeticSelectedKey = "COSMETIC_SELECTED";

    [Tooltip("Player 1 na pieces (3 pieces)")]
    [HideInInspector] public List<PlayerPiece> player1Pieces = new List<PlayerPiece>();

    [Tooltip("Player 2 na pieces (3 pieces)")]
    [HideInInspector] public List<PlayerPiece> player2Pieces = new List<PlayerPiece>();

    [Tooltip("Player 3 na pieces (3 pieces)")]
    [HideInInspector] public List<PlayerPiece> player3Pieces = new List<PlayerPiece>();

    [Tooltip("Player 4 na pieces (3 pieces)")]
    [HideInInspector] public List<PlayerPiece> player4Pieces = new List<PlayerPiece>();

    [Header("Home Slots (Assign In Inspector)")]
    [HideInInspector] public Transform[] player1HomeSlots = new Transform[3];
    [HideInInspector] public Transform[] player2HomeSlots = new Transform[3];
    [HideInInspector] public Transform[] player3HomeSlots = new Transform[3];
    [HideInInspector] public Transform[] player4HomeSlots = new Transform[3];

    int GetActivePlayerCount()
    {
        if (pathManager != null)
        {
            return pathManager.GetPlayerCount();
        }

        return Mathf.Clamp(playerCount, 2, 4);
    }

    float GetPieceScaleForCurrentMode()
    {
        return GetActivePlayerCount() >= 4 ? 0.7f : 1f;
    }

    public int GetActivePlayerCountPublic()
    {
        return GetActivePlayerCount();
    }

    List<PlayerPiece> GetPiecesForPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1Pieces;
        if (playerNumber == 2) return player2Pieces;
        if (playerNumber == 3) return player3Pieces;
        if (playerNumber == 4) return player4Pieces;
        return null;
    }

    void SetPiecesForPlayer(int playerNumber, List<PlayerPiece> pieces)
    {
        if (playerNumber == 1) player1Pieces = pieces;
        else if (playerNumber == 2) player2Pieces = pieces;
        else if (playerNumber == 3) player3Pieces = pieces;
        else if (playerNumber == 4) player4Pieces = pieces;
    }

    void EnsurePiecesSpawnedIfMissing()
    {
        if (!autoSpawnPiecesIfMissing) return;
        if (defaultPiecePrefab == null && (cosmeticPiecePrefabs == null || cosmeticPiecePrefabs.Count == 0)) return;

        int count = GetActivePlayerCount();
        if (debugAutoSpawnPieces)
        {
            Debug.Log($"GameManager: EnsurePiecesSpawnedIfMissing activePlayers={count}, boardRoot={(activeBoardRoot != null ? activeBoardRoot.name : "<null>")}");
        }
        for (int p = 1; p <= count; p++)
        {
            List<PlayerPiece> pieces = GetPiecesForPlayer(p);
            if (HasAnyValidPiece(pieces))
            {
                ApplyPieceScaleToPieces(pieces);
                ApplyPieceSortingOrdersToPieces(pieces, p);
                if (debugAutoSpawnPieces)
                {
                    Debug.Log($"GameManager: Player {p} already has scene pieces (count={(pieces != null ? pieces.Count : 0)}), skipping default spawn.");
                }
                continue;
            }

            if (debugAutoSpawnPieces)
            {
                Transform[] slots = GetHomeSlotsForPlayer(p);
                string s0 = slots != null && slots.Length > 0 && slots[0] != null ? slots[0].name : "<null>";
                string s1 = slots != null && slots.Length > 1 && slots[1] != null ? slots[1].name : "<null>";
                string s2 = slots != null && slots.Length > 2 && slots[2] != null ? slots[2].name : "<null>";
                Debug.Log($"GameManager: Spawning default pieces for player {p}. slots=[{s0},{s1},{s2}]");
            }
            SpawnDefaultPiecesForPlayer(p);
        }
    }

    void ApplyPieceSortingOrdersToPieces(List<PlayerPiece> pieces, int playerNumber)
    {
        if (pieces == null || pieces.Count == 0) return;

        // Default pieces appear at Order in Layer=2 in the prefab. If all are the same, they overlap.
        // So we assign a stable per-player stack range.
        int baseOrder = 2;
        int playerOffset = Mathf.Max(0, playerNumber - 1) * 10;

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece piece = pieces[i];
            if (!IsScenePiece(piece)) continue;

            int order = baseOrder + playerOffset + i;

            Canvas c = piece.GetComponent<Canvas>();
            if (c != null)
            {
                c.overrideSorting = true;
                c.sortingOrder = order;
                continue;
            }

            SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = order;
            }
        }
    }

    int GetSelectedCosmeticIndexOrDefault()
    {
        return PlayerPrefs.GetInt(CosmeticSelectedKey, -1);
    }

    int GetLocalPlayerNumberOrDefault()
    {
        int p = LocalPlayerNumber;
        return (p >= 1 && p <= 4) ? p : 1;
    }

    bool IsCosmeticPurchased(int index)
    {
        return PlayerPrefs.GetInt(CosmeticPurchasedKeyPrefix + index, 0) == 1;
    }

    PlayerPiece GetLocalPlayerPiecePrefabForSelectedCosmetic()
    {
        if (cosmeticPiecePrefabs == null || cosmeticPiecePrefabs.Count == 0) return null;

        int index = GetSelectedCosmeticIndexOrDefault();
        if (index < 0 || index >= cosmeticPiecePrefabs.Count) return null;
        if (!IsCosmeticPurchased(index)) return null;

        return cosmeticPiecePrefabs[index];
    }

    void ApplyPieceScaleToPieces(List<PlayerPiece> pieces)
    {
        if (pieces == null || pieces.Count == 0) return;

        float s = GetPieceScaleForCurrentMode();
        Vector3 scale = Vector3.one * s;

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece piece = pieces[i];
            if (!IsScenePiece(piece)) continue;

            piece.transform.localScale = scale;
        }
    }

    void ApplyPieceScaleToExistingPieces()
    {
        ApplyPieceScaleToPieces(player1Pieces);
        ApplyPieceScaleToPieces(player2Pieces);
        ApplyPieceScaleToPieces(player3Pieces);
        ApplyPieceScaleToPieces(player4Pieces);
    }

    bool HasAnyValidPiece(List<PlayerPiece> pieces)
    {
        if (pieces == null || pieces.Count == 0) return false;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (IsScenePiece(pieces[i])) return true;
        }
        return false;
    }

    bool IsScenePiece(PlayerPiece piece)
    {
        if (piece == null) return false;

        GameObject go = piece.gameObject;
        if (go == null) return false;

        // Prefab assets can be referenced in the inspector but are not scene instances.
        // We only want to treat spawned/scene pieces as valid.
        return go.scene.IsValid();
    }

    void DestroyAllBoardPieces()
    {
        DestroyPiecesUnderRoot(activeBoardRoot);
        if (setup2P != null && setup2P.boardRoot != null) DestroyPiecesUnderRoot(setup2P.boardRoot);
        if (setup4P != null && setup4P.boardRoot != null) DestroyPiecesUnderRoot(setup4P.boardRoot);

        if (player1Pieces != null) player1Pieces.Clear();
        if (player2Pieces != null) player2Pieces.Clear();
        if (player3Pieces != null) player3Pieces.Clear();
        if (player4Pieces != null) player4Pieces.Clear();
    }

    void DestroyPiecesUnderRoot(GameObject boardRoot)
    {
        if (boardRoot == null) return;

        PlayerPiece[] pieces = boardRoot.GetComponentsInChildren<PlayerPiece>(true);
        for (int i = 0; i < pieces.Length; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            if (!IsScenePiece(p)) continue;

            p.transform.DOKill();
            Destroy(p.gameObject);
        }
    }

    bool IsDefaultPieceSprite(Sprite sprite)
    {
        if (sprite == null) return false;
        if (sprite == defaultLocalPlayerPieceSprite) return true;
        if (sprite == defaultOpponentPieceSprite) return true;
        if (sprite == defaultPlayer3PieceSprite) return true;
        if (sprite == defaultPlayer4PieceSprite) return true;
        return false;
    }

    Sprite GetDefaultPieceSpriteForPlayer(int playerNumber)
    {
        int count = GetActivePlayerCount();

        if (count == 2)
        {
            return (playerNumber == 1) ? defaultLocalPlayerPieceSprite : defaultOpponentPieceSprite;
        }

        if (playerNumber == LocalPlayerNumber)
        {
            return defaultLocalPlayerPieceSprite;
        }

        if (playerNumber == 3 && defaultPlayer3PieceSprite != null)
        {
            return defaultPlayer3PieceSprite;
        }
        if (playerNumber == 4 && defaultPlayer4PieceSprite != null)
        {
            return defaultPlayer4PieceSprite;
        }

        return defaultOpponentPieceSprite;
    }

    public Sprite GetDefaultPieceSpriteForPlayerPublic(int playerNumber)
    {
        return GetDefaultPieceSpriteForPlayer(playerNumber);
    }

    void ApplyDefaultPieceSpritesToExistingPieces()
    {
        int count = GetActivePlayerCount();

        // Some UI screens (e.g. Lobby/PlayerFinding) can show PlayerPiece objects that are not
        // referenced by ModeSetup lists. So we apply to all PlayerPiece instances in the scene.
        PlayerPiece[] allPieces = FindObjectsOfType<PlayerPiece>(includeInactive: true);
        for (int i = 0; i < allPieces.Length; i++)
        {
            PlayerPiece piece = allPieces[i];
            if (!IsScenePiece(piece)) continue;

            int p = piece.playerNumber;
            if (p < 1 || p > count) continue;

            if (count >= 4)
            {
                piece.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            }
            else
            {
                piece.transform.localScale = Vector3.one;
            }

            Sprite desired = GetDefaultPieceSpriteForPlayer(p);
            if (desired == null) continue;

            bool shouldApply = true;
            Image img = piece.GetComponent<Image>();
            if (img != null)
            {
                Sprite current = img.sprite;
                if (current != null && !IsDefaultPieceSprite(current))
                {
                    shouldApply = false;
                }
            }
            else
            {
                SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Sprite current = sr.sprite;
                    if (current != null && !IsDefaultPieceSprite(current))
                    {
                        shouldApply = false;
                    }
                }
            }

            if (shouldApply)
            {
                // Don't rely on PlayerPiece cached references here because this can be called
                // while the piece GameObject is inactive (Awake not executed yet).
                if (img != null)
                {
                    img.sprite = desired;
                    img.enabled = true;
                }
                else
                {
                    SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sprite = desired;
                        sr.enabled = true;
                    }
                    else
                    {
                        piece.SetPieceSprite(desired);
                    }
                }
            }
        }
    }

    public void ApplyDefaultPieceSpritesNow()
    {
        ApplyDefaultPieceSpritesToExistingPieces();
    }

    void SpawnDefaultPiecesForPlayer(int playerNumber)
    {
        Transform[] slots = GetHomeSlotsForPlayer(playerNumber);
        if (slots == null || slots.Length == 0)
        {
            if (debugAutoSpawnPieces)
            {
                Debug.LogWarning($"GameManager: Cannot spawn default pieces for player {playerNumber} because home slots are missing.");
            }
            return;
        }

        List<PlayerPiece> spawned = GetPiecesForPlayer(playerNumber) ?? new List<PlayerPiece>(slots.Length);
        while (spawned.Count < slots.Length)
        {
            spawned.Add(null);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];
            if (slot == null)
            {
                if (debugAutoSpawnPieces)
                {
                    Debug.LogWarning($"GameManager: Player {playerNumber} slot index {i} is null, skipping.");
                }
                continue;
            }

            PlayerPiece instance = spawned[i];
            if (!IsScenePiece(instance))
            {
                PlayerPiece prefabToSpawn = defaultPiecePrefab;
                if (playerNumber == GetLocalPlayerNumberOrDefault())
                {
                    PlayerPiece cosmeticPrefab = GetLocalPlayerPiecePrefabForSelectedCosmetic();
                    if (cosmeticPrefab != null)
                    {
                        prefabToSpawn = cosmeticPrefab;
                    }
                }

                if (prefabToSpawn == null)
                {
                    if (debugAutoSpawnPieces)
                    {
                        Debug.LogWarning($"GameManager: Cannot spawn piece for player {playerNumber} because prefab is null (defaultPiecePrefab not assigned and no valid cosmetic prefab selected).");
                    }
                    continue;
                }

                instance = Instantiate(prefabToSpawn, slot);
                spawned[i] = instance;

                if (debugAutoSpawnPieces)
                {
                    Debug.Log($"GameManager: Spawned piece P{playerNumber}-#{i + 1} as '{instance.name}' under '{slot.name}'.");
                }
            }

            instance.name = $"Player{playerNumber}_Piece{i + 1}";

            instance.playerNumber = playerNumber;
            instance.pieceNumber = i + 1;
            instance.pathManager = pathManager;
            instance.gameManager = this;
            instance.SetHomeTransform(slot);

            if (GetActivePlayerCount() >= 4)
            {
                instance.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            }
            else
            {
                instance.transform.localScale = Vector3.one;
            }

            Sprite sprite = GetDefaultPieceSpriteForPlayer(playerNumber);

            if (debugAutoSpawnPieces)
            {
                if (defaultLocalPlayerPieceSprite == null)
                {
                    Debug.LogWarning("GameManager: defaultLocalPlayerPieceSprite is not assigned.");
                }
                if (defaultOpponentPieceSprite == null)
                {
                    Debug.LogWarning("GameManager: defaultOpponentPieceSprite is not assigned.");
                }
                if (defaultLocalPlayerPieceSprite != null && defaultOpponentPieceSprite != null && defaultLocalPlayerPieceSprite == defaultOpponentPieceSprite)
                {
                    Debug.LogWarning("GameManager: Local and Opponent piece sprites are the same asset. Opponent will look identical.");
                }

                Debug.Log($"GameManager: Applying sprite to {instance.name} for player {playerNumber}. LocalPlayerNumber={LocalPlayerNumber}, sprite={(sprite != null ? sprite.name : "<null>")}");
            }

            bool shouldApplySprite = true;
            if (playerNumber == GetLocalPlayerNumberOrDefault())
            {
                PlayerPiece cosmeticPrefab = GetLocalPlayerPiecePrefabForSelectedCosmetic();
                if (cosmeticPrefab != null)
                {
                    Image img = instance.GetComponent<Image>();
                    if (img != null)
                    {
                        shouldApplySprite = img.sprite == null;
                    }
                    else
                    {
                        SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            shouldApplySprite = sr.sprite == null;
                        }
                    }
                }
            }

            if (shouldApplySprite)
            {
                instance.SetPieceSprite(sprite);
            }
            instance.ReturnToHome();
            instance.SetClickable(false);
        }

        ApplyPieceSortingOrdersToPieces(spawned, playerNumber);

        SetPiecesForPlayer(playerNumber, spawned);
    }

    Transform[] GetHomeSlotsForPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1HomeSlots;
        if (playerNumber == 2) return player2HomeSlots;
        if (playerNumber == 3) return player3HomeSlots;
        if (playerNumber == 4) return player4HomeSlots;
        return null;
    }

    Transform[] GetFinalHomeSpotsForPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1FinalHomeSpots;
        if (playerNumber == 2) return player2FinalHomeSpots;
        if (playerNumber == 3) return player3FinalHomeSpots;
        if (playerNumber == 4) return player4FinalHomeSpots;
        return null;
    }

    GameObject GetTurnIndicatorForPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1TurnImage;
        if (playerNumber == 2) return player2TurnImage;
        if (playerNumber == 3) return player3TurnImage;
        if (playerNumber == 4) return player4TurnImage;
        return null;
    }

    TMP_Text GetTurnTimerTextForPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1TurnTimerText;
        if (playerNumber == 2) return player2TurnTimerText;
        if (playerNumber == 3) return player3TurnTimerText;
        if (playerNumber == 4) return player4TurnTimerText;
        return null;
    }

    private static Image FindFilledImageInTurnIndicator(GameObject indicator)
    {
        if (indicator == null) return null;
        Image[] images = indicator.GetComponentsInChildren<Image>(true);
        if (images != null)
        {
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null) continue;
                if (img.type == Image.Type.Filled)
                {
                    return img;
                }
            }
        }

        return indicator.GetComponent<Image>();
    }

    private void StopTurnCountdown()
    {
        if (turnCountdownCoroutine != null)
        {
            StopCoroutine(turnCountdownCoroutine);
            turnCountdownCoroutine = null;
        }
        turnCountdownExtraGranted = false;
        turnCountdownPlayer = 0;

        oopsAutoCardOpenSentThisTurn = false;
        oopsAutoMoveSentThisTurn = false;
        oopsAutoSplitFirstSentThisTurn = false;
        oopsAutoSplitSecondSentThisTurn = false;

        for (int p = 1; p <= 4; p++)
        {
            TMP_Text t = GetTurnTimerTextForPlayer(p);
            if (t != null)
            {
                t.text = string.Empty;
            }
        }
    }

    private void StartTurnCountdownForCurrentPlayer()
    {
        if (turnCountdownCoroutine != null && turnCountdownPlayer == currentPlayer)
        {
            return;
        }

        StopTurnCountdown();

        oopsAutoCardOpenSentThisTurn = false;
        oopsAutoMoveSentThisTurn = false;
        oopsAutoSplitFirstSentThisTurn = false;

        if (IsPlayWithOopsMode && !enableTurnTimerInPlayWithOops) return;
        if (!modeSelected || gameOver) return;
        if (IsBotPlayer(currentPlayer) && !enableTurnTimerForBots) return;

        GameObject activeObj = GetTurnIndicatorForPlayer(currentPlayer);
        if (activeObj == null || !activeObj.activeInHierarchy) return;

        Image fill = FindFilledImageInTurnIndicator(activeObj);
        if (fill == null) return;

        float effectiveMainSeconds = GetEffectiveTurnCountdownSeconds();

        TMP_Text timerText = GetTurnTimerTextForPlayer(currentPlayer);
        if (timerText != null)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(1f, effectiveMainSeconds));
            timerText.text = secs.ToString();
        }

        if (!turnCountdownBaseColorByImage.TryGetValue(fill, out Color normal))
        {
            normal = fill.color;
            turnCountdownBaseColorByImage[fill] = normal;
        }
        fill.color = normal;

        float dur = Mathf.Max(1f, effectiveMainSeconds);
        fill.fillAmount = 1f;

        turnCountdownPlayer = currentPlayer;
        turnCountdownExtraGranted = false;

        oopsAutoSplitSecondSentThisTurn = false;
        turnCountdownCoroutine = StartCoroutine(TurnCountdownRoutine(turnCountdownPlayer, fill, timerText, normal, dur));
    }

    private void TryOopsAutoCardOpenIfNeeded(float remainingSeconds, bool isExtraPhase)
    {
        if (!IsPlayWithOopsMode) return;
        if (oopsAutoCardOpenSentThisTurn) return;
        if (currentPlayer != LocalPlayerNumber) return;
        if (cardPicked) return;

        if (!isExtraPhase) return;

        float extraSeconds = GetEffectiveTurnCountdownExtraSeconds();

        // Extra countdown: auto pick right at the start of the extra timer.
        // (We intentionally do nothing during the main timer.)
        if (remainingSeconds < (extraSeconds - 0.5f)) return;

        if (pendingOopsOpenCard == null || !pendingOopsOpenCard.gameObject.activeInHierarchy)
        {
            pendingOopsOpenCard = CardClickHandler.GetLastClickableCard();
        }

        if (pendingOopsOpenCard == null) return;

        oopsAutoCardOpenSentThisTurn = true;
        RequestOopsCardOpen(pendingOopsOpenCard);
    }

    private bool TryOopsAutoRandomMoveIfNeeded(float remainingSeconds, bool isExtraPhase)
    {
        if (!IsPlayWithOopsMode) return false;
        if (oopsAutoMoveSentThisTurn) return false;
        if (currentPlayer != LocalPlayerNumber) return false;

        if (!cardPicked) return false;
        if (cardAnimationLock) return false;

        if (!isExtraPhase) return false;

        float extraSeconds = GetEffectiveTurnCountdownExtraSeconds();

        // Extra countdown: move starting ~2 seconds into the extra timer, until >1s remains.
        if (remainingSeconds > Mathf.Max(0f, extraSeconds - 2f) || remainingSeconds <= 1f) return false;

        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null || pieces.Count == 0) return false;

        // Card 7 split must be sent as chosenMoveType="SPLIT" with a splits payload.
        // Sending it as a normal FORWARD move can desync server-side validation (remaining split steps).
        if (isSplitMode)
        {
            oopsAutoMoveSentThisTurn = true;

            // If we are waiting for the second split move, try to complete it.
            if (selectedPieceForSplit != null && remainingSteps > 0)
            {
                if (oopsSplitMoveSent) return false;

                for (int i = 0; i < pieces.Count; i++)
                {
                    PlayerPiece second = pieces[i];
                    if (second == null) continue;
                    if (second == selectedPieceForSplit) continue;
                    if (!CheckIfMovePossible(second, remainingSteps)) continue;

                    bool sentSecond = TryOopsPlayCardSplitSecond(second, remainingSteps);
                    if (!sentSecond)
                    {
                        oopsAutoMoveSentThisTurn = false;
                    }
                    if (sentSecond) StartOopsAutoMoveWatchdog("auto split second");
                    return sentSecond;
                }

                ForceAdvanceOopsTurnLocally("split awaiting second: no legal second pawn");
                return false;
            }

            if (oopsSplitMoveSent) return false;

            // Priority for auto-turn: if any pawn can do a direct +7, do it immediately.
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                if (!CheckIfMovePossible(p, 7)) continue;

                bool sentSeven = TryOopsPlayCardMove(p, 7);
                if (!sentSeven)
                {
                    oopsAutoMoveSentThisTurn = false;
                }
                if (sentSeven) StartOopsAutoMoveWatchdog("auto +7 fallback");
                return sentSeven;
            }

            // No direct +7 possible -> try a real 2-pawn split plan.
            List<(PlayerPiece first, int firstSteps, PlayerPiece second)> splitPlans = null;
            for (int firstSteps = 1; firstSteps <= 6; firstSteps++)
            {
                int secondSteps = 7 - firstSteps;

                for (int i = 0; i < pieces.Count; i++)
                {
                    PlayerPiece first = pieces[i];
                    if (first == null) continue;
                    if (!CheckIfMovePossible(first, firstSteps)) continue;

                    for (int j = 0; j < pieces.Count; j++)
                    {
                        if (i == j) continue;
                        PlayerPiece second = pieces[j];
                        if (second == null) continue;
                        if (!CheckIfMovePossible(second, secondSteps)) continue;

                        if (splitPlans == null) splitPlans = new List<(PlayerPiece first, int firstSteps, PlayerPiece second)>();
                        splitPlans.Add((first, firstSteps, second));
                    }
                }
            }

            if (splitPlans != null && splitPlans.Count > 0)
            {
                var plan = splitPlans[UnityEngine.Random.Range(0, splitPlans.Count)];
                bool sentSplitFirst = TryOopsPlayCardSplitFirst(plan.first, plan.firstSteps);
                if (!sentSplitFirst)
                {
                    oopsAutoMoveSentThisTurn = false;
                }
                else
                {
                    oopsAutoSplitFirstSentThisTurn = true;
                }
                if (sentSplitFirst) StartOopsAutoMoveWatchdog("auto split first");
                return sentSplitFirst;
            }

            ForceAdvanceOopsTurnLocally("split mode: no legal split and no legal +7");
            return false;
        }

        List<(PlayerPiece piece, int steps)> options = new List<(PlayerPiece piece, int steps)>();

        List<(PlayerPiece attacker, PlayerPiece target)> swapOptions = null;

        List<(PlayerPiece attacker, PlayerPiece target)> bumpOptions = null;

        // SORRY: prefer BUMP from START (option1), else +4 forward (option2).
        if (isSorryMode)
        {
            List<PlayerPiece> oppTargets = new List<PlayerPiece>();
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.SyncCurrentPathIndexFromTransform();
                if (opp.IsAtHome()) continue;
                if (opp.IsOnHomePath()) continue;
                if (opp.IsFinishedInHomePath()) continue;
                oppTargets.Add(opp);
            }

            if (oppTargets.Count > 0)
            {
                bumpOptions = new List<(PlayerPiece attacker, PlayerPiece target)>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    PlayerPiece attacker = pieces[i];
                    if (attacker == null) continue;
                    attacker.SyncCurrentPathIndexFromTransform();
                    if (!attacker.IsAtHome()) continue;

                    for (int j = 0; j < oppTargets.Count; j++)
                    {
                        PlayerPiece target = oppTargets[j];
                        if (target == null) continue;
                        bumpOptions.Add((attacker, target));
                    }
                }
            }

            // +4 forward fallback (only if a pawn is on board)
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                if (p.IsAtHome()) continue;
                if (CheckIfMovePossible(p, 4)) options.Add((p, 4));
            }
        }

        // Card 12: forward +12 OR capture (BUMP).
        if (isCard12Mode)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                if (CheckIfMovePossible(p, 12)) options.Add((p, 12));
            }

            List<PlayerPiece> oppTargets = new List<PlayerPiece>();
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.SyncCurrentPathIndexFromTransform();
                if (!opp.IsOnOuterTrack()) continue;
                oppTargets.Add(opp);
            }

            if (oppTargets.Count > 0)
            {
                if (bumpOptions == null) bumpOptions = new List<(PlayerPiece attacker, PlayerPiece target)>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    PlayerPiece attacker = pieces[i];
                    if (attacker == null) continue;
                    attacker.SyncCurrentPathIndexFromTransform();
                    if (attacker.IsAtHome()) continue;
                    if (attacker.IsOnHomePath()) continue;
                    if (attacker.IsFinishedInHomePath()) continue;

                    for (int j = 0; j < oppTargets.Count; j++)
                    {
                        PlayerPiece target = oppTargets[j];
                        if (target == null) continue;
                        bumpOptions.Add((attacker, target));
                    }
                }
            }
        }

        if (isCard10Mode)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                if (CheckIfMovePossible(p, 10)) options.Add((p, 10));
                if (!p.IsAtHome() && CheckIfMovePossible(p, -1)) options.Add((p, -1));
            }
        }
        else if (isCard11Mode)
        {
            // Card 11 dual power: +11 OR SWAP.
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                if (CheckIfMovePossible(p, 11)) options.Add((p, 11));
            }

            // Swap options (outer-track only)
            List<PlayerPiece> oppPieces = new List<PlayerPiece>();
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.SyncCurrentPathIndexFromTransform();
                if (opp.IsOnOuterTrack()) oppPieces.Add(opp);
            }

            if (oppPieces.Count > 0)
            {
                swapOptions = new List<(PlayerPiece attacker, PlayerPiece target)>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    PlayerPiece attacker = pieces[i];
                    if (attacker == null) continue;
                    attacker.SyncCurrentPathIndexFromTransform();
                    if (!attacker.IsOnOuterTrack()) continue;

                    for (int j = 0; j < oppPieces.Count; j++)
                    {
                        PlayerPiece target = oppPieces[j];
                        if (target == null) continue;
                        swapOptions.Add((attacker, target));
                    }
                }
            }
        }
        else
        {
            // Normal forward/backward cards (including card 7 when split mode is not enabled).
            int steps = currentCardValue;
            if (steps != 0)
            {
                for (int i = 0; i < pieces.Count; i++)
                {
                    PlayerPiece p = pieces[i];
                    if (p == null) continue;
                    if (steps < 0 && p.IsAtHome()) continue;
                    if (CheckIfMovePossible(p, steps)) options.Add((p, steps));
                }
            }
        }

        int forwardCount = options.Count;
        int swapCount = swapOptions != null ? swapOptions.Count : 0;

        int bumpCount = bumpOptions != null ? bumpOptions.Count : 0;

        if (forwardCount == 0 && swapCount == 0 && bumpCount == 0)
        {
            oopsAutoMoveSentThisTurn = true;
            ForceAdvanceOopsTurnLocally("no legal auto action for picked card");
            return false;
        }

        // Randomly pick among all available actions.
        int total = forwardCount + swapCount + bumpCount;
        int pick = UnityEngine.Random.Range(0, total);

        oopsAutoMoveSentThisTurn = true;

        bool sent;
        if (pick < forwardCount)
        {
            (PlayerPiece piece, int steps) chosen = options[pick];
            sent = TryOopsPlayCardMove(chosen.piece, chosen.steps);
        }
        else if (pick < forwardCount + swapCount)
        {
            (PlayerPiece attacker, PlayerPiece target) chosenSwap = swapOptions[pick - forwardCount];
            sent = TryOopsPlayCardSwap(chosenSwap.attacker, chosenSwap.target);
        }
        else
        {
            (PlayerPiece attacker, PlayerPiece target) chosenBump = bumpOptions[pick - forwardCount - swapCount];
            sent = TryOopsPlayCardBump(chosenBump.attacker, chosenBump.target);
        }

        if (!sent)
        {
            oopsAutoMoveSentThisTurn = false;
        }
        else
        {
            StartOopsAutoMoveWatchdog("auto move");
        }
        return sent;
    }

    private IEnumerator TurnCountdownRoutine(int playerNumber, Image fill, TMP_Text timerText, Color normalColor, float duration)
    {
        if (fill == null) yield break;
        float dur = Mathf.Max(1f, duration);
        float remaining = dur;
        while (true)
        {
            while (remaining > 0f)
            {
                if (!modeSelected || gameOver) yield break;
                if (IsPlayWithOopsMode && !enableTurnTimerInPlayWithOops) yield break;
                if (currentPlayer != playerNumber) yield break;
                if (IsBotPlayer(currentPlayer) && !enableTurnTimerForBots) yield break;

                if (moveInputLockActive && moveInputLockPlayer == playerNumber)
                {
                    yield return null;
                    continue;
                }

                remaining -= Time.deltaTime;
                float t = Mathf.Clamp01(remaining / dur);
                fill.fillAmount = t;
                fill.color = remaining <= Mathf.Max(0f, turnCountdownRedThresholdSeconds) ? turnCountdownRedColor : normalColor;

                // PlayWithOops auto actions:
                // - Main phase: fire early (25s open, 22s move) for fast testing.
                // - Extra phase: keep previous windows as fallback.
                TryOopsAutoCardOpenIfNeeded(remaining, turnCountdownExtraGranted);
                TryOopsAutoRandomMoveIfNeeded(remaining, turnCountdownExtraGranted);

                if (timerText != null)
                {
                    int secs = Mathf.Clamp(Mathf.CeilToInt(remaining), 0, 999);
                    timerText.text = secs.ToString();
                }
                yield return null;
            }

            if (!turnCountdownExtraGranted)
            {
                turnCountdownExtraGranted = true;
                dur = Mathf.Max(1f, GetEffectiveTurnCountdownExtraSeconds());
                remaining = dur;
                fill.fillAmount = 1f;
                fill.color = normalColor;

                if (timerText != null)
                {
                    int secs = Mathf.CeilToInt(remaining);
                    timerText.text = secs.ToString();
                }
                continue;
            }

            if (IsPlayWithOopsMode)
            {
                fill.fillAmount = 0f;
                if (timerText != null)
                {
                    timerText.text = "0";
                }
                yield break;
            }

            StartCoroutine(SkipTurnAfterDelay());
            yield break;
        }
    }

    IEnumerable<PlayerPiece> GetAllActivePieces()
    {
        int count = GetActivePlayerCount();
        for (int p = 1; p <= count; p++)
        {
            List<PlayerPiece> list = GetPiecesForPlayer(p);
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                PlayerPiece piece = list[i];
                if (piece == null) continue;
                yield return piece;
            }
        }
    }

    void UpdateTurnPieceHighlights()
    {
        if (pausePopupOpen) return;
        if (cardAnimationLock) return;
        if (!cardPicked)
        {
            StopAllTurnPieceHighlights();
            return;
        }
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
        if (currentPieces != null)
        {
            for (int i = 0; i < currentPieces.Count; i++)
            {
                PlayerPiece p = currentPieces[i];
                if (p == null) continue;
                p.StartTurnHighlight();
            }
        }

        foreach (var opp in GetOpponentPieces(currentPlayer))
        {
            if (opp == null) continue;
            opp.StopTurnHighlight();
        }
    }

    public void StopAllTurnPieceHighlights()
    {
        foreach (var p in GetAllActivePieces())
        {
            if (p == null) continue;
            p.StopTurnHighlight();
        }
    }

    public void StopTurnPieceHighlightsForCurrentPlayer()
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
        if (currentPieces != null)
        {
            for (int i = 0; i < currentPieces.Count; i++)
            {
                PlayerPiece p = currentPieces[i];
                if (p == null) continue;
                p.StopTurnHighlight();
            }
        }
    }

    IEnumerable<PlayerPiece> GetOpponentPieces(int currentPlayerNumber)
    {
        int count = GetActivePlayerCount();
        for (int p = 1; p <= count; p++)
        {
            if (p == currentPlayerNumber) continue;
            List<PlayerPiece> list = GetPiecesForPlayer(p);
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                PlayerPiece piece = list[i];
                if (piece == null) continue;
                yield return piece;
            }
        }
    }

    public IEnumerable<PlayerPiece> GetOpponentPiecesForPlayer(int currentPlayerNumber)
    {
        return GetOpponentPieces(currentPlayerNumber);
    }

    public IEnumerable<PlayerPiece> GetAllActivePiecesForGameplay()
    {
        return GetAllActivePieces();
    }

    [Header("References")]
    [Tooltip("CardClickHandler reference (auto find karse)")]
    private CardClickHandler currentCardHandler = null;

    [Tooltip("PlayerPathManager reference (auto find karse)")]
    private PlayerPathManager pathManager = null;

    [System.Serializable]
    public class ModeSetup
    {
        [Range(2, 4)]
        public int playerCount = 2;

        public GameObject boardRoot;
        public PlayerPathManager pathManager;

        public GameObject player1TurnImage;
        public GameObject player2TurnImage;
        public GameObject player3TurnImage;
        public GameObject player4TurnImage;

        public TMP_Text player1TurnTimerText;
        public TMP_Text player2TurnTimerText;
        public TMP_Text player3TurnTimerText;
        public TMP_Text player4TurnTimerText;

        public List<PlayerPiece> player1Pieces = new List<PlayerPiece>();
        public List<PlayerPiece> player2Pieces = new List<PlayerPiece>();
        public List<PlayerPiece> player3Pieces = new List<PlayerPiece>();
        public List<PlayerPiece> player4Pieces = new List<PlayerPiece>();

        public Transform[] player1HomeSlots = new Transform[3];
        public Transform[] player2HomeSlots = new Transform[3];
        public Transform[] player3HomeSlots = new Transform[3];
        public Transform[] player4HomeSlots = new Transform[3];

        public Transform[] player1FinalHomeSpots = new Transform[3];
        public Transform[] player2FinalHomeSpots = new Transform[3];
        public Transform[] player3FinalHomeSpots = new Transform[3];
        public Transform[] player4FinalHomeSpots = new Transform[3];
    }

    [Header("2P Setup")]
    [SerializeField] private ModeSetup setup2P = new ModeSetup();

    [Header("4P Setup")]
    [SerializeField] private ModeSetup setup4P = new ModeSetup();

    public void Select2Player()
    {
        ApplyModeSetup(setup2P, setup4P);
        ApplyBotSetupForCurrentSelection();
    }

    public void Select4Player()
    {
        ApplyModeSetup(setup4P, setup2P);
        ApplyBotSetupForCurrentSelection();
    }

    public void SelectModeVsBot()
    {
        if (NoInternetStrip.BlockIfOffline()) return;

        vsBotMode = true;
        localOfflineFriendsMode = false;
        offlineExpertMode = false;
        GameWalletApi.FetchAndApplyWallet(OpenLobbyPanel);
        ApplyBotSetupForCurrentSelection();
        UpdateDeckTintForTurn();
        StartCardPickReminderIfNeeded();
    }

    public void SelectModeFriends()
    {
        vsBotMode = false;
        localOfflineFriendsMode = true;
        offlineExpertMode = false;

        if (useStaticLobbiesForFriendsMode)
        {
            OpenLobbyPanel();
        }
        else
        {
            GameWalletApi.FetchAndApplyWallet(OpenLobbyPanel);
        }

        ApplyBotSetupForCurrentSelection();

        StopCardPickReminder();
        RefreshActiveCardDeckAnimator();
        if (activeCardDeckAnimator != null)
        {
            activeCardDeckAnimator.ClearDeckTint();
        }
    }

    public void SelectModeOfflineExpert()
    {
        vsBotMode = true;
        localOfflineFriendsMode = true;
        offlineExpertMode = true;

        botDifficulty = BotDifficulty.Hard;

        // Offline expert uses static lobby configs (no API, no socket).
        OpenLobbyPanel();
        ApplyBotSetupForCurrentSelection();
        UpdateDeckTintForTurn();
        StartCardPickReminderIfNeeded();
    }

    private void OpenLobbyPanel()
    {
        ScreenManager sm = screenManager != null ? screenManager : FindObjectOfType<ScreenManager>();
        if (sm == null)
        {
            return;
        }

        if (lobbyPanel != null)
        {
            PrepareLobbyListsForCurrentMode(lobbyPanel);
            sm.OpenScreen(lobbyPanel);
            PopulateLobbyLists(lobbyPanel);
            return;
        }

        sm.OpenScreenByName("LobbyPanel");
        GameObject root = GameObject.Find("LobbyPanel");
        if (root != null)
        {
            PrepareLobbyListsForCurrentMode(root);
        }
        PopulateLobbyLists(root);
    }

    private void PrepareLobbyListsForCurrentMode(GameObject lobbyRoot)
    {
        if (lobbyRoot == null) return;

        LobbyListManager[] managers = lobbyRoot.GetComponentsInChildren<LobbyListManager>(true);
        if (managers == null || managers.Length == 0) return;

        bool forceStatic = useStaticLobbiesForFriendsMode && localOfflineFriendsMode;
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] == null) continue;
            if (forceStatic)
            {
                managers[i].SetPopulateFromGameWalletApiOverride(false);
                managers[i].SetPopulateFromStaticConfigsOverride(true);
            }
            else
            {
                managers[i].SetPopulateFromGameWalletApiOverride(true);
                managers[i].SetPopulateFromStaticConfigsOverride(false);
            }
        }
    }

    private void PopulateLobbyLists(GameObject lobbyRoot)
    {
        GameObject root = lobbyRoot;
        if (root == null)
        {
            root = GameObject.Find("LobbyPanel");
        }
        if (root == null) return;

        LobbyListManager[] managers = root.GetComponentsInChildren<LobbyListManager>(true);
        if (managers == null || managers.Length == 0) return;

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
            {
                managers[i].PopulateNow();
            }
        }
    }

    private void ApplyBotSetupForCurrentSelection()
    {
        if (!vsBotMode)
        {
            player1IsBot = false;
            player2IsBot = false;
            player3IsBot = false;
            player4IsBot = false;
            return;
        }

        int count = Mathf.Clamp(playerCount, 2, 4);
        player1IsBot = false;
        player2IsBot = true;
        player3IsBot = count >= 3;
        player4IsBot = count >= 4;
    }

    void ApplyModeSetup(ModeSetup active, ModeSetup inactive)
    {
        if (active == null)
        {
            return;
        }

        if (inactive != null && inactive.boardRoot != null) inactive.boardRoot.SetActive(false);
        if (active.boardRoot != null) active.boardRoot.SetActive(false);

        activeBoardRoot = active.boardRoot;

        playerCount = Mathf.Clamp(active.playerCount, 2, 4);

        pathManager = active.pathManager;
        if (pathManager != null)
        {
            pathManager.playerCount = playerCount;
        }

        player1TurnImage = active.player1TurnImage;
        player2TurnImage = active.player2TurnImage;
        player3TurnImage = active.player3TurnImage;
        player4TurnImage = active.player4TurnImage;

        if (player1TurnImage != null) player1TurnImage.SetActive(false);
        if (player2TurnImage != null) player2TurnImage.SetActive(false);
        if (player3TurnImage != null) player3TurnImage.SetActive(false);
        if (player4TurnImage != null) player4TurnImage.SetActive(false);

        player1TurnTimerText = active.player1TurnTimerText;
        player2TurnTimerText = active.player2TurnTimerText;
        player3TurnTimerText = active.player3TurnTimerText;
        player4TurnTimerText = active.player4TurnTimerText;

        player1Pieces = active.player1Pieces ?? new List<PlayerPiece>();
        player2Pieces = active.player2Pieces ?? new List<PlayerPiece>();
        player3Pieces = active.player3Pieces ?? new List<PlayerPiece>();
        player4Pieces = active.player4Pieces ?? new List<PlayerPiece>();

        player1HomeSlots = active.player1HomeSlots;
        player2HomeSlots = active.player2HomeSlots;
        player3HomeSlots = active.player3HomeSlots;
        player4HomeSlots = active.player4HomeSlots;

        player1FinalHomeSpots = active.player1FinalHomeSpots;
        player2FinalHomeSpots = active.player2FinalHomeSpots;
        player3FinalHomeSpots = active.player3FinalHomeSpots;
        player4FinalHomeSpots = active.player4FinalHomeSpots;

        modeSelected = true;

        if (active.boardRoot != null) active.boardRoot.SetActive(true);

        if (pathManager != null && vsBotMode && !localOfflineFriendsMode)
        {
            pathManager.SetGameplayProfilesVisible(false, resetScale: true);
        }

        RefreshActiveCardDeckAnimator();

        gameplayInitialized = false;
    }

    public void PopGameplayProfilesAfterPlayerFinding()
    {
        if (popProfilesAfterFindingRoutine != null)
        {
            StopCoroutine(popProfilesAfterFindingRoutine);
        }
        popProfilesAfterFindingRoutine = StartCoroutine(PopGameplayProfilesAfterPlayerFindingRoutine());
    }

    private Coroutine popProfilesAfterFindingRoutine;

    private IEnumerator PopGameplayProfilesAfterPlayerFindingRoutine()
    {
        yield return null;

        // Prefer the path manager that belongs to the currently active board (even if it is still activating).
        PlayerPathManager resolved = null;
        if (activeBoardRoot != null)
        {
            resolved = activeBoardRoot.GetComponentInChildren<PlayerPathManager>(true);
        }

        if (resolved == null)
        {
            resolved = pathManager;
        }

        if (resolved == null)
        {
            resolved = FindObjectOfType<PlayerPathManager>();
        }

        if (resolved == null)
        {
            PlayerPathManager[] managers = Resources.FindObjectsOfTypeAll<PlayerPathManager>();
            if (managers != null)
            {
                // Prefer one that is active in hierarchy.
                for (int i = 0; i < managers.Length; i++)
                {
                    var m = managers[i];
                    if (m == null) continue;
                    if (m.isActiveAndEnabled)
                    {
                        resolved = m;
                        break;
                    }
                }

                // Fallback: first one.
                if (resolved == null)
                {
                    for (int i = 0; i < managers.Length; i++)
                    {
                        if (managers[i] == null) continue;
                        resolved = managers[i];
                        break;
                    }
                }
            }
        }

        if (resolved != null)
        {
            pathManager = resolved;
            pathManager.SetGameplayProfilesVisible(true, resetScale: true);
            pathManager.PopGameplayProfiles();
        }

        popProfilesAfterFindingRoutine = null;
    }

    public void OnPlayerFindingScreenOpened()
    {
        StopHomeInternetAutoLoginWatcher();

        if (!modeSelected)
        {
            return;
        }

        bool deferUntilSocketStart = SocketConnection.Instance != null &&
                                   SocketConnection.Instance.CurrentState == SocketState.Connected &&
                                   !SocketConnection.Instance.HasReceivedGameStart;
        if (deferUntilSocketStart)
        {
            return;
        }

        EnsureHomeSlotsResolvedIfMissing();
        EnsurePiecesSpawnedIfMissing();
        ApplyDefaultPieceSpritesToExistingPieces();
        ApplyPieceScaleToExistingPieces();

        ApplyPieceSortingOrdersToPieces(player1Pieces, 1);
        ApplyPieceSortingOrdersToPieces(player2Pieces, 2);
        ApplyPieceSortingOrdersToPieces(player3Pieces, 3);
        ApplyPieceSortingOrdersToPieces(player4Pieces, 4);

        RefreshOopsDebugFields();

        if (IsPlayWithOopsMode)
        {
            EnsureOopsCardOpenListener();
            UpdateTurnIndicatorUI();
            UpdateDeckTintForTurn();
            UpdatePiecesInteractivityForOopsTurn();
            StartCardPickReminderIfNeeded();
        }
    }

    public void ApplySocketGameStartData(object data)
    {
        if (hasAppliedSocketGameStartData)
        {
            return;
        }

        if (!modeSelected)
        {
            return;
        }

        if (data == null)
        {
            Debug.LogWarning("GameManager.ApplySocketGameStartData: ignored (null payload)");
            return;
        }

        if (data is string s)
        {
            bool parseOk = true;
            object decoded = BestHTTP.JSON.Json.Decode(s, ref parseOk);
            if (!parseOk || decoded == null)
            {
                Debug.LogWarning($"GameManager.ApplySocketGameStartData: ignored (string decode failed): {s}");
                return;
            }
            data = decoded;
        }

        object payload = data;
        if (payload is IList list)
        {
            object best = null;
            for (int i = 0; i < list.Count; i++)
            {
                object candidate = list[i];
                IDictionary<string, object> d = AsStringObjectDict(candidate);
                if (d == null) continue;
                if (d.ContainsKey("room") || d.ContainsKey("players") || d.ContainsKey("data") || d.ContainsKey("success"))
                {
                    best = candidate;
                    break;
                }
            }

            if (best != null)
            {
                payload = best;
            }
            else if (list.Count > 0)
            {
                payload = list[0];
            }
        }

        IDictionary<string, object> root = AsStringObjectDict(payload);
        if (root == null)
        {
            Debug.LogWarning($"GameManager.ApplySocketGameStartData: ignored (payload not a dict, type={(payload != null ? payload.GetType().Name : "<null>")})");
            return;
        }

        IDictionary<string, object> wrapped = GetDict(root, "data");
        if (wrapped != null)
        {
            root = wrapped;
        }

        IDictionary<string, object> room = GetDict(root, "room") ?? root;

        currentRoomId = GetString(room, "roomId", string.Empty);
        if (string.IsNullOrEmpty(currentRoomId))
        {
            currentRoomId = GetString(room, "_id", string.Empty);
        }
        if (string.IsNullOrEmpty(currentRoomId))
        {
            currentRoomId = GetString(root, "roomId", string.Empty);
        }

        // In PlayWithOops mode, the server can push room updates (including isMove) without a local playCard send.
        // So we must listen as early as gameStart.
        if (IsPlayWithOopsMode)
        {
            EnsureOopsPlayingCardListener();
            EnsureOopsCardOpenListener();
        }

        IList players = GetList(room, "players");
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("GameManager.ApplySocketGameStartData: ignored (missing players list)");
            return;
        }

        hasAppliedSocketGameStartData = true;

        serverUserIdByMappedPlayerNumber.Clear();

        bool useOopsLocalRemap = IsPlayWithOopsMode;
        if (useOopsLocalRemap)
        {
            localPlayerNumber = 1;
        }
        else
        {
            int localServerIndex = FindPlayerIndexByUserId(players, UserId);
            if (localServerIndex < 0) localServerIndex = 0;
            localPlayerNumber = Mathf.Clamp(localServerIndex + 1, 1, 4);
        }

        Dictionary<string, int> userIdToMappedPlayer = useOopsLocalRemap ? BuildOopsUserIdToMappedPlayer(players) : null;
        Dictionary<int, string> mappedPlayerToUserId = useOopsLocalRemap ? BuildOopsMappedPlayerToUserId(userIdToMappedPlayer) : null;

        if (useOopsLocalRemap && mappedPlayerToUserId != null)
        {
            for (int p = 1; p <= 4; p++)
            {
                if (mappedPlayerToUserId.TryGetValue(p, out string id) && !string.IsNullOrEmpty(id))
                {
                    serverUserIdByMappedPlayerNumber[p] = id;
                }
            }
        }

        EnsureHomeSlotsResolvedIfMissing();
        EnsurePiecesSpawnedIfMissing();

        int maxPlayers = Mathf.Clamp(players.Count, 2, 4);
        if (useOopsLocalRemap)
        {
            string turnUserId = ResolveTurnUserId(room, players, string.Empty);
            currentPlayer = ResolveMappedPlayerFromTurnUserId(turnUserId, userIdToMappedPlayer, players);
            LogOopsMapping("GameStart", turnUserId, currentPlayer, mappedPlayerToUserId);
        }
        else
        {
            int turnServerIndexRaw = ResolveTurnServerIndex(room, players, 0);
            int turnIndex = Mathf.Clamp(turnServerIndexRaw, 0, players.Count - 1);
            currentPlayer = Mathf.Clamp(turnIndex + 1, 1, maxPlayers);
        }

        for (int i = 0; i < players.Count; i++)
        {
            IDictionary<string, object> sp = AsStringObjectDict(players[i]);
            if (sp == null) continue;

            string serverUserId = GetString(sp, "user_id", string.Empty);
            int mappedPlayerNumber = useOopsLocalRemap
                ? ResolveMappedPlayerForServerUserId(serverUserId, userIdToMappedPlayer, i)
                : Mathf.Clamp(i + 1, 1, 4);
            if (!string.IsNullOrEmpty(serverUserId))
            {
                serverUserIdByMappedPlayerNumber[mappedPlayerNumber] = serverUserId;
            }

            string playerName = GetString(sp, "name", string.Empty);
            int serverAvatarIndex = GetInt(sp, "avatar", 0);
            int avatarIndex = Mathf.Max(0, serverAvatarIndex - 1);
            Sprite avatarSprite = GetAvatarSpriteForIndex(avatarIndex);
            if (pathManager != null)
            {
                Debug.Log($"GameManager.ApplySocketGameStartData: Setting profile for mappedP={mappedPlayerNumber} name='{playerName}' avatarServerIndex={serverAvatarIndex} avatarIndex={avatarIndex} sprite={(avatarSprite != null ? avatarSprite.name : "<null>")}");
                pathManager.SetPlayerProfile(mappedPlayerNumber, playerName, avatarSprite);
            }

            bool isBot = GetBool(sp, "bot", false);
            if (mappedPlayerNumber == 1) player1IsBot = isBot;
            else if (mappedPlayerNumber == 2) player2IsBot = isBot;
            else if (mappedPlayerNumber == 3) player3IsBot = isBot;
            else if (mappedPlayerNumber == 4) player4IsBot = isBot;

            IList pawns = GetList(sp, "pawns");
            if (pawns == null) continue;

            List<PlayerPiece> pieces = GetPiecesForPlayer(mappedPlayerNumber);
            if (pieces == null || pieces.Count == 0) continue;

            for (int p = 0; p < pawns.Count; p++)
            {
                IDictionary<string, object> pawn = AsStringObjectDict(pawns[p]);
                if (pawn == null) continue;

                int pawnId = GetInt(pawn, "pawnId", 0);
                int position = GetInt(pawn, "position", -1);
                string status = GetString(pawn, "status", string.Empty);
                string pawnObjectId = GetString(pawn, "_id", string.Empty);

                if (pawnId <= 0) continue;

                PlayerPiece piece = FindPieceByPawnId(pieces, pawnId);
                if (piece == null) continue;

                piece.playerNumber = mappedPlayerNumber;
                piece.ApplyServerPawnId(pawnId, pawnObjectId);

                if (position == -1 && string.Equals(status, "BASE", StringComparison.OrdinalIgnoreCase))
                {
                    piece.ApplyServerBaseState();
                }
                else if (position >= 0)
                {
                    piece.ApplyServerPathIndexState(position);
                }
                else
                {
                    piece.ApplyServerBaseState();
                }
            }
        }

        ApplyDefaultPieceSpritesToExistingPieces();
        ApplyPieceScaleToExistingPieces();

        ApplyPieceSortingOrdersToPieces(player1Pieces, 1);
        ApplyPieceSortingOrdersToPieces(player2Pieces, 2);
        ApplyPieceSortingOrdersToPieces(player3Pieces, 3);
        ApplyPieceSortingOrdersToPieces(player4Pieces, 4);

        RefreshOopsDebugFields();

        if (IsPlayWithOopsMode)
        {
            suppressHumanInput = currentPlayer != 1;
            StopCardPickReminder();
            UpdateTurnIndicatorUI();
            UpdateDeckTintForTurn();
            UpdatePiecesInteractivityForOopsTurn();
            StartCardPickReminderIfNeeded();
            RefreshOopsDebugFields();
        }
    }

    private static IDictionary<string, object> AsStringObjectDict(object obj)
    {
        return obj as IDictionary<string, object>;
    }

    private static IDictionary<string, object> GetDict(IDictionary<string, object> dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key)) return null;
        if (!dict.TryGetValue(key, out object value)) return null;
        return AsStringObjectDict(value);
    }

    private static IList GetList(IDictionary<string, object> dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key)) return null;
        if (!dict.TryGetValue(key, out object value)) return null;
        return value as IList;
    }

    private static int GetInt(IDictionary<string, object> dict, string key, int fallback)
    {
        if (dict == null || string.IsNullOrEmpty(key)) return fallback;
        if (!dict.TryGetValue(key, out object value) || value == null) return fallback;
        try
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is float f) return (int)f;
            if (value is double d) return (int)d;
            if (value is string s && int.TryParse(s, out int parsed)) return parsed;
        }
        catch { }
        return fallback;
    }

    private static string ResolveTurnUserId(IDictionary<string, object> room, IList players, string fallback)
    {
        if (room == null) return fallback;
        if (!room.TryGetValue("turnIndex", out object value) || value == null) return fallback;

        try
        {
            if (value is string s)
            {
                if (!string.IsNullOrEmpty(s)) return s;
            }
            if (value is int i)
            {
                IDictionary<string, object> sp = (players != null && i >= 0 && i < players.Count) ? AsStringObjectDict(players[i]) : null;
                string id = GetString(sp, "user_id", string.Empty);
                if (!string.IsNullOrEmpty(id)) return id;
            }
            if (value is long l)
            {
                int i2 = (int)l;
                IDictionary<string, object> sp = (players != null && i2 >= 0 && i2 < players.Count) ? AsStringObjectDict(players[i2]) : null;
                string id = GetString(sp, "user_id", string.Empty);
                if (!string.IsNullOrEmpty(id)) return id;
            }

            IDictionary<string, object> dict = AsStringObjectDict(value);
            if (dict != null)
            {
                string userId = GetString(dict, "user_id", string.Empty);
                if (!string.IsNullOrEmpty(userId)) return userId;
            }
        }
        catch { }

        return fallback;
    }

    private Dictionary<string, int> BuildOopsUserIdToMappedPlayer(IList players)
    {
        Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        string localId = UserId;
        if (!string.IsNullOrEmpty(localId))
        {
            map[localId] = 1;
        }

        int next = 2;
        if (players != null)
        {
            for (int i = 0; i < players.Count && next <= 4; i++)
            {
                IDictionary<string, object> sp = AsStringObjectDict(players[i]);
                if (sp == null) continue;
                string id = GetString(sp, "user_id", string.Empty);
                if (string.IsNullOrEmpty(id)) continue;
                if (!string.IsNullOrEmpty(localId) && string.Equals(id, localId, StringComparison.OrdinalIgnoreCase)) continue;
                if (map.ContainsKey(id)) continue;
                map[id] = next;
                next++;
            }
        }

        if (string.IsNullOrEmpty(localId) && players != null)
        {
            for (int i = 0; i < players.Count && i < 4; i++)
            {
                IDictionary<string, object> sp = AsStringObjectDict(players[i]);
                if (sp == null) continue;
                string id = GetString(sp, "user_id", string.Empty);
                if (string.IsNullOrEmpty(id)) continue;
                if (map.ContainsKey(id)) continue;
                map[id] = Mathf.Clamp(i + 1, 1, 4);
            }
        }

        return map;
    }

    private static Dictionary<int, string> BuildOopsMappedPlayerToUserId(Dictionary<string, int> userIdToMappedPlayer)
    {
        Dictionary<int, string> result = new Dictionary<int, string>();
        if (userIdToMappedPlayer == null) return result;
        foreach (var kv in userIdToMappedPlayer)
        {
            if (!result.ContainsKey(kv.Value)) result[kv.Value] = kv.Key;
        }
        return result;
    }

    private static int ResolveMappedPlayerFromTurnUserId(string turnUserId, Dictionary<string, int> userIdToMappedPlayer, IList players)
    {
        if (string.IsNullOrEmpty(turnUserId))
        {
            return 1;
        }
        if (userIdToMappedPlayer != null && userIdToMappedPlayer.TryGetValue(turnUserId, out int mapped))
        {
            return Mathf.Clamp(mapped, 1, 4);
        }
        int idx = FindPlayerIndexByUserId(players, turnUserId);
        if (idx >= 0) return Mathf.Clamp(idx + 1, 1, 4);
        return 1;
    }

    private static int ResolveMappedPlayerForServerUserId(string serverUserId, Dictionary<string, int> userIdToMappedPlayer, int fallbackServerIndex)
    {
        if (!string.IsNullOrEmpty(serverUserId) && userIdToMappedPlayer != null && userIdToMappedPlayer.TryGetValue(serverUserId, out int mapped))
        {
            return Mathf.Clamp(mapped, 1, 4);
        }
        return Mathf.Clamp(fallbackServerIndex + 1, 1, 4);
    }

    private void LogOopsMapping(string context, string turnUserId, int mappedCurrentPlayer, Dictionary<int, string> mappedPlayerToUserId)
    {
        string p1 = mappedPlayerToUserId != null && mappedPlayerToUserId.TryGetValue(1, out string a) ? a : string.Empty;
        string p2 = mappedPlayerToUserId != null && mappedPlayerToUserId.TryGetValue(2, out string b) ? b : string.Empty;
        string p3 = mappedPlayerToUserId != null && mappedPlayerToUserId.TryGetValue(3, out string c) ? c : string.Empty;
        string p4 = mappedPlayerToUserId != null && mappedPlayerToUserId.TryGetValue(4, out string d) ? d : string.Empty;

        Debug.Log(
            $"<color=#FFC107>[OOPS {context}]</color> " +
            $"<color=#4CAF50>P1</color>='{p1}' " +
            $"<color=#03A9F4>P2</color>='{p2}' " +
            $"<color=#9C27B0>P3</color>='{p3}' " +
            $"<color=#FF5722>P4</color>='{p4}' " +
            $"| <color=#E91E63>TurnUser</color>='{turnUserId}' -> <color=#00E676>currentPlayer</color>={mappedCurrentPlayer} | localUser='{UserId}'");
    }

    private static bool GetBool(IDictionary<string, object> dict, string key, bool fallback)
    {
        if (dict == null || string.IsNullOrEmpty(key)) return fallback;
        if (!dict.TryGetValue(key, out object value) || value == null) return fallback;
        try
        {
            if (value is bool b) return b;
            if (value is int i) return i != 0;
            if (value is long l) return l != 0;
            if (value is string s && bool.TryParse(s, out bool parsed)) return parsed;
        }
        catch { }
        return fallback;
    }

    private static string GetString(IDictionary<string, object> dict, string key, string fallback)
    {
        if (dict == null || string.IsNullOrEmpty(key)) return fallback;
        if (!dict.TryGetValue(key, out object value) || value == null) return fallback;
        return value.ToString();
    }

    private static int ResolveTurnServerIndex(IDictionary<string, object> room, IList players, int fallback)
    {
        if (room == null) return fallback;
        if (!room.TryGetValue("turnIndex", out object value) || value == null) return fallback;

        try
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is float f) return (int)f;
            if (value is double d) return (int)d;

            if (value is string s)
            {
                if (int.TryParse(s, out int parsedIndex)) return parsedIndex;
                int idx = FindPlayerIndexByUserId(players, s);
                if (idx >= 0) return idx;
            }

            IDictionary<string, object> dict = AsStringObjectDict(value);
            if (dict != null)
            {
                string userId = GetString(dict, "user_id", string.Empty);
                if (!string.IsNullOrEmpty(userId))
                {
                    int idx = FindPlayerIndexByUserId(players, userId);
                    if (idx >= 0) return idx;
                }
            }
        }
        catch { }

        return fallback;
    }

    private static int FindPlayerIndexByUserId(IList players, string userId)
    {
        if (players == null || players.Count == 0 || string.IsNullOrEmpty(userId)) return -1;
        for (int i = 0; i < players.Count; i++)
        {
            IDictionary<string, object> sp = AsStringObjectDict(players[i]);
            if (sp == null) continue;
            string id = GetString(sp, "user_id", string.Empty);
            if (string.Equals(id, userId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static int MapServerIndexToLocalPlayerNumber(int serverIndex, int localServerIndex)
    {
        if (serverIndex == localServerIndex) return 1;
        if (serverIndex < localServerIndex) return serverIndex + 2;
        return serverIndex + 1;
    }

    private static PlayerPiece FindPieceByPawnId(List<PlayerPiece> pieces, int pawnId)
    {
        if (pieces == null) return null;
        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            if (p.pieceNumber == pawnId) return p;
        }
        return null;
    }

    private static GameManager instance;

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
            }

            return instance;
        }
    }

    public void HandleBumpAfterMove(PlayerPiece movedPiece, Transform landedPosition)
    {
        if (movedPiece == null || landedPosition == null)
        {
            return;
        }

        // Home-path pieces should not be involved in bump interactions.
        movedPiece.SyncCurrentPathIndexFromTransform();
        if (movedPiece.IsOnHomePath())
        {
            return;
        }

        int moverPlayer = movedPiece.playerNumber;

        foreach (PlayerPiece other in GetAllActivePieces())
        {
            if (other == null)
            {
                continue;
            }

            if (other == movedPiece)
            {
                continue;
            }

            if (other.playerNumber == moverPlayer)
            {
                continue;
            }

            if (other.IsAtHome())
            {
                continue;
            }

            other.SyncCurrentPathIndexFromTransform();
            if (other.IsOnHomePath())
            {
                continue;
            }

            Transform otherPos = other.GetCurrentPositionTransform();
            bool sameTransform = (otherPos == landedPosition || other.transform.parent == landedPosition);
            bool sameWorldPos = false;
            Vector3 moverWorldPos = movedPiece.transform.position;
            Vector3 otherWorldPos = other.transform.position;
            const float bumpEpsilon = 0.05f;
            if (Vector3.Distance(moverWorldPos, otherWorldPos) <= bumpEpsilon)
            {
                sameWorldPos = true;
            }
            else if (Vector3.Distance(landedPosition.position, otherWorldPos) <= bumpEpsilon)
            {
                sameWorldPos = true;
            }

            if (sameTransform || sameWorldPos)
            {
                Debug.Log($"💥 BUMP! Player {moverPlayer} Piece {movedPiece.pieceNumber} landed on opponent piece {other.pieceNumber}. Sending opponent back to START.");
                other.ReturnToHome();
                other.SyncCurrentPathIndexFromTransform();
                return;
            }
        }
    }

    public void HandleBumpAnyPieceAtPosition(PlayerPiece movedPiece, Transform landedPosition)
    {
        if (movedPiece == null || landedPosition == null)
        {
            return;
        }

        movedPiece.SyncCurrentPathIndexFromTransform();
        if (movedPiece.IsOnHomePath())
        {
            return;
        }

        foreach (PlayerPiece other in GetAllActivePieces())
        {
            if (other == null) continue;
            if (other == movedPiece) continue;
            if (other.IsAtHome()) continue;

            other.SyncCurrentPathIndexFromTransform();
            if (other.IsOnHomePath())
            {
                continue;
            }

            Transform otherPos = other.GetCurrentPositionTransform();
            bool sameTransform = (otherPos == landedPosition || other.transform.parent == landedPosition);
            bool sameWorldPos = false;
            Vector3 otherWorldPos = other.transform.position;
            const float bumpEpsilon = 0.05f;
            if (Vector3.Distance(landedPosition.position, otherWorldPos) <= bumpEpsilon)
            {
                sameWorldPos = true;
            }

            if (sameTransform || sameWorldPos)
            {
                Debug.Log($"💥 SLIDE BUMP! Piece {movedPiece.pieceNumber} landed on piece {other.pieceNumber}. Sending it back to START.");
                other.ReturnToHome();
                other.SyncCurrentPathIndexFromTransform();
            }
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;

            LoadPlayerProfileFromPrefs();
            RefreshActiveCardDeckAnimator();
            RefreshSessionDebugFields();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }

        Application.targetFrameRate = 120;
    }

    void OnEnable()
    {
        if (instance == null)
        {
            instance = this;
        }

        LoadPlayerProfileFromPrefs();

        RefreshActiveCardDeckAnimator();

        RefreshSessionDebugFields();

        StartCoroutine(RefreshCardPickReminderNextFrame());
    }

    public void LoadPlayerProfileFromPrefs()
    {
        string loadedName = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
        int loadedAvatarIndex = Mathf.Max(0, PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0));

        bool hasSavedName = PlayerPrefs.HasKey(PlayerNameKey);
        bool hasSavedAvatar = PlayerPrefs.HasKey(PlayerAvatarIndexKey);

        if (!hasSavedName || string.IsNullOrWhiteSpace(loadedName))
        {
            loadedName = "Player";
            PlayerPrefs.SetString(PlayerNameKey, loadedName);
        }

        if (!hasSavedAvatar)
        {
            loadedAvatarIndex = 0;
            PlayerPrefs.SetInt(PlayerAvatarIndexKey, loadedAvatarIndex);
        }

        if (playerAvatarSprites != null && playerAvatarSprites.Count > 0)
        {
            if (loadedAvatarIndex < 0 || loadedAvatarIndex >= playerAvatarSprites.Count)
            {
                loadedAvatarIndex = 0;
                PlayerPrefs.SetInt(PlayerAvatarIndexKey, loadedAvatarIndex);
            }

            Sprite avatar = GetAvatarSpriteForIndex(loadedAvatarIndex);
            if (avatar == null)
            {
                int firstValid = playerAvatarSprites.FindIndex(s => s != null);
                if (firstValid < 0) firstValid = 0;
                loadedAvatarIndex = Mathf.Clamp(firstValid, 0, playerAvatarSprites.Count - 1);
                PlayerPrefs.SetInt(PlayerAvatarIndexKey, loadedAvatarIndex);
            }
        }

        // Fallback: if GameManager's avatar list isn't assigned, try to resolve from ProfileSelectionController buttons.
        if (ResolveAvatarSpriteForIndex(loadedAvatarIndex) == null)
        {
            ProfileSelectionController c = FindAnyProfileSelectionController();
            if (c != null)
            {
                int count = Mathf.Max(0, c.AvatarCount);
                if (count > 0)
                {
                    if (loadedAvatarIndex < 0 || loadedAvatarIndex >= count)
                    {
                        loadedAvatarIndex = 0;
                        PlayerPrefs.SetInt(PlayerAvatarIndexKey, loadedAvatarIndex);
                    }

                    Sprite resolved = c.GetAvatarSpriteForIndexPublic(loadedAvatarIndex);
                    if (resolved == null)
                    {
                        int firstValid = -1;
                        for (int i = 0; i < count; i++)
                        {
                            if (c.GetAvatarSpriteForIndexPublic(i) != null)
                            {
                                firstValid = i;
                                break;
                            }
                        }

                        if (firstValid >= 0)
                        {
                            loadedAvatarIndex = firstValid;
                            PlayerPrefs.SetInt(PlayerAvatarIndexKey, loadedAvatarIndex);
                        }
                    }
                }
            }
        }

        PlayerPrefs.Save();

        PlayerName = loadedName;
        PlayerAvatarIndex = loadedAvatarIndex;
        PlayerAvatarSprite = ResolveAvatarSpriteForIndex(PlayerAvatarIndex);

        ApplyAvatarSpriteToAllAvatarBorders();
        RefreshSessionDebugFields();
        ProfileChanged?.Invoke();
    }

    private void ApplyAvatarSpriteToAllAvatarBorders()
    {
        if (!Application.isPlaying) return;

        Sprite s = PlayerAvatarSprite;
        if (s == null) return;

        Image[] allImages = Resources.FindObjectsOfTypeAll<Image>();
        if (allImages == null) return;

        for (int i = 0; i < allImages.Length; i++)
        {
            Image img = allImages[i];
            if (img == null) continue;
            if (!img.gameObject.scene.IsValid()) continue;

            // Only apply to images that are under an AvatarBorder root.
            bool underAvatarBorder = false;
            Transform t = img.transform;
            while (t != null)
            {
                string tn = t.name;
                if (!string.IsNullOrEmpty(tn) && tn.ToLowerInvariant().Contains("avatarborder"))
                {
                    underAvatarBorder = true;
                    break;
                }
                t = t.parent;
            }
            if (!underAvatarBorder) continue;

            // Skip decorative images (rings/frames/borders). Prefer avatar-bearing images.
            string n = img.gameObject.name;
            string ln = !string.IsNullOrEmpty(n) ? n.ToLowerInvariant() : string.Empty;
            if (ln.Contains("ring") || ln.Contains("frame") || ln.Contains("border") || ln.Contains("mask")) continue;
            if (!(ln.Contains("image") || ln.Contains("avatar") || ln.Contains("icon"))) continue;

            img.sprite = s;
        }
    }

    private Sprite GetAvatarSpriteForIndex(int index)
    {
        if (playerAvatarSprites == null || playerAvatarSprites.Count == 0) return null;
        if (index < 0 || index >= playerAvatarSprites.Count) return null;
        return playerAvatarSprites[index];
    }

    private ProfileSelectionController FindAnyProfileSelectionController()
    {
        ProfileSelectionController c = FindObjectOfType<ProfileSelectionController>();
        if (c != null) return c;

        // Includes inactive objects (and editor-only objects), so we filter to valid scene instances.
        ProfileSelectionController[] all = Resources.FindObjectsOfTypeAll<ProfileSelectionController>();
        if (all == null) return null;

        for (int i = 0; i < all.Length; i++)
        {
            ProfileSelectionController p = all[i];
            if (p == null) continue;
            if (!p.gameObject.scene.IsValid()) continue;
            return p;
        }

        return null;
    }

    private Sprite ResolveAvatarSpriteForIndex(int index)
    {
        Sprite s = GetAvatarSpriteForIndex(index);
        if (s != null) return s;

        ProfileSelectionController c = FindAnyProfileSelectionController();
        if (c == null) return null;
        return c.GetAvatarSpriteForIndexPublic(index);
    }

    public void SetSelectedLobbyRewards(long winningCoin, long winningDiamond)
    {
        selectedLobbyWinningCoin = Math.Max(0L, winningCoin);
        selectedLobbyWinningDiamond = Math.Max(0L, winningDiamond);
    }

    public void SetPlayerName(string name, bool saveToPrefs)
    {
        PlayerName = name != null ? name.Trim() : string.Empty;

        if (saveToPrefs)
        {
            PlayerPrefs.SetString(PlayerNameKey, PlayerName);
            PlayerPrefs.Save();
        }

        RefreshSessionDebugFields();
        ProfileChanged?.Invoke();
    }

    public void SetPlayerAvatar(int avatarIndex, Sprite avatarSprite, bool saveToPrefs)
    {
        PlayerAvatarIndex = Mathf.Max(0, avatarIndex);
        PlayerAvatarSprite = avatarSprite != null ? avatarSprite : ResolveAvatarSpriteForIndex(PlayerAvatarIndex);

        if (saveToPrefs)
        {
            PlayerPrefs.SetInt(PlayerAvatarIndexKey, PlayerAvatarIndex);
            PlayerPrefs.Save();
        }

        ApplyAvatarSpriteToAllAvatarBorders();

        RefreshSessionDebugFields();
        ProfileChanged?.Invoke();
    }

    private IEnumerator RefreshCardPickReminderNextFrame()
    {
        yield return null;
        RefreshActiveCardDeckAnimator();
        UpdateDeckTintForTurn();
        StartCardPickReminderIfNeeded();
    }

    private void UpdateDeckTintForTurn()
    {
        if (!enableOpponentDeckTint) return;
        if (!modeSelected) return;

        RefreshActiveCardDeckAnimator();
        if (activeCardDeckAnimator == null) return;

        if (!vsBotMode && !IsPlayWithOopsMode)
        {
            activeCardDeckAnimator.ClearDeckTint();
            return;
        }

        bool isLocalTurn = currentPlayer == Mathf.Clamp(localPlayerNumber, 1, 4);
        if (isLocalTurn)
        {
            activeCardDeckAnimator.ClearDeckTint();
        }
        else
        {
            activeCardDeckAnimator.ApplyDeckTint(opponentDeckTintColor);
        }
    }

    public bool CanLocalPlayerClickCard()
    {
        if (!IsPlayWithOopsMode && !IsOnlineFriendsMode) return true;
        if (gameOver) return false;
        if (!modeSelected) return false;
        if (cardAnimationLock) return false;
        if (cardPicked) return false;

        int local = Mathf.Clamp(localPlayerNumber, 1, 4);
        return currentPlayer == local;
    }

    private void UpdatePiecesInteractivityForOopsTurn()
    {
        if (pausePopupOpen) return;
        if (!IsPlayWithOopsMode)
        {
            UpdatePiecesInteractivityForTurn();
            return;
        }

        int local = Mathf.Clamp(localPlayerNumber, 1, 4);
        bool isLocalTurn = currentPlayer == local;

        // In PlayWithOops, when a card is already picked we must keep piece interactivity aligned with
        // the picked-card rules (especially Card 7 split remainder), otherwise room updates can leave
        // pieces highlighted but not clickable.
        if (isLocalTurn && cardPicked)
        {
            // While waiting for the second split move, only allow valid remainder moves on a different pawn.
            if (isSplitMode && selectedPieceForSplit != null && remainingSteps > 0)
            {
                foreach (var p in GetAllActivePieces())
                {
                    if (p == null) continue;
                    p.ShowPiece();
                }

                ApplyInteractivityForSplitRemainder(remainingSteps, selectedPieceForSplit);
                return;
            }

            // For all other picked cards, mirror the normal card-driven filtering.
            foreach (var p in GetAllActivePieces())
            {
                if (p == null) continue;
                p.ShowPiece();
                // Temporarily enable; ApplyInteractivityForCard will clamp to legal moves.
                p.SetClickable(p.playerNumber == local);
            }

            ApplyInteractivityForCard(currentCardValue);
            return;
        }

        foreach (var p in GetAllActivePieces())
        {
            if (p == null) continue;
            p.ShowPiece();
            p.SetClickable(isLocalTurn && p.playerNumber == local);
        }
    }

    private void RefreshActiveCardDeckAnimator()
    {
        // Select based on active mode (2P vs 4P). If mode not selected yet, fall back to a best-effort find.
        if (modeSelected)
        {
            if (playerCount <= 2)
            {
                activeCardDeckAnimator = cardDeckAnimator2P;
            }
            else
            {
                activeCardDeckAnimator = cardDeckAnimator4P;
            }

            // If not assigned, try find inside the active board root.
            if (activeCardDeckAnimator == null || !activeCardDeckAnimator.gameObject.activeInHierarchy)
            {
                ModeSetup active = (playerCount <= 2) ? setup2P : setup4P;
                if (active != null && active.boardRoot != null)
                {
                    CardDeckAnimator found = active.boardRoot.GetComponentInChildren<CardDeckAnimator>(true);
                    if (found != null && found.gameObject.activeInHierarchy)
                    {
                        activeCardDeckAnimator = found;
                    }
                }
            }

            // If still inactive, try the other reference (useful when both are assigned but one board is disabled).
            if (activeCardDeckAnimator == null || !activeCardDeckAnimator.gameObject.activeInHierarchy)
            {
                CardDeckAnimator other = (playerCount <= 2) ? cardDeckAnimator4P : cardDeckAnimator2P;
                if (other != null && other.gameObject.activeInHierarchy)
                {
                    activeCardDeckAnimator = other;
                }
            }
        }

        if (activeCardDeckAnimator == null)
        {
            // Ultimate fallback.
            CardDeckAnimator found = FindObjectOfType<CardDeckAnimator>();
            if (found != null && found.gameObject.activeInHierarchy)
            {
                activeCardDeckAnimator = found;
            }
            else
            {
                activeCardDeckAnimator = null;
            }
        }
    }

    void Start()
    {
        // Do not initialize gameplay until a mode (2P/4P) has been selected.
        // This avoids errors on the splash/menu screen where board references are not yet applied.
    }

    public void OnHomeScreenOpened()
    {
        UnhookGameplaySocketDisconnectWatcher();

        if (SocketConnection.Instance != null)
        {
            SocketConnection.Instance.SetSuspended(true);
        }
        else
        {
            SocketConnection socket = FindObjectOfType<SocketConnection>();
            if (socket != null)
            {
                socket.SetSuspended(true);
            }
        }

        ResetGameplayForNextMatch();
        gameplaySettingsButtonOpenLockActive = false;
        if (gameplaySettingsButtonOpenLockCoroutine != null)
        {
            StopCoroutine(gameplaySettingsButtonOpenLockCoroutine);
            gameplaySettingsButtonOpenLockCoroutine = null;
        }
        gameplaySettingsButtonExternalLockCount = 0;

        if (dailyBonusCoroutine != null)
        {
            StopCoroutine(dailyBonusCoroutine);
            dailyBonusCoroutine = null;
        }

        dailyBonusCoroutine = StartCoroutine(TryShowDailyBonusAfterDelay());

        StartHomeInternetAutoLoginWatcher();
    }

    private void HookGameplaySocketDisconnectWatcher()
    {
        SocketConnection socket = SocketConnection.Instance != null ? SocketConnection.Instance : FindObjectOfType<SocketConnection>();
        if (socket == null) return;
        socket.OnStateChanged -= HandleSocketStateChangedDuringGameplay;
        socket.OnStateChanged += HandleSocketStateChangedDuringGameplay;
    }

    private void UnhookGameplaySocketDisconnectWatcher()
    {
        SocketConnection socket = SocketConnection.Instance != null ? SocketConnection.Instance : FindObjectOfType<SocketConnection>();
        if (socket == null) return;
        socket.OnStateChanged -= HandleSocketStateChangedDuringGameplay;
    }

    private void HandleSocketStateChangedDuringGameplay(SocketState state)
    {
        if (state != SocketState.Disconnected && state != SocketState.Error) return;
        if (gameplayConnectionPopupOpen || gameplayConnectionReturnQueued) return;

        string msg = state == SocketState.Error
            ? gameplayErrorMessagePrefix
            : gameplayDisconnectedMessage;

        ShowGameplayConnectionPopup(msg);
    }

    private void ShowGameplayConnectionPopup(string message)
    {
        gameplayConnectionReturnQueued = true;

        if (gameplayConnectionPopupMessageText != null)
        {
            gameplayConnectionPopupMessageText.text = message ?? string.Empty;
        }

        if (gameplayConnectionPopupOkButton != null)
        {
            gameplayConnectionPopupOkButton.onClick.RemoveListener(OnGameplayConnectionPopupOkClicked);
            gameplayConnectionPopupOkButton.onClick.AddListener(OnGameplayConnectionPopupOkClicked);
        }

        if (gameplayConnectionPopupRoot != null)
        {
            gameplayConnectionPopupRoot.SetActive(true);
            gameplayConnectionPopupOpen = true;
        }
        else
        {
            OnGameplayConnectionPopupOkClicked();
        }
    }

    private void OnGameplayConnectionPopupOkClicked()
    {
        if (gameplayConnectionPopupOkButton != null)
        {
            gameplayConnectionPopupOkButton.onClick.RemoveListener(OnGameplayConnectionPopupOkClicked);
        }

        if (gameplayConnectionPopupRoot != null)
        {
            gameplayConnectionPopupRoot.SetActive(false);
        }

        gameplayConnectionPopupOpen = false;

        SocketConnection socket = SocketConnection.Instance != null ? SocketConnection.Instance : FindObjectOfType<SocketConnection>();
        if (socket != null)
        {
            socket.DisconnectManuallySilent();
            socket.SetSuspended(true);
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        if (screenManager != null)
        {
            GameObject home = FindScreenByNameInLoadedScenes("HomePanel");
            if (home != null)
            {
                screenManager.OpenScreen(home);
            }
            else
            {
                // Fallback: some scenes use LobbyPanel as the home screen.
                screenManager.OpenScreenByName("HomePanel");
                screenManager.OpenScreenByName("LobbyPanel");
            }
        }

        gameplayConnectionReturnQueued = false;
    }

    private static GameObject FindScreenByNameInLoadedScenes(string screenName)
    {
        if (string.IsNullOrWhiteSpace(screenName)) return null;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        if (all == null || all.Length == 0) return null;

        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null) continue;
            if (!string.Equals(go.name, screenName, StringComparison.Ordinal)) continue;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (go.hideFlags != HideFlags.None) continue;
            return go;
        }

        return null;
    }

    private void StartHomeInternetAutoLoginWatcher()
    {
        if (!Application.isPlaying) return;

        lastHomeReachability = Application.internetReachability;
        homeAutoLoginPendingAfterReconnect = false;

        if (homeInternetAutoLoginRoutine != null) return;
        homeInternetAutoLoginRoutine = StartCoroutine(HomeInternetAutoLoginLoop());
    }

    private IEnumerator HomeInternetAutoLoginLoop()
    {
        while (true)
        {
            float interval = Mathf.Max(0.1f, homeAutoLoginInternetPollSeconds);
            yield return new WaitForSecondsRealtime(interval);

            NetworkReachability now = Application.internetReachability;
            bool wentOffline = lastHomeReachability != NetworkReachability.NotReachable && now == NetworkReachability.NotReachable;
            bool internetCameBack = lastHomeReachability == NetworkReachability.NotReachable && now != NetworkReachability.NotReachable;
            lastHomeReachability = now;

            if (wentOffline)
            {
                homeAutoLoginInFlight = false;
                homeAutoLoginSucceeded = false;
                homeAutoLoginCreatedUser = false;
                homeAutoLoginPendingAfterReconnect = false;
                homeAutoLoginFailureCount = 0;
                homeAutoLoginNextAllowedTime = 0f;
                HideHomeAutoLoginLoader();
            }

            if (internetCameBack)
            {
                homeAutoLoginPendingAfterReconnect = true;
                homeAutoLoginFailureCount = 0;
                float delay = Mathf.Max(0f, Mathf.Max(homeAutoLoginReconnectDelaySeconds, homeAutoLoginStabilizeSeconds));
                homeAutoLoginNextAllowedTime = Time.unscaledTime + delay;
            }

            if (homeAutoLoginPendingAfterReconnect && now != NetworkReachability.NotReachable)
            {
                TryHomeAutoLoginIfNeeded();
            }
        }
    }

    private void StopHomeInternetAutoLoginWatcher()
    {
        if (homeInternetAutoLoginRoutine == null) return;
        StopCoroutine(homeInternetAutoLoginRoutine);
        homeInternetAutoLoginRoutine = null;
    }

    private bool ShouldHomeAutoLogin()
    {
        if (homeAutoLoginSucceeded) return false;
        if (homeAutoLoginInFlight) return false;
        if (Time.unscaledTime < homeAutoLoginNextAllowedTime) return false;
        if (Application.internetReachability == NetworkReachability.NotReachable) return false;

        UserSession.LoadFromPrefs();
        if (string.IsNullOrEmpty(UserSession.UserId) || string.IsNullOrEmpty(UserSession.Username)) return false;

        return true;
    }

    private void TryHomeAutoLoginIfNeeded()
    {
        if (!ShouldHomeAutoLogin()) return;

        ApiManager api = ApiManager.Instance != null ? ApiManager.Instance : FindObjectOfType<ApiManager>();
        if (api == null) return;

        HomeAutoLoginRequest req = new HomeAutoLoginRequest
        {
            user_id = UserSession.UserId,
            username = UserSession.Username,
            isGuest = UserSession.IsGuest
        };

        string json = JsonUtility.ToJson(req);
        homeAutoLoginInFlight = true;
        homeAutoLoginNextAllowedTime = Time.unscaledTime + 2f;

        ShowHomeAutoLoginLoader();

        api.Post(api.GetLoginApiUrl(), json, OnHomeAutoLoginSuccess, OnHomeAutoLoginError);
    }

    private void OnHomeAutoLoginSuccess(string response)
    {
        homeAutoLoginInFlight = false;
        homeAutoLoginSucceeded = true;
        homeAutoLoginCreatedUser = ShouldShowWelcomeForHomeAutoLogin(response);
        homeAutoLoginPendingAfterReconnect = false;
        homeAutoLoginFailureCount = 0;

        HideHomeAutoLoginLoader();

        PlayerPrefs.SetString("LOGIN_TYPE", UserSession.IsGuest ? "GUEST" : "USER");
        PlayerPrefs.SetString("USER_DATA", response);
        PlayerPrefs.Save();

        UserSession.TryApplyLoginResponse(response);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerName(UserSession.Username, saveToPrefs: true);
            GameManager.Instance.SetPlayerAvatar(UserSession.AvatarIndex, null, saveToPrefs: true);
            GameManager.Instance.RefreshSessionDebugFields();
        }

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetCoins(UserSession.Coins);
            PlayerWallet.Instance.SetDiamonds(UserSession.Diamonds);
        }
        else
        {
            PlayerPrefs.SetInt("PLAYER_COINS", UserSession.Coins);
            PlayerPrefs.SetInt("PLAYER_DIAMONDS", UserSession.Diamonds);
            PlayerPrefs.Save();
        }

        OpenLoginAndShowWelcomeAfterAutoLogin();
    }

    private void OnHomeAutoLoginError(string error)
    {
        homeAutoLoginInFlight = false;

        if (homeAutoLoginPendingAfterReconnect && Application.internetReachability != NetworkReachability.NotReachable)
        {
            homeAutoLoginFailureCount = Mathf.Clamp(homeAutoLoginFailureCount + 1, 0, 10);
            float delay = Mathf.Min(30f, 2f * Mathf.Pow(2f, Mathf.Max(0, homeAutoLoginFailureCount - 1)));
            homeAutoLoginNextAllowedTime = Time.unscaledTime + delay;
            return;
        }

        homeAutoLoginNextAllowedTime = Time.unscaledTime + 2f;
    }

    private void ShowHomeAutoLoginLoader()
    {
        if (!showHomeAutoLoginLoader) return;

        if (homeAutoLoginLoaderPanel == null)
        {
            homeAutoLoginLoaderPanel = ResolveActiveLoaderPanelAnimator();
        }

        if (homeAutoLoginLoaderPanel == null) return;

        if (!homeAutoLoginLoaderPanel.gameObject.activeSelf)
        {
            homeAutoLoginLoaderPanel.gameObject.SetActive(true);
        }

        string txt = string.IsNullOrWhiteSpace(homeAutoLoginLoaderBaseText) ? null : homeAutoLoginLoaderBaseText.Trim();
        homeAutoLoginLoaderPanel.ShowLoader(txt);
    }

    private void HideHomeAutoLoginLoader()
    {
        if (homeAutoLoginLoaderPanel != null)
        {
            homeAutoLoginLoaderPanel.Hide();
        }
    }

    private void OpenLoginAndShowWelcomeAfterAutoLogin()
    {
        if (!homeAutoLoginCreatedUser)
        {
            return;
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<ScreenManager>();
        }

        WelcomeLoginPopup popup = FindObjectOfType<WelcomeLoginPopup>(true);
        if (popup == null)
        {
            return;
        }

        if (WelcomeLoginPopup.HasShownForUser(UserSession.UserId)) return;

        if (loginScreen == null)
        {
            loginScreen = GameObject.Find("LoginScreen");
        }

        if (screenManager != null && loginScreen != null)
        {
            screenManager.OpenScreen(loginScreen);
        }

        StartCoroutine(ShowWelcomeAfterAutoLoginScreenReady(popup, loginScreen));
    }

    private bool ShouldShowWelcomeForHomeAutoLogin(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        HomeAutoLoginResponseMeta meta;
        try
        {
            meta = JsonUtility.FromJson<HomeAutoLoginResponseMeta>(rawJson);
        }
        catch
        {
            return false;
        }

        if (meta == null || !meta.success) return false;

        string msg = meta.message ?? string.Empty;
        return msg.IndexOf("created", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private IEnumerator ShowWelcomeAfterAutoLoginScreenReady(WelcomeLoginPopup popup, GameObject loginScreenObj)
    {
        if (popup == null) yield break;

        float timeoutAt = Time.unscaledTime + 2f;
        yield return null;

        while (loginScreenObj != null && !loginScreenObj.activeInHierarchy && Time.unscaledTime < timeoutAt)
        {
            yield return null;
        }

        if (popup == null) yield break;

        bool shown = popup.ShowIfFirstTime(UserSession.UserId, UserSession.Username, () =>
        {
            if (screenManager != null)
            {
                screenManager.OpenScreenByName("HomePanel");
            }
        });

        if (!shown)
        {
            if (screenManager != null)
            {
                screenManager.OpenScreenByName("HomePanel");
            }
        }
    }

    private void ResetGameplayForNextMatch()
    {
        if (!modeSelected)
        {
            return;
        }

        if (pathManager == null)
        {
            pathManager = FindObjectOfType<PlayerPathManager>();
        }
        if (pathManager != null)
        {
            pathManager.RequestClearProfilesOnNextDisable();
        }

        hasAppliedSocketGameStartData = false;

        StopCardPickReminder();

        if (deckReadyReminderCoroutine != null)
        {
            StopCoroutine(deckReadyReminderCoroutine);
            deckReadyReminderCoroutine = null;
        }

        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }
        botTurnInProgress = false;

        if (turnPulseCoroutine != null)
        {
            StopCoroutine(turnPulseCoroutine);
            turnPulseCoroutine = null;
        }

        if (moveWatchdogCoroutine != null)
        {
            StopCoroutine(moveWatchdogCoroutine);
            moveWatchdogCoroutine = null;
        }
        moveWatchdogToken++;

        StopTurnCountdown();

        moveInputLockActive = false;
        moveInputLockPlayer = 0;
        suppressHumanInput = false;
        pausePopupOpen = false;

        gameOver = false;
        winningPlayer = 0;

        if (!IsPlayWithOopsMode && !IsOnlineFriendsMode)
        {
            currentPlayer = 1;
        }
        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = string.Empty;
        currentCardPower2 = string.Empty;
        currentCardHandler = null;

        pendingOopsOpenCard = null;
        hasOopsCardOpenListener = false;
        hasOopsPlayingCardListener = false;
        serverUserIdByMappedPlayerNumber.Clear();
        oopsPendingOriginalStepsByPawnKey.Clear();

        oopsSplitAwaitingSecondPiece = false;
        oopsSplitMoveSent = false;
        oopsSplitFirstPawnId = 0;
        oopsSplitFirstSteps = 0;

        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;

        isCard10Mode = false;
        isCard11Mode = false;
        isCard12Mode = false;
        isSorryMode = false;
        selectedPieceForCard11 = null;
        selectedPieceForCard12 = null;
        selectedPieceForSorry = null;

        extraTurnPending = false;
        pendingSwitchTurn = false;
        cardAnimationLock = false;

        RefreshActiveCardDeckAnimator();
        if (cardDeckAnimator2P != null) { cardDeckAnimator2P.StopCardPickArrow(); cardDeckAnimator2P.ClearCards(); cardDeckAnimator2P.ClearDeckTint(); }
        if (cardDeckAnimator4P != null) { cardDeckAnimator4P.StopCardPickArrow(); cardDeckAnimator4P.ClearCards(); cardDeckAnimator4P.ClearDeckTint(); }
        if (activeCardDeckAnimator != null) { activeCardDeckAnimator.StopCardPickArrow(); activeCardDeckAnimator.ClearDeckTint(); }

        PlayerPiece.ClearAllPiecesHighlights();

        AssignHomePositionsFromStartPoints();
        foreach (var p in GetAllActivePieces())
        {
            if (p == null) continue;
            p.transform.DOKill();
            p.ReturnToHome();
            p.SetClickable(false);
        }

        if (player1TurnImage != null) player1TurnImage.SetActive(false);
        if (player2TurnImage != null) player2TurnImage.SetActive(false);
        if (player3TurnImage != null) player3TurnImage.SetActive(false);
        if (player4TurnImage != null) player4TurnImage.SetActive(false);

        PopupHandler popupHandler = FindObjectOfType<PopupHandler>();
        if (popupHandler != null)
        {
            popupHandler.CloseAllInstant();
        }

        DestroyAllBoardPieces();

        gameplayInitialized = false;
    }

    private IEnumerator TryShowDailyBonusAfterDelay()
    {
        float wait = Mathf.Max(0f, dailyBonusSpawnDelaySeconds);
        if (wait > 0f) yield return new WaitForSeconds(wait);
        else yield return null;

        dailyBonusCoroutine = null;
        TryShowDailyBonusNow();
    }

    private void TryShowDailyBonusNow()
    {
        if (dailyBonusPanelPrefab == null)
        {
            return;
        }

        if (activeDailyBonusInstance != null)
        {
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            return;
        }

        long nowTicks = DateTime.UtcNow.Ticks;
        long nextTicks = 0;
        string stored = PlayerPrefs.GetString(DailyBonusNextUtcTicksKey, "0");
        long.TryParse(stored, out nextTicks);

        if (nextTicks > nowTicks)
        {
            return;
        }

        Transform parent = dailyBonusSpawnParent;
        if (parent == null)
        {
            Canvas c = FindObjectOfType<Canvas>();
            if (c != null) parent = c.transform;
        }

        activeDailyBonusInstance = Instantiate(dailyBonusPanelPrefab, parent);
        activeDailyBonusInstance.name = dailyBonusPanelPrefab.name;

        BonusPanelSelectionController selection = activeDailyBonusInstance.GetComponentInChildren<BonusPanelSelectionController>(true);
        if (selection != null)
        {
            selection.OnRewardFinalized -= HandleDailyBonusRewardFinalized;
            selection.OnRewardFinalized += HandleDailyBonusRewardFinalized;
        }
    }

    private void HandleDailyBonusRewardFinalized(int reward)
    {
        int current = PlayerPrefs.GetInt(PlayerCoinsKey, 0);
        int next = current + Mathf.Max(0, reward);
        PlayerPrefs.SetInt(PlayerCoinsKey, next);

        long nextTicks = DateTime.UtcNow.AddHours(24).Ticks;
        PlayerPrefs.SetString(DailyBonusNextUtcTicksKey, nextTicks.ToString());
        PlayerPrefs.Save();

        if (activeDailyBonusInstance != null)
        {
            BonusPanelSelectionController selection = activeDailyBonusInstance.GetComponentInChildren<BonusPanelSelectionController>(true);
            if (selection != null)
            {
                selection.OnRewardFinalized -= HandleDailyBonusRewardFinalized;
            }

            Destroy(activeDailyBonusInstance);
            activeDailyBonusInstance = null;
        }
    }

    public void OnGameplayScreenOpened()
    {
        StopHomeInternetAutoLoginWatcher();

        gameplayConnectionReturnQueued = false;
        HookGameplaySocketDisconnectWatcher();

        gameplaySettingsButtonOpenLockActive = true;
        if (gameplaySettingsButtonOpenLockCoroutine != null)
        {
            StopCoroutine(gameplaySettingsButtonOpenLockCoroutine);
        }
        UpdateGameplaySettingsButtonInteractivity();

        gameplaySettingsButtonOpenLockCoroutine = StartCoroutine(ReleaseGameplaySettingsButtonOpenLockAfterDelay());

        if (!gameplayInitialized)
        {
            InitializeGameplay(firstTime: true);
        }
        else
        {
            InitializeGameplay(firstTime: false);
        }
    }

    private IEnumerator ReleaseGameplaySettingsButtonOpenLockAfterDelay()
    {
        float wait = Mathf.Max(0f, gameplaySettingsButtonDisableOnOpenSeconds);
        if (wait > 0f) yield return new WaitForSeconds(wait);
        else yield return null;

        gameplaySettingsButtonOpenLockCoroutine = null;
        gameplaySettingsButtonOpenLockActive = false;
        UpdateGameplaySettingsButtonInteractivity();
    }

    void InitializeGameplay(bool firstTime)
    {
        // If mode is not selected yet (e.g., splash screen), skip initialization entirely.
        if (!modeSelected)
        {
            return;
        }

        // Auto find PlayerPathManager
        if (pathManager == null)
        {
            pathManager = FindObjectOfType<PlayerPathManager>();
        }

        if (pathManager == null)
        {
            Debug.LogError("GameManager: PlayerPathManager not found! (Did you assign it in the selected Mode Setup?)");
            return;
        }

        deckReadyForTurnCountdown = false;
        StopTurnCountdown();

        if (!IsPlayWithOopsMode)
        {
            pathManager.SetGameplayProfilesVisible(true, resetScale: true);
        }

        ApplyOfflineFriendProfilesIfNeeded();

        EnsureHomeSlotsResolvedIfMissing();

        // Spawn default pieces whenever gameplay initializes (not only the first time),
        // so re-opening the gameplay screen or reloading UI still repairs missing pieces.
        EnsurePiecesSpawnedIfMissing();
        ApplyDefaultPieceSpritesToExistingPieces();

        if (firstTime)
        {
            // Check if pieces are assigned
            int count = GetActivePlayerCount();
            for (int p = 1; p <= count; p++)
            {
                List<PlayerPiece> pieces = GetPiecesForPlayer(p);
                if (pieces == null || pieces.Count == 0)
                {
                    Debug.LogError($"GameManager: Player {p} pieces are not assigned! Please assign pieces in Inspector.");
                }
            }

            ValidatePieceAssignments();

            gameOver = false;
            winningPlayer = 0;

            // Only local/offline modes should force Player 1 to start.
            // Networked modes (PlayWithOops/OnlineFriends) are server-authoritative.
            if (!IsPlayWithOopsMode && !IsOnlineFriendsMode)
            {
                // Current player set karo (Player 1 start kare)
                currentPlayer = 1;
            }
            cardPicked = false;
            currentCardValue = 0;
            isSplitMode = false;
            remainingSteps = 0;
            selectedPieceForSplit = null;

            AssignHomePositionsFromStartPoints();
        }

        ShowCurrentPlayerPieces();
        if (IsPlayWithOopsMode)
        {
            UpdateTurnIndicatorUI();
            UpdateDeckTintForTurn();
            UpdatePiecesInteractivityForOopsTurn();
            StartCardPickReminderIfNeeded();
        }
        else
        {
            UpdatePiecesInteractivityForTurn();
            UpdateTurnIndicatorUI();
        }

        StopAllTurnPieceHighlights();
        ScheduleTurnHighlightsAfterDeckReady();

        EnsurePowerButtonReference();
        UpdatePowerButtonInteractivity();

        UpdateGameplaySettingsButtonInteractivity();

        TryStartBotTurnIfNeeded();

        gameplayInitialized = true;
    }

    void ScheduleTurnHighlightsAfterDeckReady()
    {
        if (delayedTurnHighlightCoroutine != null)
        {
            StopCoroutine(delayedTurnHighlightCoroutine);
            delayedTurnHighlightCoroutine = null;
        }
        delayedTurnHighlightCoroutine = StartCoroutine(ApplyTurnHighlightsAfterDeckReady());
    }

    private IEnumerator ApplyTurnHighlightsAfterDeckReady()
    {
        yield return null;

        float timeout = 6f;
        while (cardAnimationLock && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        delayedTurnHighlightCoroutine = null;
        UpdateTurnPieceHighlights();
    }

    void EnsurePowerButtonReference()
    {
        if (powerButton != null)
        {
            return;
        }

        GameObject go = GameObject.Find("PowerButton");
        if (go == null)
        {
            return;
        }

        powerButton = go.GetComponent<Button>();
    }

    void EnsureGameplaySettingsButtonReference()
    {
        if (gameplaySettingsButton != null)
        {
            return;
        }

        GameObject go = GameObject.Find("Game Play Setting");
        if (go == null)
        {
            go = GameObject.Find("settingbtn");
        }
        if (go == null)
        {
            if (!warnedMissingGameplaySettingsButton)
            {
                warnedMissingGameplaySettingsButton = true;
                Debug.LogWarning("GameManager: gameplaySettingsButton is missing. Assign it in Inspector (recommended) or ensure a GameObject named 'Game Play Setting' exists.");
            }
            return;
        }

        gameplaySettingsButton = go.GetComponent<Button>();
    }

    void UpdateGameplaySettingsButtonInteractivity()
    {
        if (gameplaySettingsButton == null)
        {
            EnsureGameplaySettingsButtonReference();
        }
        if (gameplaySettingsButton == null)
        {
            return;
        }

        if (gameplaySettingsButtonOpenLockActive || gameplaySettingsButtonExternalLockCount > 0)
        {
            gameplaySettingsButton.interactable = false;
            return;
        }

        bool canUse = !gameOver
            && modeSelected
            && !cardAnimationLock
            && !pausePopupOpen;

        gameplaySettingsButton.interactable = canUse;
    }

    void UpdatePowerButtonInteractivity()
    {
        if (powerButton == null)
        {
            return;
        }

        bool canUse = !gameOver
            && modeSelected
            && !IsBotPlayer(currentPlayer)
            && cardPicked
            && !cardAnimationLock
            && currentCardHandler != null
            && !(isSplitMode && selectedPieceForSplit != null)
            && !isSorryMode
            && !isCard11Mode
            && !isCard12Mode;

        powerButton.interactable = canUse;
    }

    void ValidatePieceAssignments()
    {
        HashSet<PlayerPiece> seen = new HashSet<PlayerPiece>();

        void ValidateList(List<PlayerPiece> list, int expectedPlayer)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                PlayerPiece p = list[i];
                if (p == null)
                {
                    Debug.LogWarning($"GameManager: Player {expectedPlayer} pieces list has NULL at index {i}");
                    continue;
                }

                if (!seen.Add(p))
                {
                    Debug.LogWarning($"GameManager: Duplicate piece reference detected: '{p.name}' appears multiple times across player lists.");
                }

                if (p.playerNumber != expectedPlayer)
                {
                    Debug.LogWarning($"GameManager: Piece '{p.name}' is in Player {expectedPlayer} list but has playerNumber={p.playerNumber}. Interactivity may be wrong.");
                }
            }
        }

        ValidateList(player1Pieces, 1);
        ValidateList(player2Pieces, 2);
        ValidateList(player3Pieces, 3);
        ValidateList(player4Pieces, 4);
    }

    /// <summary>
    /// Sab pieces hide karo (game start par)
    /// </summary>
    void HideAllPieces()
    {
        Debug.Log("Hiding all pieces at game start...");
        
        int hiddenCount = 0;
        foreach (var piece in GetAllActivePieces())
        {
            if (piece == null) continue;
            piece.HidePiece();
            hiddenCount++;
        }

        Debug.Log($"Total pieces hidden: {hiddenCount}");
        Debug.Log($"Active players: {GetActivePlayerCount()}");
    }

    void AssignHomePositionsFromStartPoints()
    {
        int count = GetActivePlayerCount();
        for (int p = 1; p <= count; p++)
        {
            List<PlayerPiece> pieces = GetPiecesForPlayer(p);
            Transform[] slots = GetHomeSlotsForPlayer(p);
            if (pieces == null || slots == null) continue;

            for (int i = 0; i < pieces.Count && i < slots.Length; i++)
            {
                if (pieces[i] == null) continue;
                if (slots[i] == null) continue;
                pieces[i].SetHomeTransform(slots[i]);
                pieces[i].ReturnToHome();
            }
        }
    }

    /// <summary>
    /// Card pick thayu pachhi call karo - card value set karo
    /// </summary>
    public void OnCardPicked(CardClickHandler cardHandler, int cardValue)
    {
        StopCardPickReminder();

        currentCardHandler = cardHandler;
        cardPicked = true;
        currentCardValue = cardValue;

        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;
        isCard10Mode = false;
        isCard11Mode = false;
        selectedPieceForCard11 = null;
        isCard12Mode = false;
        selectedPieceForCard12 = null;
        isSorryMode = false;
        selectedPieceForSorry = null;
        
        // Card power text store karo (Start pawn check mate)
        if (cardHandler != null)
        {
            currentCardPower1 = cardHandler.cardPower1 ?? "";
            currentCardPower2 = cardHandler.cardPower2 ?? "";
        }

        Debug.Log($"Player {currentPlayer} picked card - Power1: '{currentCardPower1}', Power2: '{currentCardPower2}', Value: {currentCardValue}");

        LogTurnMoveOptions(cardValue);

        List<PlayerPiece> statusPieces = GetPiecesForPlayer(currentPlayer);
        if (statusPieces != null)
        {
            foreach (var p in statusPieces)
            {
                if (p == null) continue;
                Debug.Log(p.GetStatusString());
            }
        }

        // SORRY! card detection (ExtractCardValue returns 0)
        if (cardValue == 0 && (currentCardPower1.Contains("SORRY") || currentCardPower2.Contains("SORRY")))
        {
            // Rulebook:
            // Option 1: START pawn -> replace opponent pawn (opponent goes START)
            // Option 2: if option1 not possible -> +4 forward
            bool option1Possible = IsSorryOption1Possible();

            if (option1Possible)
            {
                isSorryMode = true;
                selectedPieceForSorry = null;
                Debug.Log($"🔵 SORRY! picked - Rulebook Mode Enabled (START pawn replace opponent)");
            }
            else
            {
                // Fallback to +4 as a normal move
                isSorryMode = false;
                selectedPieceForSorry = null;
                currentCardValue = 4;
                cardValue = 4;
                Debug.Log($"🔵 SORRY! picked - Option1 not possible, using fallback +4 forward");
            }
        }

        // Card 7 check karo - split mode enable karo (agar split possible hoy to)
        if (cardValue == 7)
        {
            // Split possible check karo
            if (CheckIfSplitPossible())
            {
                isSplitMode = true;
                remainingSteps = 7; // Total 7 steps split kari shaksho
                selectedPieceForSplit = null;
                Debug.Log($"🔵 Card 7 picked - Split Mode Enabled! Total steps: {remainingSteps}");
            }
            else
            {
                // Split possible nahi → Direct 7 move (split mode nahi)
                isSplitMode = false;
                remainingSteps = 0;
                selectedPieceForSplit = null;
                Debug.Log($"🔵 Card 7 picked - Split NOT possible, using direct 7 move");
            }
            isCard10Mode = false; // Card 7 mate Card 10 mode disable
        }
        // Card 10 check karo - dual power mode enable karo (Move +10 OR -1 backward)
        else if (cardValue == 10)
        {
            isCard10Mode = true;
            isCard11Mode = false;
            selectedPieceForCard11 = null;

            isCard12Mode = false;
            selectedPieceForCard12 = null;
            isSplitMode = false;
            remainingSteps = 0;
            selectedPieceForSplit = null;
            Debug.Log($"🔵 Card 10 picked - Dual Power Mode Enabled! (Move +10 OR -1 backward)");
        }
        else if (cardValue == 11)
        {
            // Card 11 dual power only if swap is actually possible.
            // If swap not possible, treat it like a normal +11 move card (no destination highlight / no swap targets).
            bool hasCurrentOnBoard = false;
            List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
            foreach (var p in currentPieces)
            {
                if (p == null) continue;
                if (!p.IsAtHome())
                {
                    hasCurrentOnBoard = true;
                    break;
                }
            }

            bool hasOpponentOnBoard = false;
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                if (!opp.IsAtHome())
                {
                    hasOpponentOnBoard = true;
                    break;
                }
            }

            bool swapPossible = hasCurrentOnBoard && hasOpponentOnBoard;

            isCard11Mode = swapPossible;
            isCard10Mode = false;
            selectedPieceForCard11 = null;
            isSplitMode = false;
            remainingSteps = 0;
            selectedPieceForSplit = null;

            Debug.Log(swapPossible
                ? $"🔵 Card 11 picked - Dual Power Mode Enabled! (Move +11 OR Swap)"
                : $"🔵 Card 11 picked - Swap not possible, using normal +11 move");
        }
        else
        {
            isSplitMode = false;
            remainingSteps = 0;
            selectedPieceForSplit = null;
            isCard10Mode = false;
            isCard11Mode = false;
            selectedPieceForCard11 = null;
            isCard12Mode = false;
            selectedPieceForCard12 = null;
        }

        // Current player na pieces show karo (agar hidden che to)
        ShowCurrentPlayerPieces();
        UpdatePiecesInteractivityForTurn();

        // Card pick pachhi: current player na pieces ne card-power hisaabe interactable/filter karo.
        // Dual power cards ma: 2 mathi 1 pn possible hoy to piece clickable raheshe.
        ApplyInteractivityForCard(cardValue);

        UpdateTurnPieceHighlights();

        // Card 1-7 mate: Pieces check karo aur move possible pieces highlight karo
        // Card 7 Special: Agar split mode enable che to highlight nahi kariye (split mode ma pieces click kari shaksho)
        // Card 10 Special: Dual power mode - pieces clickable raheshe, destination blocks highlight thase
        // NOTE: Card 8, 10, 11 mate blocking check OnPieceClicked() ma thase (highlight nahi, pan blocking check thase)
        // Also, negative cards like -4 should still go through normal move-check + auto-skip.
        if ((cardValue >= 1 && cardValue <= 7) || cardValue == 12 || cardValue < 0 || (cardValue == 11 && !isCard11Mode))
        {
            // Card 7 with split mode: Highlight nahi kariye (split mode ma pieces click kari shaksho)
            if (cardValue == 7 && isSplitMode)
            {
                Debug.Log($"🔵 Card 7 Split Mode: Pieces will be clickable for split (no highlight needed)");
                // Split mode ma pieces clickable raheshe (highlight nahi kariye)
                List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
                foreach (PlayerPiece piece in currentPieces)
                {
                    if (piece == null) continue; // Null check
                    piece.SetClickable(true); // Clickable banao (split mode mate)
                }
            }
            else
            {
                // Normal cards (1-6) ya Card 7 without split mode
                CheckAndHighlightPossibleMoves(cardValue);
            }
        }
        // Card 10: Dual power mode - pieces clickable raheshe, destination blocks highlight thase
        else if (cardValue == 10 && isCard10Mode)
        {
            Debug.Log($"🔵 Card 10 Dual Power Mode: Pieces will be clickable, destination blocks will be highlighted");
            // Card pick step ma already ApplyInteractivityForCard() chalavi didhu che.
        }
        else if (cardValue == 11 && isCard11Mode)
        {
            Debug.Log($"🔵 Card 11 Dual Power Mode: Pieces will be clickable (+11 destination or swap)");
            // Card pick step ma already ApplyInteractivityForCard() chalavi didhu che.
        }
        if (isSorryMode)
        {
            Debug.Log($"🔵 SORRY! Rulebook Mode: Select your START pawn, then select opponent on-board pawn to replace it");
            List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
            foreach (PlayerPiece piece in currentPieces)
            {
                if (piece == null) continue;
                piece.SetClickable(true);
            }

            if (!IsAnyActionPossibleForSorry())
            {
                Debug.LogWarning($"⚠️ No attack possible for Player {currentPlayer} with SORRY!. Turn will be skipped.");
                StartCoroutine(SkipTurnAfterDelay());
            }
        }

        // Rule: Jo koi pan move possible nathi to turn SKIP (special cards mate)
        if (cardValue == 8 || cardValue == 10 || cardValue == 11)
        {
            if (!IsAnyActionPossibleForSpecialCard(cardValue))
            {
                Debug.LogWarning($"⚠️ No move possible for Player {currentPlayer} with special card value {cardValue}. Turn will be skipped.");

                List<PlayerPiece> skipStatusPieces = GetPiecesForPlayer(currentPlayer);
                if (skipStatusPieces != null)
                {
                    foreach (var p in skipStatusPieces)
                    {
                        if (p == null) continue;
                        Debug.Log(p.GetStatusString());
                    }
                }

                StartCoroutine(SkipTurnAfterDelay());
            }
        }

        // Do-or-die rule for dual-power cards:
        // Auto-execute ONLY when exactly one legal action exists across all pieces/options.
        if (cardValue == 10 && isCard10Mode)
        {
            TryAutoExecuteForcedCard10Move();
        }

        UpdatePowerButtonInteractivity();
    }

    void LogTurnMoveOptions(int cardValue)
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null || pieces.Count == 0)
        {
            return;
        }

        string playerColor = currentPlayer == 1 ? "#00C853" : (currentPlayer == 2 ? "#FF6D00" : (currentPlayer == 3 ? "#2962FF" : "#D500F9"));

        StringBuilder sb = new StringBuilder(256);
        sb.Append($"<color={playerColor}>TURN P{currentPlayer}</color> card=<b>{cardValue}</b> ");

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;

            p.SyncCurrentPathIndexFromTransform();
            string fromLabel = p.GetZoneLabel();
            int fromIndex = p.GetCurrentPathIndex();

            bool ok = TryGetDestinationForMove(p, cardValue, out int destIndex, out Transform dest, out string reason);
            if (ok && dest != null)
            {
                sb.Append($"| P{p.playerNumber}-#{p.pieceNumber} {fromLabel}[{fromIndex}] -> <color=#00E5FF>{dest.name}</color>({destIndex}) ");
            }
            else
            {
                sb.Append($"| P{p.playerNumber}-#{p.pieceNumber} {fromLabel}[{fromIndex}] -> <color=#FF1744>NO</color>({reason}) ");
            }
        }

        if (cardValue == 10)
        {
            int possibleForward = 0;
            int possibleBack = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                if (TryGetDestinationForMove(p, 10, out _, out _, out _)) possibleForward++;
                if (!p.IsAtHome() && TryGetDestinationForMove(p, -1, out _, out _, out _)) possibleBack++;
            }
            sb.Append($"| alt(-1)={possibleBack}, alt(+10)={possibleForward} ");
        }

        Debug.Log(sb.ToString());
    }

    [ContextMenu("Debug/Audit: Print Card Options (Current Player)")]
    void DebugAuditCardOptionsForCurrentPlayer()
    {
        if (pathManager == null)
        {
            Debug.LogWarning("AUDIT: pathManager is null");
            return;
        }

        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null || pieces.Count == 0)
        {
            Debug.LogWarning($"AUDIT: No pieces assigned for currentPlayer={currentPlayer}");
            return;
        }

        int[] auditMoves = new[] { -4, -1, 1, 2, 3, 4, 5, 7, 8, 10, 11, 12 };

        bool splitPossible = CheckIfSplitPossible();

        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine($"AUDIT: currentPlayer={currentPlayer} cardPicked={cardPicked} currentCardValue={currentCardValue} modes: split={isSplitMode}, c10={isCard10Mode}, c11={isCard11Mode}, c12={isCard12Mode}, sorry={isSorryMode} | splitPossible={splitPossible}");

        int oppOnBoard = 0;
        int oppOnOuter = 0;
        foreach (var opp in GetOpponentPieces(currentPlayer))
        {
            if (opp == null) continue;
            opp.SyncCurrentPathIndexFromTransform();
            if (opp.IsAtHome()) continue;
            if (opp.IsOnHomePath()) continue;
            if (opp.IsFinishedInHomePath()) continue;
            oppOnBoard++;
            if (opp.IsOnOuterTrack()) oppOnOuter++;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;

            p.SyncCurrentPathIndexFromTransform();
            string label = p.GetZoneLabel();
            int fromIndex = p.GetCurrentPathIndex();

            sb.AppendLine($"AUDIT: P{p.playerNumber}-#{p.pieceNumber} from {label}[{fromIndex}] name='{p.name}'");

            for (int m = 0; m < auditMoves.Length; m++)
            {
                int steps = auditMoves[m];
                bool ok = TryGetDestinationForMove(p, steps, out int destIndex, out Transform dest, out string reason);
                if (ok && dest != null)
                {
                    sb.AppendLine($"  move {steps,3}: OK -> {dest.name}({destIndex})");
                }
                else
                {
                    sb.AppendLine($"  move {steps,3}: NO -> {reason}");
                }
            }

            if (!p.IsAtHome())
            {
                bool c10f = TryGetDestinationForMove(p, 10, out int d10, out Transform t10, out string r10);
                bool c10b = TryGetDestinationForMove(p, -1, out int dM1, out Transform tM1, out string rM1);
                sb.AppendLine($"  card10: +10={(c10f ? (t10 != null ? $"{t10.name}({d10})" : "OK") : $"NO({r10})")}, -1={(c10b ? (tM1 != null ? $"{tM1.name}({dM1})" : "OK") : $"NO({rM1})")}");
            }
            else
            {
                bool c10f = TryGetDestinationForMove(p, 10, out int d10, out Transform t10, out string r10);
                sb.AppendLine($"  card10: +10={(c10f ? (t10 != null ? $"{t10.name}({d10})" : "OK") : $"NO({r10})")}, -1=NO(backward from START) ");
            }

            bool can11 = TryGetDestinationForMove(p, 11, out int d11, out Transform t11, out string r11);
            bool canSwap11 = p.IsOnOuterTrack() && oppOnOuter > 0;
            sb.AppendLine($"  card11: +11={(can11 ? (t11 != null ? $"{t11.name}({d11})" : "OK") : $"NO({r11})")}, swapPossible={canSwap11}");

            bool can12 = TryGetDestinationForMove(p, 12, out int d12, out Transform t12, out string r12);
            bool canCapture12 = !p.IsAtHome() && oppOnBoard > 0;
            sb.AppendLine($"  card12: +12={(can12 ? (t12 != null ? $"{t12.name}({d12})" : "OK") : $"NO({r12})")}, capturePossible={canCapture12}");
        }

        bool hasStartPawn = false;
        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            p.SyncCurrentPathIndexFromTransform();
            if (p.IsAtHome())
            {
                hasStartPawn = true;
                break;
            }
        }

        bool sorryOption1 = hasStartPawn && oppOnBoard > 0;
        bool sorryOption2Plus4 = false;
        if (!sorryOption1)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                PlayerPiece p = pieces[i];
                if (p == null) continue;
                p.SyncCurrentPathIndexFromTransform();
                if (p.IsAtHome()) continue;
                if (p.IsOnHomePath()) continue;
                if (p.IsFinishedInHomePath()) continue;

                if (TryGetDestinationForMove(p, 4, out _, out _, out _))
                {
                    sorryOption2Plus4 = true;
                    break;
                }
            }
        }

        sb.AppendLine($"AUDIT: SORRY option1Possible={sorryOption1} (hasStartPawn={hasStartPawn}, oppOnBoard={oppOnBoard}) | option2(+4)Possible={sorryOption2Plus4}");

        Debug.Log(sb.ToString());
    }

    void TryAutoExecuteForcedCard10Move()
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
        if (currentPieces == null) return;

        PlayerPiece forcedPiece = null;
        int forcedSteps = 0;
        int totalActions = 0;

        foreach (var piece in currentPieces)
        {
            if (piece == null) continue;

            if (CheckIfMovePossible(piece, 10))
            {
                totalActions++;
                forcedPiece = piece;
                forcedSteps = 10;
                if (totalActions > 1) break;
            }

            if (!piece.IsAtHome() && CheckIfMovePossible(piece, -1))
            {
                totalActions++;
                forcedPiece = piece;
                forcedSteps = -1;
                if (totalActions > 1) break;
            }
        }

        if (totalActions == 1 && forcedPiece != null)
        {
            PlayerPiece.ClearAllPiecesHighlights();
            Debug.Log($"🔵 Card 10 Do-or-die: Only one legal action found -> auto move Piece '{forcedPiece.name}' steps {forcedSteps}");
            forcedPiece.MovePieceDirectly(forcedSteps);
        }
    }

    bool IsSorryOption1Possible()
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);

        bool hasStartPawn = false;
        foreach (var p in currentPieces)
        {
            if (p == null) continue;
            p.SyncCurrentPathIndexFromTransform();
            if (p.IsAtHome())
            {
                hasStartPawn = true;
                break;
            }
        }
        if (!hasStartPawn)
        {
            Debug.Log($"🔵 SORRY Option1 check: Player {currentPlayer} hasStartPawn=false -> Option1 not possible (fallback +4)");
            return false;
        }

        int validOppTargets = 0;
        foreach (var opp in GetOpponentPieces(currentPlayer))
        {
            if (opp == null) continue;
            if (opp.IsAtHome()) continue;
            opp.SyncCurrentPathIndexFromTransform();
            if (opp.IsOnHomePath()) continue;
            if (opp.IsFinishedInHomePath()) continue;
            validOppTargets++;
        }

        bool result = validOppTargets > 0;
        Debug.Log($"🔵 SORRY Option1 check: Player {currentPlayer} hasStartPawn=true, validOppTargets={validOppTargets} -> Option1Possible={result}");
        return result;
    }

    bool IsAnyActionPossibleForSorry()
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);

        bool option1Possible = false;
        bool hasStartPawn = false;
        foreach (var p in currentPieces)
        {
            if (p == null) continue;
            p.SyncCurrentPathIndexFromTransform();
            if (p.IsAtHome())
            {
                hasStartPawn = true;
                break;
            }
        }

        if (hasStartPawn)
        {
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                if (opp.IsAtHome())
                {
                    continue;
                }

                opp.SyncCurrentPathIndexFromTransform();
                if (opp.IsOnHomePath() || opp.IsFinishedInHomePath())
                {
                    continue;
                }

                if (!opp.IsAtHome())
                {
                    option1Possible = true;
                    break;
                }
            }
        }

        if (option1Possible) return true;

        // Option2 (+4) only if option1 not possible
        foreach (var p in currentPieces)
        {
            if (p == null) continue;
            if (CheckIfMovePossible(p, 4)) return true;
        }

        return false;
    }

    bool IsAnyActionPossibleForSpecialCard(int cardValue)
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);

        if (cardValue == 8)
        {
            foreach (var piece in currentPieces)
            {
                if (piece == null) continue;
                if (CheckIfMovePossible(piece, 8)) return true;
            }
            return false;
        }

        if (cardValue == 10)
        {
            foreach (var piece in currentPieces)
            {
                if (piece == null) continue;
                if (CheckIfMovePossible(piece, 10)) return true;
                if (!piece.IsAtHome() && CheckIfMovePossible(piece, -1)) return true;
            }
            return false;
        }

        if (cardValue == 11)
        {
            bool hasSwapTarget = false;
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.SyncCurrentPathIndexFromTransform();
                if (opp.IsOnOuterTrack())
                {
                    hasSwapTarget = true;
                    break;
                }
            }

            foreach (var piece in currentPieces)
            {
                if (piece == null) continue;
                if (CheckIfMovePossible(piece, 11)) return true;
                piece.SyncCurrentPathIndexFromTransform();
                if (hasSwapTarget && piece.IsOnOuterTrack()) return true;
            }
            return false;
        }

        return true;
    }

    public bool IsCard11Mode()
    {
        return isCard11Mode;
    }

    public PlayerPiece GetSelectedPieceForCard11()
    {
        return selectedPieceForCard11;
    }

    public void SetSelectedPieceForCard11(PlayerPiece piece)
    {
        selectedPieceForCard11 = piece;
    }

    public void CompleteCard11Mode()
    {
        Debug.Log($"🔵 Card 11 Mode Complete!");

        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        isCard11Mode = false;
        selectedPieceForCard11 = null;

        isCard12Mode = false;
        selectedPieceForCard12 = null;

        isSorryMode = false;
        selectedPieceForSorry = null;

        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        StopAllTurnPieceHighlights();

        if (extraTurnPending)
        {
            extraTurnPending = false;
            RefreshTurnForCurrentPlayer();
        }
        else
        {
            SwitchTurn();
        }
    }

    public bool IsCard12Mode()
    {
        return isCard12Mode;
    }

    public bool IsSorryMode()
    {
        return isSorryMode;
    }

    public PlayerPiece GetSelectedPieceForSorry()
    {
        return selectedPieceForSorry;
    }

    public void SetSelectedPieceForSorry(PlayerPiece piece)
    {
        selectedPieceForSorry = piece;
    }

    public void CompleteSorryMode()
    {
        Debug.Log($"🔵 SORRY! Mode Complete!");

        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        isSorryMode = false;
        selectedPieceForSorry = null;

        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        StopAllTurnPieceHighlights();

        if (extraTurnPending)
        {
            extraTurnPending = false;
            RefreshTurnForCurrentPlayer();
        }
        else
        {
            SwitchTurn();
        }
    }

    public PlayerPiece GetSelectedPieceForCard12()
    {
        return selectedPieceForCard12;
    }

    public void SetSelectedPieceForCard12(PlayerPiece piece)
    {
        selectedPieceForCard12 = piece;
    }

    public void CompleteCard12Mode()
    {
        Debug.Log($"🔵 Card 12 Mode Complete!");

        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        isCard12Mode = false;
        selectedPieceForCard12 = null;

        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        StopAllTurnPieceHighlights();

        if (extraTurnPending)
        {
            extraTurnPending = false;
            RefreshTurnForCurrentPlayer();
        }
        else
        {
            SwitchTurn();
        }
    }

    /// <summary>
    /// Card pick pachhi pieces check karo aur move possible pieces highlight karo (Card 1-7)
    /// Rule: Same player na piece destination par hoy to move BLOCKED
    /// Rule: Jo koi pan move possible nathi to turn SKIP
    /// Card 7 Special: Agar split possible nahi to direct 7 move check karo
    /// </summary>
    void CheckAndHighlightPossibleMoves(int cardValue)
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
        bool anyPieceCanMove = false;

        // Card 7 Special Rule: Agar split possible nahi to direct 7 move check karo
        if (cardValue == 7 && !isSplitMode)
        {
            // Split possible nahi → Direct 7 move check karo
            Debug.Log($"🔵 Card 7: Split NOT possible, checking direct 7 move");
            foreach (PlayerPiece piece in currentPieces)
            {
                if (piece == null) continue; // Null check
                
                bool canMove = CheckIfMovePossible(piece, 7);
                
                if (canMove)
                {
                    // Highlight logic removed - no yellow color or scale 1.1
                    piece.SetClickable(true);
                    anyPieceCanMove = true;
                    Debug.Log($"✅ Piece {piece.pieceNumber} can move 7 steps (direct move)");
                }
                else
                {
                    // Highlight logic removed
                    Debug.Log($"❌ Piece {piece.pieceNumber} cannot move 7 steps (blocked or invalid)");
                }
            }
        }
        else
        {
            // Normal cards (1-6) ya Card 7 with split mode
            Debug.Log($"🔍 CheckAndHighlightPossibleMoves: Total pieces in list = {currentPieces.Count}");
            for (int i = 0; i < currentPieces.Count; i++)
            {
                PlayerPiece piece = currentPieces[i];
                if (piece == null)
                {
                    Debug.LogWarning($"⚠️ Null piece found at index {i} in currentPieces list!");
                    continue; // Null check
                }
                
                Debug.Log($"🔍 Checking Piece {piece.pieceNumber} (index {i}) for card value {cardValue}...");
                bool canMove = CheckIfMovePossible(piece, cardValue);
                
                if (canMove)
                {
                    // Highlight logic removed - no yellow color or scale 1.1
                    piece.SetClickable(true); // Clickable banao
                    anyPieceCanMove = true;
                    Debug.Log($"✅ Piece {piece.pieceNumber} can move {cardValue} steps");
                }
                else
                {
                    // Highlight logic removed
                    Debug.Log($"❌ Piece {piece.pieceNumber} cannot move {cardValue} steps (blocked or invalid)");
                }
            }
        }

        // Rule: Jo koi pan move possible nathi to turn SKIP
        if (!anyPieceCanMove)
        {
           // Debug.LogWarning($"⚠️ No move possible for Player {currentPlayer} with card value {cardValue}. Turn will be skipped.");
            StartCoroutine(SkipTurnAfterDelay());
        }
    }

    /// <summary>
    /// Check karo ki piece par move possible che ke nahi
    /// Rule: Same player na piece destination par hoy to move BLOCKED
    /// </summary>
    public bool CheckIfMovePossible(PlayerPiece piece, int cardValue)
    {
        if (piece == null || pathManager == null)
        {
            //Debug.LogWarning($"⚠️ CheckIfMovePossible: Piece or pathManager is null for Piece {piece?.pieceNumber}");
            return false;
        }

        piece.SyncCurrentPathIndexFromTransform();

        if (piece.IsFinishedInHomePath())
        {
            return false;
        }

        // Backward card check (Card 4)
        if (cardValue < 0 && piece.IsAtHome())
        {
           // Debug.Log($"⚠️ CheckIfMovePossible: Piece {piece.pieceNumber} at home, backward card {cardValue} not allowed");
            return false; // Backward card home ma use nahi thay
        }

        // Destination calculate karo
        List<Transform> completePath = pathManager.GetCompletePlayerPath(piece.playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
           // Debug.LogWarning($"⚠️ CheckIfMovePossible: Complete path is null or empty for Piece {piece.pieceNumber}");
            return false;
        }

        // Route path get karo (wrap-around logic mate - sirf route path na last element use karo)
        List<Transform> routePath = pathManager.GetPlayerRoutePath(piece.playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1; // Route path na last index (home path exclude)
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2); // NEW: second-last route tile is entry gate to home

        int currentIndex = piece.GetCurrentPathIndex();
        int destIndex;
        if (currentIndex == -1)
        {
            // Piece home/start ma che
            destIndex = cardValue - 1; // steps = 5 to index 4
           // Debug.Log($"🔍 CheckIfMovePossible: Piece {piece.pieceNumber} at home, destination index = {destIndex} (cardValue {cardValue} - 1)");
        }
        else
        {
            // Piece already path par che
            if (cardValue < 0)
            {
                // CASE: Backward move (negative steps)
                // NEW RULE:
                // - If piece is in home path, exit home path to routeEntryIndex (second-last route tile)
                //   in 1 step, skipping routePathLastIndex entirely.
                // - Wrap-around uses routeEntryIndex (not routePathLastIndex).
                if (currentIndex >= routePathLength)
                {
                    int homeStartIndex = routePathLength;
                    int homeOffset = currentIndex - homeStartIndex;
                    int absSteps = -cardValue;

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
                    destIndex = currentIndex + cardValue;
                    if (destIndex < 0)
                    {
                        destIndex = routePathLastIndex + destIndex + 1;
                    }
                }
               // Debug.Log($"🔵 CheckIfMovePossible: Piece {piece.pieceNumber} at index {currentIndex}, backward move {cardValue} => destination index {destIndex}");
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
                    if (cardValue <= stepsToEntry)
                    {
                        destIndex = currentIndex + cardValue;
                    }
                    else
                    {
                        int remainingAfterEntryToHome = cardValue - stepsToEntry - 1; // 1 step = entryIndex -> home[0]
                        destIndex = routePathLength + remainingAfterEntryToHome; // home starts at index routePathLength
                    }
                }
                else if (currentIndex == routePathLastIndex)
                {
                    // If a piece is on the last route tile, forward movement wraps back to the start of the route.
                    // This prevents incorrect jumps into the home path from the last route index.
                    destIndex = (cardValue - 1);
                }
                else
                {
                    // Already beyond entry (last route index or home path) -> normal forward
                    destIndex = currentIndex + cardValue;
                }

               // Debug.Log($"🔍 CheckIfMovePossible: Piece {piece.pieceNumber} at index {currentIndex}, FORWARD move {cardValue} steps = destination index {destIndex} (routeEntryIndex={routeEntryIndex})");
            }
        }

        // Boundary check (ensure destIndex is within valid range)
        if (destIndex < 0)
        {
            // This should rarely happen with new logic, but safety check
            Debug.LogWarning($"⚠️ CheckIfMovePossible: Piece {piece.pieceNumber} destination index {destIndex} is negative, clamping to 0");
            destIndex = 0;
        }
        else if (destIndex >= completePath.Count)
        {
            // HOME exact-count rule: If a forward move would overshoot beyond the last home slot,
            // the move is invalid (no clamping).
            Debug.LogWarning($"⚠️ CheckIfMovePossible: Piece {piece.pieceNumber} destination index {destIndex} out of bounds (path count: {completePath.Count}). Move invalid (exact-count HOME rule).");
            return false;
        }

        // Rule: Same player na piece destination par hoy to move BLOCKED
        if (IsSamePlayerPieceAtPosition(destIndex, piece.playerNumber, piece))
        {
            Debug.LogWarning($"⚠️ CheckIfMovePossible: Piece {piece.pieceNumber} move BLOCKED - same player piece at destination index {destIndex}");
            return false; // BLOCKED - same player na piece destination par che
        }

       // Debug.Log($"✅ CheckIfMovePossible: Piece {piece.pieceNumber} can move to destination index {destIndex}");
        return true; // Move possible ✅
    }

    public bool TryGetDestinationForMove(PlayerPiece piece, int cardValue, out int destIndex, out Transform destPosition, out string reason)
    {
        destIndex = -1;
        destPosition = null;
        reason = "";

        if (piece == null || pathManager == null)
        {
            reason = "piece or pathManager null";
            return false;
        }

        piece.SyncCurrentPathIndexFromTransform();

        if (piece.IsFinishedInHomePath())
        {
            reason = "piece already finished in home (last slot)";
            return false;
        }

        if (cardValue < 0 && piece.IsAtHome())
        {
            reason = "backward move not allowed from START";
            return false;
        }

        List<Transform> completePath = pathManager.GetCompletePlayerPath(piece.playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            reason = "completePath missing/empty";
            return false;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(piece.playerNumber);
        int routePathLength = routePath != null ? routePath.Count : completePath.Count;
        int routePathLastIndex = routePathLength - 1;
        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);

        int currentIndex = piece.GetCurrentPathIndex();

        // Defensive: if the piece could not resolve its position to a known path anchor,
        // it may report a sentinel like -999. In that case, do not attempt wrap-around math.
        if (currentIndex < -1)
        {
            reason = $"invalid currentPathIndex ({currentIndex})";
            return false;
        }

        if (currentIndex == -1)
        {
            destIndex = cardValue - 1;
        }
        else
        {
            if (cardValue < 0)
            {
                if (currentIndex >= routePathLength)
                {
                    int homeStartIndex = routePathLength;
                    int homeOffset = currentIndex - homeStartIndex;
                    int absSteps = -cardValue;

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
                    destIndex = currentIndex + cardValue;
                    if (destIndex < 0)
                    {
                        destIndex = routePathLastIndex + destIndex + 1;
                    }
                }
            }
            else
            {
                if (currentIndex >= 0 && currentIndex < routePathLength && currentIndex <= routeEntryIndex)
                {
                    int stepsToEntry = routeEntryIndex - currentIndex;
                    if (cardValue <= stepsToEntry)
                    {
                        destIndex = currentIndex + cardValue;
                    }
                    else
                    {
                        int remainingAfterEntryToHome = cardValue - stepsToEntry - 1;
                        destIndex = routePathLength + remainingAfterEntryToHome;
                    }
                }
                else if (currentIndex == routePathLastIndex)
                {
                    // Wrap forward moves from the last route tile back to the start of the route.
                    destIndex = (cardValue - 1);
                }
                else
                {
                    destIndex = currentIndex + cardValue;
                }
            }
        }

        if (destIndex < 0)
        {
            reason = $"destIndex negative ({destIndex})";
            return false;
        }

        if (destIndex >= completePath.Count)
        {
            reason = $"overshoot beyond last home slot (destIndex={destIndex}, pathCount={completePath.Count})";
            return false;
        }

        if (IsSamePlayerPieceAtPosition(destIndex, piece.playerNumber, piece))
        {
            reason = $"blocked by same player piece at destination index {destIndex}";
            return false;
        }

        destPosition = completePath[destIndex];
        if (destPosition == null)
        {
            reason = $"destination Transform is null at index {destIndex}";
            return false;
        }

        return true;
    }

    public void DebugPrintMoveDestinations(PlayerPiece piece, int cardValue)
    {
        if (piece == null)
        {
            Debug.LogWarning("DebugPrintMoveDestinations: piece is null");
            return;
        }

        piece.SyncCurrentPathIndexFromTransform();

        bool ok = TryGetDestinationForMove(piece, cardValue, out int destIndex, out Transform dest, out string reason);
        string fromLabel = piece.GetZoneLabel();
        int fromIndex = piece.GetCurrentPathIndex();

        if (ok)
        {
           // Debug.Log($"🧭 MOVE OPTIONS: P{piece.playerNumber}-Piece{piece.pieceNumber} from {fromLabel} idx={fromIndex} with card {cardValue} => destIndex={destIndex}, destName='{dest.name}'");
        }
        else
        {
           // Debug.LogWarning($"🧭 MOVE OPTIONS: P{piece.playerNumber}-Piece{piece.pieceNumber} from {fromLabel} idx={fromIndex} with card {cardValue} => NO MOVE ({reason})");
        }
    }

    /// <summary>
    /// Check karo ki destination position par same player na piece che ke nahi
    /// Rule: Ek normal square par tamara color no fakt 1 pawn rahi shake
    /// </summary>
    bool IsSamePlayerPieceAtPosition(int positionIndex, int playerNumber, PlayerPiece currentPiece)
    {
        // Null check - pathManager null nahi hoy to j check karo
        if (pathManager == null)
        {
            Debug.LogError("IsSamePlayerPieceAtPosition: pathManager is null!");
            return false;
        }
        
        List<PlayerPiece> allPieces = GetPiecesForPlayer(playerNumber);
        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);

        if (completePath == null || positionIndex < 0 || positionIndex >= completePath.Count)
            return false;

        if (positionIndex == completePath.Count - 1)
            return false;

        Transform targetPosition = completePath[positionIndex];
        
        // Null check - targetPosition null nahi hoy to j check karo
        if (targetPosition == null)
        {
            Debug.LogError($"IsSamePlayerPieceAtPosition: targetPosition is null at index {positionIndex}!");
            return false;
        }

        Transform normalizedTarget = NormalizeToPathAnchor(targetPosition, completePath);

        // Sabhi pieces check karo
        if (allPieces == null)
        {
            return false;
        }

        foreach (PlayerPiece piece in allPieces)
        {
            // Null check - piece null nahi hoy to j check karo
            if (piece == null)
                continue;

            // Safety: List assignment wrong hoy to pan, sirf same playerNumber na pieces ne j block karo
            if (piece.playerNumber != playerNumber)
                continue;
                
            // Current piece ne skip karo (same piece)
            if (piece == currentPiece)
                continue;

            piece.SyncCurrentPathIndexFromTransform();

            // START/Home ma betha pawns normal squares ne block na kare.
            if (piece.IsAtHome())
                continue;

            Transform normalizedPieceAnchor = NormalizeToPathAnchor(piece.GetCurrentPositionTransform() != null
                ? piece.GetCurrentPositionTransform()
                : piece.transform.parent, completePath);

            if (normalizedTarget != null && normalizedPieceAnchor != null && normalizedTarget == normalizedPieceAnchor)
            {
                Debug.Log($"🚫 Blocked: Piece {piece.pieceNumber} already at target position (normalized anchor match)");
                return true;
            }

            // Robust transform checks (some boards use nested holders under a tile).
            // If the piece is parented anywhere under the target tile (or vice-versa), treat as occupied.
            Transform pieceRoot = piece.transform;
            if (pieceRoot != null)
            {
                if (pieceRoot == targetPosition || pieceRoot.IsChildOf(targetPosition))
                {
                    //Debug.Log($"🚫 Blocked: Piece {piece.pieceNumber} is under target position (IsChildOf match)");
                    return true;
                }
            }

            // Piece na current position check karo (real-time check)
            int pieceCurrentIndex = piece.GetCurrentPathIndex();
            if (pieceCurrentIndex == positionIndex)
            {
                // Same position par piece che - BLOCKED!
               // Debug.Log($"🚫 Blocked: Piece {piece.pieceNumber} already at position {positionIndex} (index match)");
                return true;
            }

            // Piece na current transform check karo (real-time check - transform.parent use karo)
            Transform pieceTransform = piece.GetCurrentPositionTransform();
            if (pieceTransform != null && (pieceTransform == targetPosition || pieceTransform.IsChildOf(targetPosition)))
            {
                // Same position par piece che - BLOCKED!
               // Debug.Log($"🚫 Blocked: Piece {piece.pieceNumber} already at target position (transform match)");
                return true;
            }
            
            // Additional check: Piece na transform.parent check karo (real-time position)
            // Agar piece move thayu che pan currentPathIndex update nahi thayu to parent check karo
            if (piece.transform.parent != null && piece.transform.parent == targetPosition)
            {
                // Same position par piece che - BLOCKED!
               // Debug.Log($"🚫 Blocked: Piece {piece.pieceNumber} already at target position (parent match)");
                return true;
            }

            // Final fallback: world-position epsilon (covers rare hierarchy setups)
            const float occupiedEpsilon = 0.05f;
            if (pieceRoot != null && Vector3.Distance(pieceRoot.position, targetPosition.position) <= occupiedEpsilon)
            {
               // Debug.Log($"🚫 Blocked: Piece {piece.pieceNumber} already at target position (world distance match)");
                return true;
            }
        }

        return false; // No blocking
    }

    Transform NormalizeToPathAnchor(Transform t, List<Transform> completePath)
    {
        if (t == null || completePath == null)
        {
            return null;
        }

        Transform cur = t;
        while (cur != null)
        {
            if (completePath.Contains(cur))
            {
                return cur;
            }

            cur = cur.parent;
        }

        return null;
    }

    /// <summary>
    /// Card 7 mate split possible check karo
    /// Rule: 7 steps ne 2 pieces ma divide kari shaksho, pan banne move possible hova joiye
    /// </summary>
    bool CheckIfSplitPossible()
    {
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);

        // All possible split combinations check karo (e.g., 1+6, 2+5, 3+4, 4+3, 5+2, 6+1)
        for (int steps1 = 1; steps1 <= 6; steps1++)
        {
            int steps2 = 7 - steps1;

            // Sabhi pieces check karo (2 pieces select kari ne check karo)
            for (int i = 0; i < currentPieces.Count; i++)
            {
                for (int j = i + 1; j < currentPieces.Count; j++)
                {
                    PlayerPiece piece1 = currentPieces[i];
                    PlayerPiece piece2 = currentPieces[j];

                    // Null check
                    if (piece1 == null || piece2 == null)
                        continue;

                    // Check: Piece1 par steps1 possible che?
                    bool piece1OK = CheckIfMovePossible(piece1, steps1);
                    
                    // Check: Piece2 par steps2 possible che?
                    bool piece2OK = CheckIfMovePossible(piece2, steps2);

                    if (piece1OK && piece2OK)
                    {
                       // Debug.Log($"✅ Split possible: Piece {piece1.pieceNumber} = {steps1} steps, Piece {piece2.pieceNumber} = {steps2} steps");
                        return true; // Split possible ✅
                    }
                }
            }
        }

        //Debug.Log($"❌ Split NOT possible - no valid combination found");
        return false; // Split possible nahi
    }

    /// <summary>
    /// Sabhi pieces na highlights clear karo
    /// NOTE: Highlight logic removed - no yellow color or scale 1.1
    /// </summary>
    void ClearAllPieceHighlights()
    {
        // Highlight logic removed - no need to clear highlights
        // Method kept for compatibility but does nothing
    }

    /// <summary>
    /// Turn skip karo (delay pachhi - card return sathe)
    /// </summary>
    IEnumerator SkipTurnAfterDelay()
    {
        // Highlights clear karo
        ClearAllPieceHighlights();
        PlayerPiece.ClearAllPiecesHighlights();
        
        yield return new WaitForSeconds(1f); // Thoduk delay (user ne samaj aavi jay)

        //Debug.Log($"⏭️ Skipping turn for Player {currentPlayer} - No move possible");

        // Card return karo
        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        // Reset card state
        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        // Reset modes
        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;

        isCard10Mode = false;

        isCard11Mode = false;
        selectedPieceForCard11 = null;

        isCard12Mode = false;
        selectedPieceForCard12 = null;

        extraTurnPending = false;

        StopAllTurnPieceHighlights();

        // Turn switch karo
        SwitchTurn();
    }


    /// <summary>
    /// Current player na pieces show karo
    /// NOTE: Pieces already visible che start position par, so just ensure they're active
    /// </summary>
    void ShowCurrentPlayerPieces()
    {
        if (pausePopupOpen) return;
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);

        Debug.Log($"Ensuring pieces are visible for Player {currentPlayer}. Total pieces in list: {currentPieces.Count}");

        int shownCount = 0;
        foreach (var piece in currentPieces)
        {
            if (piece != null)
            {
                // Piece already visible che, just ensure it's active
                piece.ShowPiece();
                piece.SetClickable(true);
                shownCount++;
                Debug.Log($"Player {currentPlayer} piece active: {piece.name}");
            }
            else
            {
                Debug.LogWarning($"Player {currentPlayer} has null piece in list!");
            }
        }

        Debug.Log($"Total pieces active: {shownCount}");
    }

    void UpdatePiecesInteractivityForTurn()
    {
        if (pausePopupOpen) return;

        if (IsOnlineFriendsMode)
        {
            int local = Mathf.Clamp(localPlayerNumber, 1, 4);
            if (currentPlayer != local)
            {
                foreach (var p in GetAllActivePieces())
                {
                    if (p == null) continue;
                    p.SetClickable(false);
                }
                return;
            }
        }
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);
        if (currentPieces != null)
        {
            foreach (var piece in currentPieces)
            {
                if (piece == null) continue;
                piece.ShowPiece();
                piece.SetClickable(true);
            }
        }

        foreach (var opp in GetOpponentPieces(currentPlayer))
        {
            if (opp == null) continue;
            opp.ShowPiece();
            opp.SetClickable(false);
        }
    }

    void ApplyInteractivityForCard(int cardValue)
    {
        if (pausePopupOpen) return;
        List<PlayerPiece> currentPieces = GetPiecesForPlayer(currentPlayer);

        if (currentPieces == null) return;

        foreach (var piece in currentPieces)
        {
            if (piece == null) continue;

            bool canInteract = false;

            // SORRY mode handled elsewhere (START pawn select), keep all current pieces clickable.
            if (isSorryMode)
            {
                canInteract = true;
            }
            else if (cardValue == 10 && isCard10Mode)
            {
                bool canForward = CheckIfMovePossible(piece, 10);
                bool canBackward = !piece.IsAtHome() && CheckIfMovePossible(piece, -1);
                canInteract = canForward || canBackward;
            }
            else if (cardValue == 11 && isCard11Mode)
            {
                bool canForward = CheckIfMovePossible(piece, 11);

                // Swap requires: your selected piece on outer track + at least one opponent outer-track target.
                bool hasOpponentOuterTrack = false;
                foreach (var opp in GetOpponentPieces(currentPlayer))
                {
                    if (opp == null) continue;
                    if (opp.IsOnOuterTrack())
                    {
                        hasOpponentOuterTrack = true;
                        break;
                    }
                }

                bool canSwap = piece.IsOnOuterTrack() && hasOpponentOuterTrack;
                canInteract = canForward || canSwap;
            }
            else if (cardValue == 7 && isSplitMode)
            {
                // Split mode ma pieces click allow karva mate current behavior j rahe.
                canInteract = true;
            }
            else
            {
                canInteract = CheckIfMovePossible(piece, cardValue);
            }

            piece.SetClickable(canInteract);
        }
    }

    /// <summary>
    /// Piece move complete thayu pachhi call karo
    /// </summary>
    public void OnPieceMoved(PlayerPiece movedPiece, int stepsUsed)
    {
        NotifyMoveCompleted();
        Debug.Log($"🔵 OnPieceMoved called: isSplitMode={isSplitMode}, remainingSteps={remainingSteps}, selectedPiece={selectedPieceForSplit != null}");
        
        // Piece move thay pachhi sabhi pieces na destination highlights clear karo
        PlayerPiece.ClearAllPiecesHighlights();

        if (gameOver)
        {
            return;
        }

        if (IsPlayWithOopsMode)
        {
            // IMPORTANT: For +7 split, the server can send 2 separate moves.
            // We must keep the card in-place until BOTH parts complete.
            // This must also work for opponent moves where selectedPieceForSplit may not be set locally.
            if (isSplitMode && remainingSteps > 0)
            {
                // Only update remainder UI/auto-send for the local player when we know the first piece.
                if (currentPlayer == LocalPlayerNumber && selectedPieceForSplit != null)
                {
                    StopAllTurnPieceHighlights();
                    ApplyInteractivityForSplitRemainder(remainingSteps, selectedPieceForSplit);
                    UpdateTurnPieceHighlightsForSplitRemainder(remainingSteps, selectedPieceForSplit);

                    // Auto-finish split remainder in PlayWithOops to prevent soft-lock when the player is inactive.
                    if (oopsAutoSplitFirstSentThisTurn && !oopsAutoSplitSecondSentThisTurn)
                    {
                        oopsAutoSplitSecondSentThisTurn = true;
                        StartCoroutine(OopsAutoSendSplitSecondNextFrame());
                    }
                }

                // Do not return/reset the card yet.
                return;
            }

            if (currentCardHandler != null)
            {
                currentCardHandler.ReturnCardToStart();
            }

            isSplitMode = false;
            remainingSteps = 0;
            selectedPieceForSplit = null;
            oopsSplitAwaitingSecondPiece = false;
            oopsSplitMoveSent = false;

            isCard10Mode = false;
            isCard11Mode = false;
            selectedPieceForCard11 = null;
            isCard12Mode = false;
            selectedPieceForCard12 = null;
            isSorryMode = false;
            selectedPieceForSorry = null;

            cardPicked = false;
            currentCardValue = 0;
            currentCardPower1 = "";
            currentCardPower2 = "";
            currentCardHandler = null;

            StopAllTurnPieceHighlights();

            // Ensure deck + input reflect whose turn it is (server-authoritative).
            suppressHumanInput = currentPlayer != Mathf.Clamp(localPlayerNumber, 1, 4);
            UpdateTurnIndicatorUI();
            UpdateDeckTintForTurn();
            UpdatePiecesInteractivityForOopsTurn();

            return;
        }

        if (movedPiece != null && movedPiece.IsFinishedInHomePath())
        {
            ArrangePiecesInFinalHomeSlot(movedPiece.playerNumber);
            if (CheckAndHandleWinCondition(movedPiece.playerNumber))
            {
                return;
            }

            extraTurnPending = true;
        }
        
        // Card 10 mode check karo
        if (isCard10Mode)
        {
            Debug.Log($"🔵 Card 10 Mode: Piece moved. Completing card 10 mode.");
            CompleteCard10Mode();
            return;
        }

        if (isCard11Mode)
        {
            Debug.Log($"🔵 Card 11 Mode: Piece moved. Completing card 11 mode.");
            CompleteCard11Mode();
            return;
        }

        if (isCard12Mode)
        {
            Debug.Log($"🔵 Card 12 Mode: Piece moved. Completing card 12 mode.");
            CompleteCard12Mode();
            return;
        }
        
        // Split mode check karo (Card 7)
        if (isSplitMode)
        {
            Debug.Log($"🔵 Split Mode Active: remainingSteps={remainingSteps}, stepsUsed={stepsUsed}");
            
            // First piece move thayu
            if (selectedPieceForSplit == null)
            {
                selectedPieceForSplit = movedPiece;
                remainingSteps -= stepsUsed;
                Debug.Log($"🔵 Split Mode: First piece moved {stepsUsed} steps. Remaining: {remainingSteps} steps");
                
                // Agar remaining steps 0 ya negative thai gay to split complete
                if (remainingSteps <= 0)
                {
                    Debug.Log($"🔵 Split Mode: All steps used in first move. Completing split mode.");
                    CompleteSplitMode();
                    return;
                }

                 // Rule: Remaining steps MUST be used by a different piece.
                 // If no other piece can use the remainder, auto-finish by moving the first piece the remaining steps
                 // (effectively behaving like a direct 7 move).
                 bool anyOtherPieceCanUseRemainder = IsAnyOtherPieceCanUseSplitRemainder(remainingSteps, selectedPieceForSplit);
                 if (!anyOtherPieceCanUseRemainder)
                 {
                     int autoSteps = remainingSteps;
                     Debug.LogWarning($"⚠️ Split Mode: No other piece can use remaining {autoSteps} steps. Auto-finishing on first piece (direct 7 behavior).");
                     movedPiece.MovePieceDirectly(autoSteps);
                     return;
                 }

                 ApplyInteractivityForSplitRemainder(remainingSteps, selectedPieceForSplit);
                 UpdateTurnPieceHighlightsForSplitRemainder(remainingSteps, selectedPieceForSplit);
                
                // Card return nahi karo, turn change nahi karo - user ne next piece click karva do
                Debug.Log($"🔵 Split Mode: Card will NOT return yet. Click another piece to use remaining {remainingSteps} steps.");
                return; // IMPORTANT: Return early - card return nahi karo, turn change nahi karo
            }
            else
            {
                // Second piece (or third piece) move thayu - remaining steps use thayu
                remainingSteps -= stepsUsed;
                Debug.Log($"🔵 Split Mode: Additional piece moved {stepsUsed} steps. Remaining: {remainingSteps} steps");
                
                // Agar remaining steps 0 ya negative thai gay to split complete
                if (remainingSteps <= 0)
                {
                    Debug.Log($"🔵 Split Mode: All steps used. Completing split mode.");
                    CompleteSplitMode();
                    return;
                }
                
                // Ahiya pn remaining steps che - user ne next piece click karva do
                Debug.Log($"🔵 Split Mode: Still {remainingSteps} steps remaining. Click another piece to use them.");

                ApplyInteractivityForSplitRemainder(remainingSteps, selectedPieceForSplit);
                UpdateTurnPieceHighlightsForSplitRemainder(remainingSteps, selectedPieceForSplit);
                return; // Return early - card return nahi karo, turn change nahi karo
            }
        }
        else
        {
            // Normal card - card return karo
            Debug.Log($"🔵 Normal card mode: Returning card and switching turn.");
            if (currentCardHandler != null)
            {
                currentCardHandler.ReturnCardToStart();
            }
            
            // Reset Card 10 mode
            isCard10Mode = false;

            isCard11Mode = false;
            selectedPieceForCard11 = null;

            isCard12Mode = false;
            selectedPieceForCard12 = null;
            
            // Card value reset karo
            cardPicked = false;
            currentCardValue = 0;
            currentCardPower1 = "";
            currentCardPower2 = "";
            currentCardHandler = null;

            StopAllTurnPieceHighlights();

            if (extraTurnPending)
            {
                extraTurnPending = false;
                RefreshTurnForCurrentPlayer();
            }
            else
            {
                // Turn change karo (next player)
                SwitchTurn();
            }
        }
    }

    bool CheckAndHandleWinCondition(int playerNumber)
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(playerNumber);
        if (pieces == null || pieces.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null)
            {
                continue;
            }

            if (!p.IsFinishedInHomePath())
            {
                return false;
            }
        }

        DeclareWinner(playerNumber);
        return true;
    }

    void ArrangePiecesInFinalHomeSlot(int playerNumber)
    {
        if (pathManager == null)
        {
            return;
        }

        List<Transform> completePath = pathManager.GetCompletePlayerPath(playerNumber);
        if (completePath == null || completePath.Count == 0)
        {
            return;
        }

        Transform finalSlot = completePath[completePath.Count - 1];
        if (finalSlot == null)
        {
            return;
        }

        List<PlayerPiece> pieces = GetPiecesForPlayer(playerNumber);
        if (pieces == null)
        {
            return;
        }

        List<PlayerPiece> inFinal = new List<PlayerPiece>();
        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            if (!p.IsFinishedInHomePath()) continue;
            if (p.transform.parent != finalSlot) continue;
            inFinal.Add(p);
        }

        if (inFinal.Count == 0)
        {
            return;
        }

        inFinal.Sort((a, b) => a.pieceNumber.CompareTo(b.pieceNumber));

        Transform[] spots = GetFinalHomeSpotsForPlayer(playerNumber);
        bool hasSpotLayout = (spots != null && spots.Length >= 3 && spots[0] != null && spots[1] != null && spots[2] != null);

        if (hasSpotLayout)
        {
            for (int i = 0; i < inFinal.Count; i++)
            {
                PlayerPiece p = inFinal[i];
                if (p == null) continue;

                Transform targetSpot = spots[Mathf.Clamp(i, 0, spots.Length - 1)];
                if (targetSpot == null) continue;

                p.transform.SetParent(targetSpot);
                p.transform.SetAsLastSibling();

                RectTransform rt = p.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition3D = Vector3.zero;
                }
                else
                {
                    Vector3 local = Vector3.zero;
                    local.z = -1.0f;
                    p.transform.localPosition = local;
                }

                p.transform.rotation = finalSlot.rotation;
            }

            return;
        }

        Vector3[] offsets;
        if (inFinal.Count == 1)
        {
            offsets = new[] { Vector3.zero };
        }
        else if (inFinal.Count == 2)
        {
            float d = finalHomeSlotOffset;
            offsets = new[] { new Vector3(-d, 0f, -1f), new Vector3(d, 0f, -1f) };
        }
        else
        {
            float d = finalHomeSlotOffset;
            offsets = new[] { new Vector3(0f, d * 0.6f, -1f), new Vector3(-d, -d * 0.6f, -1f), new Vector3(d, -d * 0.6f, -1f) };
        }

        for (int i = 0; i < inFinal.Count; i++)
        {
            PlayerPiece p = inFinal[i];
            if (p == null) continue;

            Vector3 local = offsets[Mathf.Min(i, offsets.Length - 1)];
            RectTransform rt = p.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition3D = local;
            }
            else
            {
                p.transform.localPosition = local;
            }

            p.transform.SetAsLastSibling();
        }
    }

    void DeclareWinner(int playerNumber)
    {
        gameOver = true;
        winningPlayer = playerNumber;

        Debug.Log($"🏁 WINNER: Player {winningPlayer} (all pieces collected in final HOME slot)");

        PlayerPiece.ClearAllPiecesHighlights();

        if (turnPulseCoroutine != null)
        {
            StopCoroutine(turnPulseCoroutine);
            turnPulseCoroutine = null;
        }

        if (player1TurnImage != null) player1TurnImage.SetActive(false);
        if (player2TurnImage != null) player2TurnImage.SetActive(false);
        if (player3TurnImage != null) player3TurnImage.SetActive(false);
        if (player4TurnImage != null) player4TurnImage.SetActive(false);

        CardClickHandler[] allCards = FindObjectsOfType<CardClickHandler>();
        for (int i = 0; i < allCards.Length; i++)
        {
            if (allCards[i] == null) continue;
            allCards[i].SetCardClickable(false);
        }

        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }

        foreach (var p in GetAllActivePieces())
        {
            if (p == null) continue;
            p.SetClickable(false);
        }

        foreach (var p in GetAllActivePieces())
        {
            if (p == null) continue;
            p.HidePiece();
        }

        PopupHandler popupHandler = FindObjectOfType<PopupHandler>();
        if (popupHandler != null)
        {
            popupHandler.ShowWinPopup(winningPlayer);
        }

        DestroyAllBoardPieces();
    }

    /// <summary>
    /// Card 10 mode complete karo
    /// </summary>
    void CompleteCard10Mode()
    {
        Debug.Log($"🔵 Card 10 Mode Complete!");
        
        // Card return karo
        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }
        
        // Reset Card 10 mode
        isCard10Mode = false;

        isCard11Mode = false;
        selectedPieceForCard11 = null;

        isCard12Mode = false;
        selectedPieceForCard12 = null;
        
        // Card value reset karo
        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        StopAllTurnPieceHighlights();

        if (extraTurnPending)
        {
            extraTurnPending = false;
            RefreshTurnForCurrentPlayer();
        }
        else
        {
            // Turn change karo (next player)
            SwitchTurn();
        }
    }

    /// <summary>
    /// Split mode complete karo (Card 7)
    /// </summary>
    void CompleteSplitMode()
    {
        Debug.Log($"🔵 Split Mode Complete! All steps used.");
        
        // Card return karo
        if (currentCardHandler != null)
        {
            currentCardHandler.ReturnCardToStart();
        }
        
        // Reset split mode
        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;
        
        // Reset Card 10 mode
        isCard10Mode = false;

        isCard11Mode = false;
        selectedPieceForCard11 = null;
        
        // Card value reset karo
        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        StopAllTurnPieceHighlights();

        if (extraTurnPending)
        {
            extraTurnPending = false;
            RefreshTurnForCurrentPlayer();
        }
        else
        {
            // Turn change karo (next player)
            SwitchTurn();
        }
    }

    void RefreshTurnForCurrentPlayer()
    {
        StopCardPickReminder();

        if (IsPlayWithOopsMode)
        {
            suppressHumanInput = currentPlayer != 1;
            UpdateTurnIndicatorUI();
            UpdateDeckTintForTurn();
            UpdatePiecesInteractivityForOopsTurn();
            StartCardPickReminderIfNeeded();
            return;
        }

        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }
        botTurnInProgress = false;

        PlayerPiece.ClearAllPiecesHighlights();

        Debug.Log($"🔁 Extra turn granted to Player {currentPlayer}");

        suppressHumanInput = IsBotPlayer(currentPlayer);

        ShowCurrentPlayerPieces();
        UpdatePiecesInteractivityForTurn();
        UpdateTurnIndicatorUI();

        UpdateTurnPieceHighlights();

        UpdatePowerButtonInteractivity();
        UpdateGameplaySettingsButtonInteractivity();

        TryStartBotTurnIfNeeded();
        UpdateDeckTintForTurn();
        StartCardPickReminderIfNeeded();
    }

    /// <summary>
    /// Turn switch karo (next player)
    /// </summary>
    void SwitchTurn()
    {
        StopCardPickReminder();

        // Never switch turns while a pawn is animating/moving.
        // The switch will be performed by NotifyMoveCompleted once the move finishes.
        if (moveInputLockActive)
        {
            pendingSwitchTurn = true;
            return;
        }

        if (IsPlayWithOopsMode)
        {
            suppressHumanInput = currentPlayer != 1;
            UpdateTurnIndicatorUI();
            UpdateDeckTintForTurn();
            UpdatePiecesInteractivityForOopsTurn();
            StartCardPickReminderIfNeeded();
            return;
        }

        if (cardAnimationLock)
        {
            pendingSwitchTurn = true;
            return;
        }

        // If a bot coroutine was running for the previous turn, stop it to avoid cross-turn state bleed.
        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }
        botTurnInProgress = false;

        // Turn change pachhi sabhi pieces na destination highlights clear karo
        PlayerPiece.ClearAllPiecesHighlights();
        
        int count = GetActivePlayerCount();
        if (count <= 2)
        {
            currentPlayer = currentPlayer == 1 ? 2 : 1;
        }
        else
        {
            currentPlayer++;
            if (currentPlayer > count)
            {
                currentPlayer = 1;
            }
        }
        Debug.Log($"Turn switched to Player {currentPlayer}");

        suppressHumanInput = IsBotPlayer(currentPlayer);

        ShowCurrentPlayerPieces();
        UpdatePiecesInteractivityForTurn();
        UpdateTurnIndicatorUI();

        UpdateTurnPieceHighlights();

        UpdatePowerButtonInteractivity();

        TryStartBotTurnIfNeeded();
        UpdateDeckTintForTurn();
        StartCardPickReminderIfNeeded();
    }

    private void StartCardPickReminderIfNeeded()
    {
        if (!enableHumanCardPickReminder)
        {
            if (debugCardPickReminder) Debug.Log("CardPickReminder: skipped (disabled)");
            return;
        }
        if (gameOver)
        {
            if (debugCardPickReminder) Debug.Log("CardPickReminder: skipped (gameOver)");
            return;
        }
        if (!modeSelected)
        {
            if (debugCardPickReminder) Debug.Log("CardPickReminder: skipped (modeSelected=false)");
            return;
        }
        if (!vsBotMode && !localOfflineFriendsMode && !IsPlayWithOopsMode && !IsOnlineFriendsMode)
        {
            if (debugCardPickReminder) Debug.Log("CardPickReminder: skipped (friends mode)");
            return;
        }

        if ((IsPlayWithOopsMode || IsOnlineFriendsMode) && currentPlayer != Mathf.Clamp(localPlayerNumber, 1, 4))
        {
            StopCardPickReminder();
            return;
        }
        if (cardAnimationLock)
        {
            if (debugCardPickReminder) Debug.Log("CardPickReminder: skipped (cardAnimationLock=true)");
            return;
        }
        if (IsBotPlayer(currentPlayer))
        {
            if (debugCardPickReminder) Debug.Log($"CardPickReminder: skipped (Player {currentPlayer} is bot)");
            return;
        }
        if (cardPicked)
        {
            if (debugCardPickReminder) Debug.Log("CardPickReminder: skipped (cardPicked=true)");
            return;
        }

        if (debugCardPickReminder) Debug.Log($"CardPickReminder: starting for Player {currentPlayer}");

        RefreshActiveCardDeckAnimator();
        if (activeCardDeckAnimator != null)
        {
            activeCardDeckAnimator.StartCardPickArrow();
        }
        else if (!warnedMissingDeckAnimatorForArrow)
        {
            warnedMissingDeckAnimatorForArrow = true;
            Debug.LogWarning("Card Pick Arrow: CardDeckAnimator reference is missing on GameManager. Assign it in Inspector or ensure there is one in the scene.");
        }

        cardPickReminderToken++;
        int token = cardPickReminderToken;
        if (cardPickReminderCoroutine != null)
        {
            StopCoroutine(cardPickReminderCoroutine);
            cardPickReminderCoroutine = null;
        }
        cardPickReminderCoroutine = StartCoroutine(CardPickReminderCoroutine(token));
    }

    private void StopCardPickReminder()
    {
        cardPickReminderToken++;
        if (cardPickReminderCoroutine != null)
        {
            StopCoroutine(cardPickReminderCoroutine);
            cardPickReminderCoroutine = null;
        }

        RefreshActiveCardDeckAnimator();
        if (activeCardDeckAnimator != null)
        {
            activeCardDeckAnimator.StopCardPickArrow();
        }

        CardClickHandler handler = CardClickHandler.GetLastClickableCard();
        if (handler != null)
        {
            RectTransform rt = handler.GetComponent<RectTransform>();
            if (rt != null)
            {
                DOTween.Kill(rt);
            }
        }
    }

    public void NotifyCardClickStarted()
    {
        StopCardPickReminder();
    }

    private IEnumerator CardPickReminderCoroutine(int token)
    {
        float delay = Mathf.Max(0f, cardPickReminderInitialDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        while (true)
        {
            if (token != cardPickReminderToken) yield break;
            if (gameOver || !modeSelected) yield break;
            if (cardAnimationLock) yield break;
            if (IsBotPlayer(currentPlayer)) yield break;
            if ((IsPlayWithOopsMode || IsOnlineFriendsMode) && currentPlayer != Mathf.Clamp(localPlayerNumber, 1, 4)) yield break;
            if (cardPicked) yield break;

            CardClickHandler handler = CardClickHandler.GetLastClickableCard();
            RectTransform rt = handler != null ? handler.GetComponent<RectTransform>() : null;
            if (rt != null)
            {
                DOTween.Kill(rt);
                float d = Mathf.Max(0.05f, cardPickReminderShakeDuration);
                float s = Mathf.Max(0f, cardPickReminderShakeStrength);
                int v = Mathf.Max(2, cardPickReminderShakeVibrato);
                rt.DOShakeAnchorPos(d, s, v, 90f, false, true);
            }

            float repeat = Mathf.Max(0.1f, cardPickReminderRepeatInterval);
            yield return new WaitForSeconds(repeat);
        }
    }

    public bool IsHumanInputSuppressed()
    {
        return suppressHumanInput;
    }

    public void SetCardAnimationLock(bool locked)
    {
        cardAnimationLock = locked;

        if (cardAnimationLock)
        {
            StopCardPickReminder();
            if (deckReadyReminderCoroutine != null)
            {
                StopCoroutine(deckReadyReminderCoroutine);
                deckReadyReminderCoroutine = null;
            }
        }

        UpdatePowerButtonInteractivity();
        UpdateGameplaySettingsButtonInteractivity();
        if (!cardAnimationLock && pendingSwitchTurn)
        {
            pendingSwitchTurn = false;
            SwitchTurn();
            return;
        }

        if (!cardAnimationLock)
        {
            UpdateTurnPieceHighlights();
            StartCardPickReminderIfNeeded();
        }
    }

    bool IsBotPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1IsBot;
        if (playerNumber == 2) return player2IsBot;
        if (playerNumber == 3) return player3IsBot;
        if (playerNumber == 4) return player4IsBot;
        return false;
    }

    void TryStartBotTurnIfNeeded()
    {
        if (IsPlayWithOopsMode)
        {
            if (botTurnCoroutine != null)
            {
                StopCoroutine(botTurnCoroutine);
                botTurnCoroutine = null;
            }
            botTurnInProgress = false;
            return;
        }

        if (gameOver)
        {
            return;
        }

        if (cardAnimationLock)
        {
            return;
        }

        if (!modeSelected)
        {
            return;
        }

        if (!IsBotPlayer(currentPlayer))
        {
            suppressHumanInput = false;
            return;
        }

        if (cardPicked)
        {
            return;
        }

        suppressHumanInput = true;

        if (botTurnInProgress)
        {
            return;
        }

        if (botTurnCoroutine != null)
        {
            return;
        }

        Debug.Log($"🤖 Bot: Starting bot turn for Player {currentPlayer}");
        botTurnCoroutine = StartCoroutine(BotTurnCoroutine());
    }

    public void NotifyDeckReady()
    {
        float now = Time.time;
        if (now - lastDeckReadyNotifyTime < 0.35f)
        {
            return;
        }

        lastDeckReadyNotifyTime = now;

        deckReadyForTurnCountdown = true;
        UpdateTurnIndicatorUI();

        TryStartBotTurnIfNeeded();

        RefreshActiveCardDeckAnimator();
        UpdateDeckTintForTurn();

        if (deckReadyReminderCoroutine != null)
        {
            StopCoroutine(deckReadyReminderCoroutine);
            deckReadyReminderCoroutine = null;
        }
        deckReadyReminderCoroutine = StartCoroutine(StartCardPickReminderAfterDeckReady());
    }

    private IEnumerator StartCardPickReminderAfterDeckReady()
    {
        float d = Mathf.Max(0f, cardPickReminderAfterDeckReadyDelay);
        if (d > 0f)
        {
            yield return new WaitForSeconds(d);
        }
        if (debugCardPickReminder) Debug.Log($"CardPickReminder: deck ready delay elapsed ({d:0.00}s)");
        StartCardPickReminderIfNeeded();
        deckReadyReminderCoroutine = null;
    }

    public void OnPowerButtonClicked()
    {
        if (gameOver || !modeSelected)
        {
            return;
        }

        if (!cardPicked)
        {
            return;
        }

        if (cardAnimationLock)
        {
            return;
        }

        if (currentCardHandler == null)
        {
            return;
        }

        if (isSplitMode && selectedPieceForSplit != null)
        {
            return;
        }

        if (isSorryMode || isCard11Mode || isCard12Mode)
        {
            return;
        }

        StartCoroutine(PowerButtonDiscardAndOpenNext());
    }

    IEnumerator PowerButtonDiscardAndOpenNext()
    {
        SetCardAnimationLock(true);

        CardClickHandler handlerToReturn = currentCardHandler;

        isSplitMode = false;
        remainingSteps = 0;
        selectedPieceForSplit = null;
        isCard10Mode = false;
        isCard11Mode = false;
        selectedPieceForCard11 = null;
        isCard12Mode = false;
        selectedPieceForCard12 = null;
        isSorryMode = false;
        selectedPieceForSorry = null;

        cardPicked = false;
        currentCardValue = 0;
        currentCardPower1 = "";
        currentCardPower2 = "";
        currentCardHandler = null;

        if (handlerToReturn != null)
        {
            handlerToReturn.ReturnCardToStart();
        }

        UpdatePowerButtonInteractivity();

        float timeout = 3.5f;
        while (cardAnimationLock && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return null;

        CardClickHandler nextCard = CardClickHandler.GetLastClickableCard();
        if (nextCard != null)
        {
            nextCard.TriggerCardClick();
        }
    }

    CardClickHandler FindLastDeckCardHandlerFallback()
    {
        CardClickHandler[] all = FindObjectsOfType<CardClickHandler>();
        CardClickHandler best = null;
        int bestSiblingIndex = -1;
        int bestParentChildCount = -1;

        for (int i = 0; i < all.Length; i++)
        {
            CardClickHandler h = all[i];
            if (h == null) continue;
            Transform p = h.transform.parent;
            if (p == null) continue;
            if (!p.name.Contains("DeckShadow")) continue;

            int sibling = h.transform.GetSiblingIndex();
            int childCount = p.childCount;

            if (childCount > bestParentChildCount || (childCount == bestParentChildCount && sibling > bestSiblingIndex))
            {
                best = h;
                bestSiblingIndex = sibling;
                bestParentChildCount = childCount;
            }
        }

        return best;
    }

    IEnumerator BotTurnCoroutine()
    {
        int botTurnPlayer = currentPlayer;
        botTurnInProgress = true;

        yield return new WaitForSeconds(botThinkDelay);

        if (gameOver || !modeSelected || currentPlayer != botTurnPlayer)
        {
            botTurnInProgress = false;
            botTurnCoroutine = null;
            yield break;
        }

        if (!IsBotPlayer(currentPlayer) || currentPlayer != botTurnPlayer)
        {
            botTurnInProgress = false;
            botTurnCoroutine = null;
            yield break;
        }

        float prePickDelay = UnityEngine.Random.Range(1f, 4f);
        yield return new WaitForSeconds(prePickDelay);

        if (gameOver || !modeSelected || !IsBotPlayer(currentPlayer) || currentPlayer != botTurnPlayer)
        {
            botTurnInProgress = false;
            botTurnCoroutine = null;
            yield break;
        }

        if (!cardPicked)
        {
            float timeout = 20f;
            float retryTimer = 0f;
            float postClickCooldown = 0f;
            while (!cardPicked && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                retryTimer -= Time.deltaTime;
                postClickCooldown -= Time.deltaTime;

                if (gameOver || !modeSelected || currentPlayer != botTurnPlayer)
                {
                    break;
                }

                if (!IsBotPlayer(currentPlayer) || currentPlayer != botTurnPlayer)
                {
                    break;
                }

                if (cardAnimationLock)
                {
                    yield return null;
                    continue;
                }

                if (retryTimer <= 0f)
                {
                    retryTimer = 0.35f;

                    if (postClickCooldown > 0f)
                    {
                        yield return null;
                        continue;
                    }

                    CardClickHandler lastCard = CardClickHandler.GetLastClickableCard();
                    if (lastCard == null)
                    {
                        lastCard = FindLastDeckCardHandlerFallback();
                        if (lastCard != null)
                        {
                            lastCard.SetCardClickable(true);
                        }
                    }

                    if (lastCard != null)
                    {
                        Debug.Log($"🤖 Bot: Triggering card click for Player {currentPlayer} using '{lastCard.gameObject.name}'");
                        lastCard.TriggerCardClick();
                        postClickCooldown = 0.6f;
                    }
                }

                yield return null;
            }
        }

        if (!cardPicked)
        {
            Debug.LogWarning($"🤖 Bot: Card was not picked in time for Player {currentPlayer}. Skipping.");
            botTurnInProgress = false;
            botTurnCoroutine = null;
            yield break;
        }

        float preMoveDelay = UnityEngine.Random.Range(1f, 4f);
        yield return new WaitForSeconds(preMoveDelay);

        if (gameOver || !modeSelected || !IsBotPlayer(currentPlayer) || currentPlayer != botTurnPlayer)
        {
            botTurnInProgress = false;
            botTurnCoroutine = null;
            yield break;
        }

        bool actionDone = false;

        if (isSorryMode)
        {
            actionDone = TryExecuteBotSorryAction();
        }
        else if (isCard12Mode)
        {
            actionDone = TryExecuteBotCard12Capture();
        }
        else if (isCard11Mode)
        {
            actionDone = TryExecuteBotCard11Action();
        }
        else if (isCard10Mode)
        {
            actionDone = TryExecuteBotCard10Action();
        }
        else if (currentCardValue == 7 && isSplitMode)
        {
            actionDone = TryExecuteBotSplit7();
            if (actionDone)
            {
                float splitTimeout = 12f;
                while (splitTimeout > 0f && isSplitMode && IsBotPlayer(currentPlayer) && !gameOver)
                {
                    splitTimeout -= Time.deltaTime;
                    yield return null;
                }

                botTurnInProgress = false;
                botTurnCoroutine = null;
                yield break;
            }
        }
        else
        {
            actionDone = TryExecuteBotNormalMove(currentCardValue);
        }

        if (!actionDone)
        {
            Debug.LogWarning($"🤖 Bot: No legal action found for Player {currentPlayer} (cardValue={currentCardValue}). Skipping.");
            yield return StartCoroutine(SkipTurnAfterDelay());

            botTurnInProgress = false;
            botTurnCoroutine = null;

            TryStartBotTurnIfNeeded();
            yield break;
        }

        botTurnInProgress = false;
        botTurnCoroutine = null;
    }

    bool TryExecuteBotNormalMove(int steps)
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null) return false;

        if (!TryPickBotMoveForSteps(steps, out PlayerPiece bestPiece))
        {
            return false;
        }

        bestPiece.MovePieceDirectly(steps);
        return true;
    }

    bool TryExecuteBotCard10Action()
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null) return false;

        bool hasForward = TryPickBotMoveForSteps(10, out PlayerPiece forwardPiece);
        bool hasBackward = TryPickBotMoveForSteps(-1, out PlayerPiece backwardPiece);

        if (!hasForward && !hasBackward)
        {
            return false;
        }

        float forwardScore = float.NegativeInfinity;
        if (hasForward && forwardPiece != null)
        {
            forwardScore = ScoreMoveForPiece(forwardPiece, 10);
        }

        float backwardScore = float.NegativeInfinity;
        if (hasBackward && backwardPiece != null)
        {
            backwardScore = ScoreMoveForPiece(backwardPiece, -1);
        }

        if (hasForward && (!hasBackward || forwardScore >= backwardScore))
        {
            forwardPiece.MovePieceDirectly(10);
            return true;
        }

        backwardPiece.MovePieceDirectly(-1);
        return true;
    }

    bool TryExecuteBotCard11Action()
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null) return false;

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            if (CheckIfMovePossible(p, 11))
            {
                if (TryGetDestinationForMove(p, 11, out int destIndex, out Transform destPos, out string reason) && destPos != null)
                {
                    p.OnDestinationClicked(destPos, 11);
                }
                else
                {
                    Debug.LogWarning($"🤖 Bot: Card 11 forward move looked possible but destination resolve failed: {reason} (destIndex={destIndex}). Falling back to swap.");
                    break;
                }
                return true;
            }
        }

        PlayerPiece source = null;
        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            p.SyncCurrentPathIndexFromTransform();
            if (!p.IsOnOuterTrack()) continue;

            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.SyncCurrentPathIndexFromTransform();
                if (!opp.IsOnOuterTrack()) continue;
                source = p;
                break;
            }

            if (source != null) break;
        }

        if (source == null)
        {
            return false;
        }

        source.TriggerBotPieceClick();

        PlayerPiece target = null;
        foreach (var opp in GetOpponentPieces(currentPlayer))
        {
            if (opp == null) continue;
            opp.SyncCurrentPathIndexFromTransform();
            if (!opp.IsOnOuterTrack()) continue;
            target = opp;
            break;
        }

        if (target == null)
        {
            return false;
        }

        target.TriggerBotPieceClick();
        return true;
    }

    bool TryExecuteBotCard12Capture()
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null) return false;

        PlayerPiece source = null;
        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            if (!CheckIfMovePossible(p, 12)) continue;
            source = p;
            break;
        }

        if (source == null)
        {
            return false;
        }

        source.TriggerBotPieceClick();

        PlayerPiece target = null;
        foreach (var opp in GetOpponentPieces(currentPlayer))
        {
            if (opp == null) continue;
            opp.SyncCurrentPathIndexFromTransform();
            if (opp.IsAtHome()) continue;
            if (opp.IsOnHomePath()) continue;
            if (opp.IsFinishedInHomePath()) continue;
            target = opp;
            break;
        }

        if (target == null)
        {
            return false;
        }

        target.TriggerBotPieceClick();
        return true;
    }

    bool TryExecuteBotSorryAction()
    {
        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null) return false;

        PlayerPiece source = null;
        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            p.SyncCurrentPathIndexFromTransform();
            if (!p.IsAtHome()) continue;
            source = p;
            break;
        }

        if (source != null)
        {
            PlayerPiece target = null;
            foreach (var opp in GetOpponentPieces(currentPlayer))
            {
                if (opp == null) continue;
                opp.SyncCurrentPathIndexFromTransform();
                if (opp.IsAtHome()) continue;
                if (opp.IsOnHomePath()) continue;
                if (opp.IsFinishedInHomePath()) continue;
                target = opp;
                break;
            }

            if (target != null)
            {
                source.TriggerBotPieceClick();
                target.TriggerBotPieceClick();
                return true;
            }
        }

        return TryExecuteBotNormalMove(4);
    }

    bool TryPickBotMoveForSteps(int steps, out PlayerPiece chosenPiece)
    {
        chosenPiece = null;

        List<PlayerPiece> pieces = GetPiecesForPlayer(currentPlayer);
        if (pieces == null || pathManager == null)
        {
            return false;
        }

        List<(PlayerPiece piece, float score)> candidates = new List<(PlayerPiece piece, float score)>();

        for (int i = 0; i < pieces.Count; i++)
        {
            PlayerPiece p = pieces[i];
            if (p == null) continue;
            if (!CheckIfMovePossible(p, steps)) continue;

            float s = ScoreMoveForPiece(p, steps);
            candidates.Add((p, s));
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        if (botDifficulty == BotDifficulty.Easy)
        {
            chosenPiece = candidates[UnityEngine.Random.Range(0, candidates.Count)].piece;
            return chosenPiece != null;
        }

        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        if (botDifficulty == BotDifficulty.Hard)
        {
            chosenPiece = candidates[0].piece;
            return chosenPiece != null;
        }

        int topN = Mathf.Clamp(1 + Mathf.RoundToInt(botHumanRandomness * 4f), 2, Mathf.Min(3, candidates.Count));
        float totalWeight = 0f;
        float[] weights = new float[topN];
        for (int i = 0; i < topN; i++)
        {
            float w = Mathf.Exp(candidates[i].score * 0.03f);
            weights[i] = w;
            totalWeight += w;
        }

        float pick = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < topN; i++)
        {
            pick -= weights[i];
            if (pick <= 0f)
            {
                chosenPiece = candidates[i].piece;
                return chosenPiece != null;
            }
        }

        chosenPiece = candidates[0].piece;
        return chosenPiece != null;
    }

    float ScoreMoveForPiece(PlayerPiece piece, int steps)
    {
        if (piece == null || pathManager == null)
        {
            return float.NegativeInfinity;
        }

        piece.SyncCurrentPathIndexFromTransform();

        bool ok = TryGetDestinationForMove(piece, steps, out int destIndex, out Transform dest, out string reason);
        if (!ok || dest == null)
        {
            return float.NegativeInfinity;
        }

        float score = 0f;

        List<Transform> completePath = pathManager.GetCompletePlayerPath(piece.playerNumber);
        int lastIndex = (completePath != null && completePath.Count > 0) ? (completePath.Count - 1) : -1;

        int fromIndex = piece.GetCurrentPathIndex();

        if (lastIndex >= 0 && destIndex == lastIndex)
        {
            score += 1000f;
        }

        if (completePath != null && completePath.Count > 0)
        {
            int homePathStart = Mathf.Max(0, completePath.Count - 5);
            if (destIndex >= homePathStart)
            {
                score += 80f + (destIndex - homePathStart) * 15f;
            }
        }

        if (fromIndex < 0)
        {
            score += 35f;
        }

        int progressDelta = (fromIndex < 0) ? (destIndex + 1) : (destIndex - fromIndex);
        score += progressDelta * 6f;

        if (steps < 0)
        {
            score -= 18f;
        }

        if (WouldBumpOpponentAtDestination(piece, dest))
        {
            score += 140f;
        }

        if (WouldBumpOwnPieceAtDestination(piece, dest))
        {
            score -= 260f;
        }

        score += ScoreSlideOpportunity(piece, destIndex, dest);

        return score;
    }

    float ScoreSlideOpportunity(PlayerPiece mover, int destIndex, Transform dest)
    {
        if (mover == null || dest == null || pathManager == null)
        {
            return 0f;
        }

        mover.SyncCurrentPathIndexFromTransform();
        if (destIndex < 0)
        {
            return 0f;
        }

        List<Transform> routePath = pathManager.GetPlayerRoutePath(mover.playerNumber);
        int routePathLength = routePath != null ? routePath.Count : -1;
        if (routePathLength <= 0)
        {
            return 0f;
        }

        if (destIndex >= routePathLength)
        {
            return 0f;
        }

        SlideTrigger[] triggers = dest.GetComponentsInParent<SlideTrigger>(true);
        if (triggers == null || triggers.Length == 0)
        {
            return 0f;
        }

        SlideTrigger matched = null;
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null && triggers[i].ownerPlayer == mover.playerNumber)
            {
                matched = triggers[i];
                break;
            }
        }

        if (matched == null)
        {
            return 0f;
        }

        int steps = Mathf.Max(0, matched.slideSteps);
        if (steps <= 0)
        {
            return 0f;
        }

        int routeEntryIndex = Mathf.Max(0, routePathLength - 2);
        int routePathLastIndex = routePathLength - 1;

        float score = 18f;
        int posIndex = destIndex;

        for (int s = 0; s < steps; s++)
        {
            int nextIndex = (posIndex == routeEntryIndex) ? 0 : posIndex + 1;
            if (nextIndex == routePathLastIndex)
            {
                nextIndex = (routePathLastIndex == routeEntryIndex) ? 0 : routeEntryIndex;
            }

            List<Transform> completePath = pathManager.GetCompletePlayerPath(mover.playerNumber);
            if (completePath == null || nextIndex < 0 || nextIndex >= completePath.Count)
            {
                break;
            }

            Transform nextPos = completePath[nextIndex];
            if (nextPos == null)
            {
                break;
            }

            if (WouldBumpOpponentAtDestination(mover, nextPos))
            {
                score += 90f;
            }

            if (WouldBumpOwnPieceAtDestination(mover, nextPos))
            {
                score -= 220f;
            }

            posIndex = nextIndex;
        }

        return score;
    }

    bool WouldBumpOpponentAtDestination(PlayerPiece mover, Transform dest)
    {
        if (mover == null || dest == null)
        {
            return false;
        }

        foreach (var opp in GetOpponentPieces(mover.playerNumber))
        {
            if (opp == null) continue;
            if (opp.IsAtHome()) continue;

            opp.SyncCurrentPathIndexFromTransform();
            if (opp.IsOnHomePath()) continue;
            if (opp.IsFinishedInHomePath()) continue;

            Transform oppPos = opp.GetCurrentPositionTransform();
            bool sameTransform = (oppPos == dest || opp.transform.parent == dest);
            if (sameTransform)
            {
                return true;
            }

            const float bumpEpsilon = 0.05f;
            if (Vector3.Distance(opp.transform.position, dest.position) <= bumpEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    bool WouldBumpOwnPieceAtDestination(PlayerPiece mover, Transform dest)
    {
        if (mover == null || dest == null)
        {
            return false;
        }

        foreach (PlayerPiece other in GetPiecesForPlayer(mover.playerNumber))
        {
            if (other == null) continue;
            if (other == mover) continue;
            if (other.IsAtHome()) continue;

            other.SyncCurrentPathIndexFromTransform();
            if (other.IsOnHomePath()) continue;
            if (other.IsFinishedInHomePath()) continue;

            Transform otherPos = other.GetCurrentPositionTransform();
            bool sameTransform = (otherPos == dest || other.transform.parent == dest);
            if (sameTransform)
            {
                return true;
            }

            const float bumpEpsilon = 0.05f;
            if (Vector3.Distance(other.transform.position, dest.position) <= bumpEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    void UpdateTurnIndicatorUI()
    {
        int count = GetActivePlayerCount();
        bool showIndicators = !delayTurnUiUntilDeckReady || deckReadyForTurnCountdown;

        int uiPlayer = currentPlayer;
        if (moveInputLockActive && moveInputLockPlayer > 0)
        {
            uiPlayer = moveInputLockPlayer;
        }

        if (player1TurnImage != null) player1TurnImage.SetActive(showIndicators && uiPlayer == 1);
        if (player2TurnImage != null) player2TurnImage.SetActive(showIndicators && uiPlayer == 2);
        if (player3TurnImage != null) player3TurnImage.SetActive(showIndicators && count >= 3 && uiPlayer == 3);
        if (player4TurnImage != null) player4TurnImage.SetActive(showIndicators && count >= 4 && uiPlayer == 4);

        if (IsPlayWithOopsMode)
        {
            string p1Name = player1TurnImage != null ? player1TurnImage.name : "<null>";
            string p2Name = player2TurnImage != null ? player2TurnImage.name : "<null>";
            bool p1Active = player1TurnImage != null && player1TurnImage.activeSelf;
            bool p2Active = player2TurnImage != null && player2TurnImage.activeSelf;
            Debug.Log($"<color=#8BC34A>[OOPS TurnUI]</color> currentPlayer={currentPlayer} show={showIndicators} p1='{p1Name}' active={p1Active} p2='{p2Name}' active={p2Active}");
        }

        if (turnPulseCoroutine != null)
        {
            StopCoroutine(turnPulseCoroutine);
            turnPulseCoroutine = null;
        }

        if (!showIndicators)
        {
            StopTurnCountdown();
            return;
        }

        for (int p = 1; p <= 4; p++)
        {
            TMP_Text t = GetTurnTimerTextForPlayer(p);
            if (t == null) continue;
            if (p == uiPlayer)
            {
                bool canShowTimerText = deckReadyForTurnCountdown
                                        && !IsPlayWithOopsMode
                                        && (!IsBotPlayer(currentPlayer) || enableTurnTimerForBots);

                if (canShowTimerText)
                {
                    int secs = Mathf.CeilToInt(Mathf.Max(1f, turnCountdownSeconds));
                    t.text = secs.ToString();
                }
                else
                {
                    t.text = string.Empty;
                }
            }
            else
            {
                t.text = string.Empty;
            }
        }

        GameObject activeObj = GetTurnIndicatorForPlayer(uiPlayer);
        if (activeObj == null)
        {
            return;
        }

        RectTransform rt = activeObj.GetComponent<RectTransform>();
        if (rt == null)
        {
            return;
        }

        if (enableTurnPulseAnimation)
        {
            turnPulseCoroutine = StartCoroutine(PulseTurnIndicator(rt));
        }
        else
        {
            rt.localScale = Vector3.one;
        }

        if (!deckReadyForTurnCountdown)
        {
            StopTurnCountdown();
            return;
        }

        // Do not restart/reset the countdown UI while a move is in progress.
        // Only start the countdown when the moving lock has cleared AND the UI player is the real currentPlayer.
        if (moveInputLockActive)
        {
            return;
        }

        if (uiPlayer != currentPlayer)
        {
            return;
        }

        StartTurnCountdownForCurrentPlayer();
    }

    IEnumerator PulseTurnIndicator(RectTransform rt)
    {
        if (rt == null)
        {
            yield break;
        }

        Vector3 baseScale = Vector3.one;
        rt.localScale = baseScale;

        float duration = Mathf.Max(0.05f, turnPulseDuration);
        float half = duration * 0.5f;
        Vector3 peak = baseScale * Mathf.Max(1f, turnPulseScale);

        while (true)
        {
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / half);
                rt.localScale = Vector3.Lerp(baseScale, peak, u);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / half);
                rt.localScale = Vector3.Lerp(peak, baseScale, u);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Current player get karo
    /// </summary>
    public int GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public int GetCardSpriteVariantForCurrentPlayer()
    {
        return GetCardSpriteVariantForPlayer(currentPlayer);
    }

    public int GetCardSpriteVariantForPlayer(int playerNumber)
    {
        if (playerNumber == 1) return player1CardSpriteVariant;
        if (playerNumber == 2) return player2CardSpriteVariant;
        if (playerNumber == 3) return player3CardSpriteVariant;
        if (playerNumber == 4) return player4CardSpriteVariant;
        return Mathf.Clamp(playerNumber, 1, 4);
    }

    /// <summary>
    /// Card pick thayu che ke nahi check karo
    /// </summary>
    public bool IsCardPicked()
    {
        return cardPicked;
    }

    /// <summary>
    /// Current card value get karo
    /// </summary>
    public int GetCurrentCardValue()
    {
        return currentCardValue;
    }

    /// <summary>
    /// Current card power1 get karo
    /// </summary>
    public string GetCurrentCardPower1()
    {
        return currentCardPower1 ?? "";
    }

    /// <summary>
    /// Current card power2 get karo
    /// </summary>
    public string GetCurrentCardPower2()
    {
        return currentCardPower2 ?? "";
    }

    /// <summary>
    /// Card value extract karo card power text mathi
    /// Example: "+3" -> 3, "Move +5" -> 5, "+1 move" -> 1
    /// </summary>
    public static int ExtractCardValue(string powerText)
    {
        if (string.IsNullOrEmpty(powerText))
        {
            return 0;
        }

        string text = powerText.Trim();

        int sign = 1;
        bool readingNumber = false;
        string numberStr = "";

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (!readingNumber)
            {
                if (c == '+' || c == '-')
                {
                    sign = (c == '-') ? -1 : 1;
                    continue;
                }

                if (char.IsDigit(c))
                {
                    readingNumber = true;
                    numberStr += c;
                }

                continue;
            }

            if (char.IsDigit(c))
            {
                numberStr += c;
            }
            else
            {
                break;
            }
        }

        if (int.TryParse(numberStr, out int value))
        {
            return sign * value;
        }

        return 0;
    }

    /// <summary>
    /// Split mode active che ke nahi check karo
    /// </summary>
    public bool IsSplitMode()
    {
        return isSplitMode;
    }

    /// <summary>
    /// Remaining steps get karo (split mode mate)
    /// </summary>
    public int GetRemainingSteps()
    {
        return remainingSteps;
    }

    /// <summary>
    /// First piece already move thayu che ke nahi check karo (split mode)
    /// </summary>
    public bool IsFirstPieceMovedInSplit()
    {
        return selectedPieceForSplit != null;
    }

    /// <summary>
    /// First piece get karo (split mode)
    /// </summary>
    public PlayerPiece GetFirstPieceInSplit()
    {
        return selectedPieceForSplit;
    }

    /// <summary>
    /// Selected piece for split get karo
    /// </summary>
    public PlayerPiece GetSelectedPieceForSplit()
    {
        return selectedPieceForSplit;
    }

    /// <summary>
    /// Card 10 mode active che ke nahi check karo
    /// </summary>
    public bool IsCard10Mode()
    {
        return isCard10Mode;
    }
}

