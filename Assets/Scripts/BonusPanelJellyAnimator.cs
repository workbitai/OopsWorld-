using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class BonusPanelJellyAnimator : MonoBehaviour
{
    [Header("Targets (3 bonus objects)")]
    [SerializeField] private List<RectTransform> bonusItems = new List<RectTransform>();

    [Header("Auto Play")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private float startDelay = 0.05f;
    [SerializeField] private float itemStagger = 0.08f;

    [Header("Pop In")]
    [SerializeField] private float popDuration = 0.22f;
    [SerializeField] private Ease popEase = Ease.OutBack;
    [SerializeField] private float overshootScale = 1.12f;
    [SerializeField] private float overshootDuration = 0.08f;

    [Header("Jelly (Squash & Stretch)")]
    [SerializeField] private float squashStretchAmount = 0.18f;
    [SerializeField] private float squashStretchDuration = 0.14f;
    [SerializeField] private Ease squashStretchEase = Ease.OutQuad;

    [Header("Punch")]
    [SerializeField] private bool usePunchPosition = true;
    [SerializeField] private Vector2 punchPosition = new Vector2(0f, 14f);
    [SerializeField] private float punchPositionDuration = 0.25f;
    [SerializeField] private int punchVibrato = 10;
    [SerializeField] private float punchElasticity = 0.85f;

    [Header("Idle (Continuous React)")]
    [SerializeField] private bool playIdle = true;
    [SerializeField] private bool useInflateIdle = true;
    [SerializeField] private float idleStartDelay = 0.15f;
    [SerializeField] private float idleInflateScale = 1.06f;
    [SerializeField] private float idleInflateDuration = 0.18f;
    [SerializeField] private float idleSquashStretchAmount = 0.14f;
    [SerializeField] private float idleSquashStretchDuration = 0.16f;
    [SerializeField] private float idleSettleDuration = 0.18f;
    [SerializeField] private Ease idleEase = Ease.InOutSine;

    [Header("Idle Extra (Optional)")]
    [SerializeField] private bool idleUseRotate = false;
    [SerializeField] private float idleRotateAngle = 2f;
    [SerializeField] private float idleRotateDuration = 0.8f;
    [SerializeField] private bool idleUseBob = false;
    [SerializeField] private float idleBobY = 6f;
    [SerializeField] private float idleBobDuration = 0.75f;
    [SerializeField] private float idleStagger = 0.18f;

    private readonly Dictionary<RectTransform, Vector3> baseScaleCache = new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Vector2> baseAnchoredPosCache = new Dictionary<RectTransform, Vector2>();
    private Sequence seq;
    private Sequence idleSeq;

    private void OnEnable()
    {
        if (bonusItems == null || bonusItems.Count == 0)
        {
            TryAutoAssignBonusItems();
        }

        CacheBaseScales();

        if (playOnEnable)
        {
            Play();
        }
        else
        {
            ResetHidden();
        }
    }

    private void OnDisable()
    {
        Kill();
    }

    public void Play()
    {
        Kill();
        CacheBaseScales();
        ResetHidden();

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        if (startDelay > 0f)
        {
            seq.AppendInterval(startDelay);
        }

        int count = bonusItems != null ? bonusItems.Count : 0;
        for (int i = 0; i < count; i++)
        {
            RectTransform rt = bonusItems[i];
            if (rt == null) continue;

            float st = i * Mathf.Max(0f, itemStagger);

            Vector3 baseScale = Vector3.one;
            if (!baseScaleCache.TryGetValue(rt, out baseScale))
            {
                baseScale = rt.localScale;
                baseScaleCache[rt] = baseScale;
            }

            Vector3 startScale = baseScale * 0.01f;
            seq.Insert(st, rt.DOScale(baseScale, Mathf.Max(0.01f, popDuration)).SetEase(popEase));

            // Jelly: squash (wide + short) then stretch (narrow + tall) then settle.
            float jellyStart = st + Mathf.Max(0.01f, popDuration) * 0.30f;
            float amt = Mathf.Clamp(squashStretchAmount, 0f, 0.6f);
            float jDur = Mathf.Max(0.01f, squashStretchDuration);

            seq.Insert(jellyStart, rt.DOScale(new Vector3(baseScale.x * (1f + amt), baseScale.y * (1f - amt), baseScale.z), jDur).SetEase(squashStretchEase));
            seq.Insert(jellyStart + jDur, rt.DOScale(new Vector3(baseScale.x * (1f - amt), baseScale.y * (1f + amt), baseScale.z), jDur).SetEase(squashStretchEase));

            if (overshootScale > 1f && overshootDuration > 0f)
            {
                float settleStart = st + Mathf.Max(0.01f, popDuration);
                float oDur = Mathf.Max(0.01f, overshootDuration);
                seq.Insert(settleStart, rt.DOScale(baseScale * overshootScale, oDur).SetEase(Ease.OutQuad));
                seq.Insert(settleStart + oDur, rt.DOScale(baseScale, oDur).SetEase(Ease.OutQuad));
            }
            else
            {
                float settle2Start = jellyStart + (jDur * 2f);
                seq.Insert(settle2Start, rt.DOScale(baseScale, jDur).SetEase(squashStretchEase));
            }

            if (usePunchPosition)
            {
                seq.Insert(st, rt.DOPunchAnchorPos(punchPosition, Mathf.Max(0.01f, punchPositionDuration), Mathf.Max(1, punchVibrato), Mathf.Clamp01(punchElasticity)));
            }
        }

        seq.OnComplete(() =>
        {
            if (playIdle)
            {
                PlayIdle();
            }
        });
    }

    public void Hide()
    {
        Kill();
        CacheBaseScales();
        ResetHidden();
    }

    public void StopIdle()
    {
        if (idleSeq != null)
        {
            idleSeq.Kill();
            idleSeq = null;
        }
    }

    public void StopAllItemTweens()
    {
        StopIdle();
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }

        if (bonusItems == null) return;
        for (int i = 0; i < bonusItems.Count; i++)
        {
            if (bonusItems[i] != null) bonusItems[i].DOKill();
        }
    }

    public void StopAndRestoreBaseImmediate()
    {
        if (bonusItems == null || bonusItems.Count == 0)
        {
            TryAutoAssignBonusItems();
        }

        CacheBaseScales();
        StopAllItemTweens();

        if (bonusItems == null) return;
        for (int i = 0; i < bonusItems.Count; i++)
        {
            RectTransform rt = bonusItems[i];
            if (rt == null) continue;

            Vector3 s;
            if (!baseScaleCache.TryGetValue(rt, out s)) s = Vector3.one;
            if (s.x < 0.0001f && s.y < 0.0001f) s = Vector3.one;
            rt.localScale = s;

            Vector2 p;
            if (baseAnchoredPosCache.TryGetValue(rt, out p)) rt.anchoredPosition = p;
        }
    }

    public void Kill()
    {
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }

        if (idleSeq != null)
        {
            idleSeq.Kill();
            idleSeq = null;
        }

        if (bonusItems == null) return;
        for (int i = 0; i < bonusItems.Count; i++)
        {
            if (bonusItems[i] != null) bonusItems[i].DOKill();
        }
    }

    private void CacheBaseScales()
    {
        if (bonusItems == null) return;
        for (int i = 0; i < bonusItems.Count; i++)
        {
            RectTransform rt = bonusItems[i];
            if (rt == null) continue;

            if (!baseScaleCache.ContainsKey(rt))
            {
                Vector3 s = rt.localScale;
                if (s.x < 0.0001f && s.y < 0.0001f) s = Vector3.one;
                baseScaleCache[rt] = s;
            }

            if (!baseAnchoredPosCache.ContainsKey(rt))
            {
                baseAnchoredPosCache[rt] = rt.anchoredPosition;
            }
        }
    }

    private void ResetHidden()
    {
        if (bonusItems == null) return;

        for (int i = 0; i < bonusItems.Count; i++)
        {
            RectTransform rt = bonusItems[i];
            if (rt == null) continue;

            Vector3 baseScale;
            if (!baseScaleCache.TryGetValue(rt, out baseScale)) baseScale = Vector3.one;
            rt.localScale = baseScale * 0.01f;

            Vector2 basePos;
            if (!baseAnchoredPosCache.TryGetValue(rt, out basePos)) basePos = rt.anchoredPosition;
            rt.anchoredPosition = basePos;
        }
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

        float baseDelay = Mathf.Max(0f, idleStartDelay);
        if (baseDelay > 0f)
        {
            idleSeq.AppendInterval(baseDelay);
        }

        int count = bonusItems != null ? bonusItems.Count : 0;
        for (int i = 0; i < count; i++)
        {
            RectTransform rt = bonusItems[i];
            if (rt == null) continue;

            Vector3 baseScale;
            if (!baseScaleCache.TryGetValue(rt, out baseScale)) baseScale = Vector3.one;

            Vector2 basePos;
            if (!baseAnchoredPosCache.TryGetValue(rt, out basePos)) basePos = rt.anchoredPosition;

            float st = i * Mathf.Max(0f, idleStagger);

            float inflate = Mathf.Max(1f, idleInflateScale);
            float inflateDur = Mathf.Max(0.05f, idleInflateDuration);
            float amt = Mathf.Clamp(idleSquashStretchAmount, 0f, 0.6f);
            float ssDur = Mathf.Max(0.05f, idleSquashStretchDuration);
            float settleDur = Mathf.Max(0.05f, idleSettleDuration);

            float startT = baseDelay + st;

            if (useInflateIdle)
            {
                // Inflate (inside pressure) -> squash -> stretch -> settle.
                idleSeq.Insert(startT, rt.DOScale(baseScale * inflate, inflateDur).SetEase(idleEase));

                idleSeq.Insert(startT + inflateDur,
                    rt.DOScale(new Vector3(baseScale.x * (1f + amt), baseScale.y * (1f - amt), baseScale.z), ssDur).SetEase(idleEase));

                idleSeq.Insert(startT + inflateDur + ssDur,
                    rt.DOScale(new Vector3(baseScale.x * (1f - amt), baseScale.y * (1f + amt), baseScale.z), ssDur).SetEase(idleEase));

                idleSeq.Insert(startT + inflateDur + (ssDur * 2f), rt.DOScale(baseScale, settleDur).SetEase(idleEase));
            }
            else
            {
                // Fallback simple pulse.
                idleSeq.Insert(startT, rt.DOScale(baseScale * inflate, inflateDur).SetEase(idleEase).SetLoops(2, LoopType.Yoyo));
            }

            if (idleUseRotate)
            {
                float ang = Mathf.Abs(idleRotateAngle);
                float rDur = Mathf.Max(0.05f, idleRotateDuration);
                idleSeq.Insert(startT, rt.DORotate(new Vector3(0f, 0f, ang), rDur).SetEase(idleEase).SetLoops(2, LoopType.Yoyo));
            }

            if (idleUseBob)
            {
                float bob = Mathf.Abs(idleBobY);
                float bDur = Mathf.Max(0.05f, idleBobDuration);
                idleSeq.Insert(startT, rt.DOAnchorPosY(basePos.y + bob, bDur).SetEase(idleEase).SetLoops(2, LoopType.Yoyo));
            }
        }

        float loopLen = idleInflateDuration + (idleSquashStretchDuration * 2f) + idleSettleDuration;
        if (!useInflateIdle) loopLen = Mathf.Max(loopLen, idleInflateDuration * 2f);
        if (idleUseRotate) loopLen = Mathf.Max(loopLen, idleRotateDuration);
        if (idleUseBob) loopLen = Mathf.Max(loopLen, idleBobDuration);

        idleSeq.AppendInterval(Mathf.Max(0.05f, loopLen + idleStagger));
        idleSeq.SetLoops(-1, LoopType.Restart);
    }

    private void TryAutoAssignBonusItems()
    {
        if (bonusItems == null)
        {
            bonusItems = new List<RectTransform>();
        }
        bonusItems.Clear();

        RectTransform b1 = FindChildRectTransformByName("Bonus 1");
        RectTransform b2 = FindChildRectTransformByName("Bonus 2");
        RectTransform b3 = FindChildRectTransformByName("Bonus 3");

        if (b1 != null) bonusItems.Add(b1);
        if (b2 != null) bonusItems.Add(b2);
        if (b3 != null) bonusItems.Add(b3);

        baseAnchoredPosCache.Clear();
    }

    private RectTransform FindChildRectTransformByName(string childName)
    {
        if (string.IsNullOrEmpty(childName)) return null;

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            if (!string.Equals(t.name, childName, System.StringComparison.OrdinalIgnoreCase)) continue;
            return t as RectTransform;
        }
        return null;
    }
}
