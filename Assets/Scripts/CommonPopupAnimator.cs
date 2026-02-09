using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CommonPopupAnimator : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject popupRoot;

    [Header("Board")]
    [SerializeField] private RectTransform board;
    [SerializeField] private Vector3 boardHiddenScale = Vector3.zero;
    [SerializeField] private Vector3 boardShownScale = Vector3.one;
    [SerializeField] private float boardOpenDuration = 0.28f;
    [SerializeField] private float boardCloseDuration = 0.22f;
    [SerializeField] private Ease boardOpenEase = Ease.OutBack;
    [SerializeField] private Ease boardCloseEase = Ease.InBack;

    [Header("Piece")]
    [SerializeField] private RectTransform piece;
    [SerializeField] private float pieceRiseDistance = 260f;
    [SerializeField] private float pieceRiseDuration = 0.26f;
    [SerializeField] private float pieceFallDuration = 0.20f;
    [SerializeField] private float pieceDelayAfterBoardOpen = 0.05f;
    [SerializeField] private Ease pieceRiseEase = Ease.OutCubic;
    [SerializeField] private Ease pieceFallEase = Ease.InCubic;
    [SerializeField] private bool pieceFade = true;

    [Header("After Board Items")]
    [SerializeField] private List<RectTransform> afterBoardItems = new List<RectTransform>();
    [SerializeField] private float afterBoardItemDuration = 0.22f;
    [SerializeField] private float afterBoardItemStagger = 0.06f;
    [SerializeField] private Ease afterBoardItemEase = Ease.OutBack;
    [SerializeField] private bool afterBoardItemFade = true;
    [SerializeField] private float afterBoardItemFadeDuration = 0.16f;
    [SerializeField] private bool playPieceAfterItems = true;

    [Header("Interaction")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Sequence seq;
    private Vector2 pieceShownPos;
    private Vector2 pieceHiddenPos;

    private readonly Dictionary<RectTransform, Vector3> itemBaseScale = new Dictionary<RectTransform, Vector3>();

    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }
        else
        {
            if (popupRoot.GetComponent<PopupHandler>() != null)
            {
                popupRoot = gameObject;
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = popupRoot.GetComponent<CanvasGroup>();
        }

        if (piece != null)
        {
            pieceShownPos = piece.anchoredPosition;
            RefreshRuntimeCaches();
        }

        CacheItemBaseScales();
    }

    private void OnDisable()
    {
        Kill();
    }

    public void Open()
    {
        Canvas.ForceUpdateCanvases();
        RefreshRuntimeCaches();

        if (popupRoot != null && !popupRoot.activeSelf)
        {
            popupRoot.SetActive(true);
        }

        Kill();
        SetInteractable(false);

        if (board != null)
        {
            board.localScale = boardHiddenScale;
        }

        ResetAfterBoardItemsHidden();

        if (piece != null)
        {
            piece.anchoredPosition = pieceHiddenPos;
            if (pieceFade)
            {
                var cg = piece.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        if (board != null)
        {
            seq.Append(board.DOScale(boardShownScale, Mathf.Max(0.01f, boardOpenDuration)).SetEase(boardOpenEase));
        }

        if (piece != null && !playPieceAfterItems)
        {
            AppendPieceOpen();
        }

        AppendAfterBoardItemsOpen();

        if (piece != null && playPieceAfterItems)
        {
            AppendPieceOpen();
        }

        seq.OnComplete(() =>
        {
            SetInteractable(true);
        });
    }

    public void Close(Action onClosed = null)
    {
        Kill();
        SetInteractable(false);

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        if (piece != null)
        {
            seq.Append(piece.DOAnchorPos(pieceHiddenPos, Mathf.Max(0.01f, pieceFallDuration)).SetEase(pieceFallEase));

            if (pieceFade)
            {
                var cg = piece.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    seq.Join(cg.DOFade(0f, Mathf.Max(0.01f, pieceFallDuration * 0.8f)));
                }
            }
        }

        AppendAfterBoardItemsClose();

        if (board != null)
        {
            seq.Append(board.DOScale(boardHiddenScale, Mathf.Max(0.01f, boardCloseDuration)).SetEase(boardCloseEase));
        }

        seq.OnComplete(() =>
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }

            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }

            onClosed?.Invoke();
        });
    }

    public void HideInstant()
    {
        Canvas.ForceUpdateCanvases();
        RefreshRuntimeCaches();

        Kill();
        SetInteractable(false);

        if (board != null)
        {
            board.localScale = boardHiddenScale;
        }

        ResetAfterBoardItemsHidden();

        if (piece != null)
        {
            piece.anchoredPosition = pieceHiddenPos;
            if (pieceFade)
            {
                var cg = piece.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void RefreshRuntimeCaches()
    {
        if (piece != null)
        {
            pieceHiddenPos = pieceShownPos + Vector2.down * pieceRiseDistance;
        }

        CacheItemBaseScales();
    }

    private void SetInteractable(bool on)
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = on;
    }

    private void CacheItemBaseScales()
    {
        if (afterBoardItems == null) return;

        for (int i = 0; i < afterBoardItems.Count; i++)
        {
            var rt = afterBoardItems[i];
            if (rt == null) continue;

            if (!itemBaseScale.ContainsKey(rt))
            {
                Vector3 s = rt.localScale;
                if (s.x < 0.001f && s.y < 0.001f)
                {
                    s = Vector3.one;
                }
                itemBaseScale[rt] = s;
            }
        }
    }

    private void ResetAfterBoardItemsHidden()
    {
        CacheItemBaseScales();

        if (afterBoardItems == null) return;

        for (int i = 0; i < afterBoardItems.Count; i++)
        {
            var rt = afterBoardItems[i];
            if (rt == null) continue;

            rt.localScale = Vector3.zero;

            if (afterBoardItemFade)
            {
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }
    }

    private void AppendAfterBoardItemsOpen()
    {
        if (afterBoardItems == null || afterBoardItems.Count == 0) return;

        float dur = Mathf.Max(0.01f, afterBoardItemDuration);
        float stg = Mathf.Max(0f, afterBoardItemStagger);

        float baseTime = seq.Duration();

        for (int i = 0; i < afterBoardItems.Count; i++)
        {
            var rt = afterBoardItems[i];
            if (rt == null) continue;

            Vector3 baseS;
            if (!itemBaseScale.TryGetValue(rt, out baseS))
            {
                baseS = rt.localScale;
            }

            float start = i * stg;
            seq.Insert(baseTime + start, rt.DOScale(baseS, dur).SetEase(afterBoardItemEase));

            if (afterBoardItemFade)
            {
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float fd = Mathf.Max(0.01f, afterBoardItemFadeDuration);
                    seq.Insert(baseTime + start, cg.DOFade(1f, fd));
                }
            }
        }
    }

    private void AppendAfterBoardItemsClose()
    {
        if (afterBoardItems == null || afterBoardItems.Count == 0) return;

        float dur = Mathf.Max(0.01f, afterBoardItemDuration);
        float stg = Mathf.Max(0f, afterBoardItemStagger);

        float baseTime = seq.Duration();

        for (int i = afterBoardItems.Count - 1; i >= 0; i--)
        {
            var rt = afterBoardItems[i];
            if (rt == null) continue;

            int rev = (afterBoardItems.Count - 1) - i;
            float start = rev * stg;
            seq.Insert(baseTime + start, rt.DOScale(Vector3.zero, dur).SetEase(Ease.InBack));

            if (afterBoardItemFade)
            {
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float fd = Mathf.Max(0.01f, afterBoardItemFadeDuration);
                    seq.Insert(baseTime + start, cg.DOFade(0f, fd));
                }
            }
        }
    }

    private void AppendPieceOpen()
    {
        if (pieceDelayAfterBoardOpen > 0f)
        {
            seq.AppendInterval(pieceDelayAfterBoardOpen);
        }

        seq.Append(piece.DOAnchorPos(pieceShownPos, Mathf.Max(0.01f, pieceRiseDuration)).SetEase(pieceRiseEase));

        if (pieceFade)
        {
            var cg = piece.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                seq.Join(cg.DOFade(1f, Mathf.Max(0.01f, pieceRiseDuration * 0.8f)));
            }
        }
    }

    private void Kill()
    {
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }
    }
}
