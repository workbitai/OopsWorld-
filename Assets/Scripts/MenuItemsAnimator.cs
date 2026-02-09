using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MenuItemsAnimator : MonoBehaviour
{
    [Header("Auto Play")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool hideOnEnable = true;
    [SerializeField] private float startDelay = 0.05f;

    [Header("Speed")]
    [SerializeField] private float speedMultiplier = 1f;

    [Header("Items (order matters)")]
    [SerializeField] private List<RectTransform> items = new List<RectTransform>();

    [Header("Entrance")]
    [SerializeField] private float itemDuration = 0.35f;
    [SerializeField] private float itemStagger = 0.07f;
    [SerializeField] private Ease itemEase = Ease.OutBack;
    [SerializeField] private float overshootScale = 1.08f;
    [SerializeField] private float overshootDuration = 0.10f;

    [Header("Optional Fade")]
    [SerializeField] private bool useCanvasGroupFade = false;
    [SerializeField] private float fadeDuration = 0.18f;

    [Header("Idle")]
    [SerializeField] private bool playIdlePulse = true;
    [SerializeField] private float idlePulseScale = 1.03f;
    [SerializeField] private float idlePulseDuration = 0.9f;
    [SerializeField] private float idlePulseStagger = 0.25f;

    private Sequence activeSeq;
    private Sequence idleSeq;

    private bool hasPlayed;

    private readonly List<Vector3> baseScales = new List<Vector3>();
    private readonly List<CanvasGroup> itemCanvasGroups = new List<CanvasGroup>();
    private readonly Dictionary<RectTransform, Vector3> originalScaleCache = new Dictionary<RectTransform, Vector3>();

    private void OnEnable()
    {
        hasPlayed = false;
        CacheBase();

        if (playOnEnable)
        {
            Play();
        }
        else if (hideOnEnable)
        {
            ResetHidden();
        }
    }

    private void OnDisable()
    {
        Kill();
    }

    public float GetEntranceTotalDuration()
    {
        float spd = Mathf.Max(0.01f, speedMultiplier);
        int count = items != null ? items.Count : 0;
        if (count <= 0)
        {
            return Mathf.Max(0f, startDelay) / spd;
        }

        float lastStart = (count - 1) * Mathf.Max(0f, itemStagger) / spd;
        float settle = Mathf.Max(0f, itemDuration) / spd;
        if (overshootScale > 1f && overshootDuration > 0f)
        {
            settle += (overshootDuration * 2f) / spd;
        }

        return (Mathf.Max(0f, startDelay) / spd) + lastStart + settle;
    }

    public void Hide()
    {
        hasPlayed = false;
        Kill();
        CacheBase();
        ResetHidden();
    }

    public void Play()
    {
        if (hasPlayed) return;
        hasPlayed = true;

        float spd = Mathf.Max(0.01f, speedMultiplier);

        Kill();
        CacheBase();
        ResetHidden();

        activeSeq = DOTween.Sequence();
        activeSeq.SetAutoKill(true);

        if (startDelay > 0f)
        {
            activeSeq.AppendInterval(startDelay / spd);
        }

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = items[i];
            if (rt == null) continue;

            float st = (i * itemStagger) / spd;

            Vector3 baseScale = (i < baseScales.Count) ? baseScales[i] : Vector3.one;

            if (useCanvasGroupFade)
            {
                CanvasGroup cg = (i < itemCanvasGroups.Count) ? itemCanvasGroups[i] : null;
                if (cg != null)
                {
                    activeSeq.Insert(st, cg.DOFade(1f, fadeDuration / spd));
                }
            }

            activeSeq.Insert(st, rt.DOScale(baseScale, itemDuration / spd).SetEase(itemEase));

            if (overshootScale > 1f && overshootDuration > 0f)
            {
                float settleStart = st + (itemDuration / spd);
                activeSeq.Insert(settleStart, rt.DOScale(baseScale * overshootScale, overshootDuration / spd).SetEase(Ease.OutQuad));
                activeSeq.Insert(settleStart + (overshootDuration / spd), rt.DOScale(baseScale, overshootDuration / spd).SetEase(Ease.OutQuad));
            }
        }

        activeSeq.OnComplete(() =>
        {
            if (playIdlePulse)
            {
                PlayIdle();
            }
        });
    }

    public void Close(Action onComplete = null)
    {
        Kill();
        CacheBase();

        float spd = Mathf.Max(0.01f, speedMultiplier);

        activeSeq = DOTween.Sequence();
        activeSeq.SetAutoKill(true);

        if (startDelay > 0f)
        {
            activeSeq.AppendInterval(startDelay / spd);
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            RectTransform rt = items[i];
            if (rt == null) continue;

            int reverseIndex = (items.Count - 1) - i;
            float st = (reverseIndex * itemStagger) / spd;

            if (useCanvasGroupFade)
            {
                CanvasGroup cg = (i < itemCanvasGroups.Count) ? itemCanvasGroups[i] : null;
                if (cg != null)
                {
                    activeSeq.Insert(st, cg.DOFade(0f, fadeDuration / spd));
                }
            }

            activeSeq.Insert(st, rt.DOScale(Vector3.zero, itemDuration / spd).SetEase(Ease.InBack));
        }

        activeSeq.OnComplete(() =>
        {
            hasPlayed = false;
            onComplete?.Invoke();
        });
    }

    private void PlayIdle()
    {
        if (idleSeq != null)
        {
            idleSeq.Kill();
            idleSeq = null;
        }

        idleSeq = DOTween.Sequence();
        idleSeq.SetAutoKill(false);

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = items[i];
            if (rt == null) continue;

            Vector3 baseScale = (i < baseScales.Count) ? baseScales[i] : Vector3.one;

            float st = i * idlePulseStagger;
            float half = idlePulseDuration * 0.5f;
            idleSeq.Insert(st, rt.DOScale(baseScale * idlePulseScale, half).SetEase(Ease.InOutSine));
            idleSeq.Insert(st + half, rt.DOScale(baseScale, half).SetEase(Ease.InOutSine));
        }

        idleSeq.AppendInterval(Mathf.Max(0.05f, idlePulseStagger));
        idleSeq.SetLoops(-1, LoopType.Restart);
    }

    public void Kill()
    {
        if (activeSeq != null)
        {
            activeSeq.Kill();
            activeSeq = null;
        }

        if (idleSeq != null)
        {
            idleSeq.Kill();
            idleSeq = null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null) items[i].DOKill();
            if (useCanvasGroupFade && i < itemCanvasGroups.Count && itemCanvasGroups[i] != null) itemCanvasGroups[i].DOKill();
        }
    }

    private void CacheBase()
    {
        baseScales.Clear();
        itemCanvasGroups.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = items[i];
            if (rt == null)
            {
                baseScales.Add(Vector3.one);
                itemCanvasGroups.Add(null);
                continue;
            }

            Vector3 s;

            if (!originalScaleCache.TryGetValue(rt, out s))
            {
                s = rt.localScale;
                if (s.x > 0.0001f || s.y > 0.0001f)
                {
                    originalScaleCache[rt] = s;
                }
                else
                {
                    s = Vector3.one;
                }
            }

            baseScales.Add(s);

            if (useCanvasGroupFade)
            {
                CanvasGroup cg = rt.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = rt.gameObject.AddComponent<CanvasGroup>();
                }
                itemCanvasGroups.Add(cg);
            }
            else
            {
                itemCanvasGroups.Add(null);
            }
        }
    }

    private void ResetHidden()
    {
        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = items[i];
            if (rt == null) continue;

            rt.localScale = Vector3.zero;

            if (useCanvasGroupFade)
            {
                CanvasGroup cg = (i < itemCanvasGroups.Count) ? itemCanvasGroups[i] : null;
                if (cg != null)
                {
                    cg.alpha = 0f;
                }
            }
        }
    }
}
