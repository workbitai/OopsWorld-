using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class BonusCoinBurstVfx : MonoBehaviour
{
    [Header("Prefab & Parenting")]
    [SerializeField] private RectTransform coinPrefab;
    [SerializeField] private RectTransform spawnParent;
    [SerializeField] private string spawnParentChildNameUnderSource = "coinParent";
    [SerializeField] private RectTransform randomTargetArea;

    [Header("Count")]
    [SerializeField] private int minCoins = 10;
    [SerializeField] private int maxCoins = 12;

    [Header("Motion")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private Ease flyEase = Ease.OutCubic;
    [SerializeField] private float scatterRadius = 220f;
    [SerializeField] private float startPunchRadius = 35f;
    [SerializeField] private float spawnStagger = 0.02f;

    [Header("Scale")]
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 1f;
    [SerializeField] private float scaleDuration = 0.18f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Move To Target & Destroy (Optional)")]
    [SerializeField] private RectTransform destroyTarget;
    [SerializeField] private bool moveToTargetAndDestroy = false;
    [SerializeField] private float moveToTargetDelay = 0.10f;
    [SerializeField] private float moveToTargetDuration = 0.35f;
    [SerializeField] private Ease moveToTargetEase = Ease.InOutQuad;

    [Header("Rotation")]
    [SerializeField] private bool rotateCoins = true;
    [SerializeField] private float rotateMin = -360f;
    [SerializeField] private float rotateMax = 360f;

    [Header("Reward / Counting (Optional)")]
    [SerializeField] private List<int> rewardValues = new List<int>();
    [SerializeField] private TMP_Text coinFlyingCountText;
    [SerializeField] private string coinFlyingPrefix = "+";
    [SerializeField] private float coinFlyingCountStartDelay = 0f;
    [SerializeField] private float coinFlyingCountDuration = 0.55f;

    [Header("Target Move + Add (Optional)")]
    [SerializeField] private RectTransform targetContainerToMove;
    [SerializeField] private List<RectTransform> targetPoints = new List<RectTransform>();
    [SerializeField] private TMP_Text targetValueText;
    [SerializeField] private float afterCoinsDelaySeconds = 0.15f;
    [SerializeField] private float moveTargetToNextDuration = 0.35f;
    [SerializeField] private Ease moveTargetToNextEase = Ease.InOutQuad;
    [SerializeField] private bool scaleDownWhileMovingToNext = true;
    [SerializeField] private Ease moveToNextScaleEase = Ease.InQuad;
    [SerializeField] private bool fadeOutWhileMovingToNext = false;
    [SerializeField] private Ease moveToNextFadeEase = Ease.InQuad;
    [SerializeField] private float targetAddCountDuration = 0.45f;
    [SerializeField] private string targetValuePrefix = "";

    [Header("Target Container Return (Optional)")]
    [SerializeField] private RectTransform returnTarget;
    [SerializeField] private float returnDelaySeconds = 3f;
    [SerializeField] private float returnMoveDuration = 0.35f;
    [SerializeField] private Ease returnMoveEase = Ease.InOutQuad;
    [SerializeField] private bool returnFadeOut = true;
    [SerializeField] private Ease returnFadeEase = Ease.InQuad;

    public event Action OnAllCoinsDestroyed;

    private int activeCoins;
    private int pickedReward;
    private int targetIndex;
    private Tween flyingCountTween;
    private Tween targetAddTween;
    private Tween returnTween;
    private CanvasGroup targetContainerCanvasGroup;

    public void PlayBurst(RectTransform source)
    {
        RectTransform resolvedParent = ResolveSpawnParent(source);
        PlayBurst(source, resolvedParent);
    }

    private CanvasGroup EnsureTargetContainerCanvasGroup()
    {
        if (targetContainerToMove == null) return null;

        if (targetContainerCanvasGroup == null)
        {
            targetContainerCanvasGroup = targetContainerToMove.GetComponent<CanvasGroup>();
            if (targetContainerCanvasGroup == null)
            {
                targetContainerCanvasGroup = targetContainerToMove.gameObject.AddComponent<CanvasGroup>();
            }
        }

        return targetContainerCanvasGroup;
    }

    public void PlayBurst(RectTransform source, RectTransform overrideSpawnParent)
    {
        if (source == null) return;
        if (coinPrefab == null)
        {
            Debug.LogWarning("BonusCoinBurstVfx: coinPrefab not assigned.");
            return;
        }
        if (overrideSpawnParent == null)
        {
            Debug.LogWarning($"BonusCoinBurstVfx: spawnParent not resolved. source='{source.name}' childName='{spawnParentChildNameUnderSource}'");
            return;
        }

        int count = UnityEngine.Random.Range(Mathf.Max(1, minCoins), Mathf.Max(1, maxCoins) + 1);
        activeCoins = Mathf.Max(0, count);

        pickedReward = PickRewardValue();
        StartFlyingCount(pickedReward, count);

        ActivateTargetContainer();

        if (activeCoins == 0)
        {
            HandleAllCoinsDestroyed();
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float delay = i * Mathf.Max(0f, spawnStagger);
            SpawnOne(source, overrideSpawnParent, delay);
        }
    }

    private void SpawnOne(RectTransform source, RectTransform resolvedParent, float delay)
    {
        RectTransform coin = Instantiate(coinPrefab, resolvedParent);
        coin.gameObject.SetActive(true);

        // Ensure coins render above everything in the parent.
        coin.SetAsLastSibling();

        // Place at the resolved parent center (coinParent center) in its own local space.
        Vector3 originWorld = GetRectTransformCenterWorld(resolvedParent);
        Vector3 local3 = resolvedParent.InverseTransformPoint(originWorld);
        Vector2 localStart = new Vector2(local3.x, local3.y);

        coin.anchoredPosition = localStart;
        coin.localScale = Vector3.one * Mathf.Max(0.01f, startScale);

        Vector2 localTarget = GetRandomTargetLocal(resolvedParent, localStart);

        coin.DOKill();

        Sequence s = DOTween.Sequence();
        s.SetDelay(Mathf.Max(0f, delay));

        // Small initial punch outward so it feels like it pops from the bag.
        Vector2 punch = (localTarget - localStart).normalized * Mathf.Max(0f, startPunchRadius);
        s.Append(coin.DOAnchorPos(localStart + punch, 0.10f).SetEase(Ease.OutQuad));

        s.Append(coin.DOAnchorPos(localTarget, Mathf.Max(0.01f, flyDuration)).SetEase(flyEase));

        float sDur = Mathf.Max(0.01f, scaleDuration);
        s.Join(coin.DOScale(Vector3.one * Mathf.Max(0.01f, endScale), sDur).SetEase(scaleEase));

        if (rotateCoins)
        {
            float z = UnityEngine.Random.Range(rotateMin, rotateMax);
            s.Join(coin.DORotate(new Vector3(0f, 0f, z), Mathf.Max(0.01f, flyDuration)).SetEase(Ease.OutCubic));
        }

        if (moveToTargetAndDestroy && destroyTarget != null)
        {
            float d = Mathf.Max(0f, moveToTargetDelay);
            float dur = Mathf.Max(0.01f, moveToTargetDuration);

            Vector3 targetWorld = GetRectTransformCenterWorld(destroyTarget);
            s.AppendInterval(d);
            s.Append(coin.DOMove(targetWorld, dur).SetEase(moveToTargetEase));
        }

        s.OnComplete(() =>
        {
            if (coin != null)
            {
                Destroy(coin.gameObject);
            }

            activeCoins = Mathf.Max(0, activeCoins - 1);
            if (activeCoins == 0)
            {
                HandleAllCoinsDestroyed();
            }
        });

    }

    private void HandleAllCoinsDestroyed()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(AfterCoinsSequence());
        }
        else
        {
            OnAllCoinsDestroyed?.Invoke();
        }
    }

    private IEnumerator AfterCoinsSequence()
    {
        float d = Mathf.Max(0f, afterCoinsDelaySeconds);
        if (d > 0f) yield return new WaitForSeconds(d);

        if (targetContainerToMove != null && targetPoints != null && targetPoints.Count > 0)
        {
            RectTransform next = GetNextTargetPoint();
            if (next != null)
            {
                Vector3 nextWorld = GetRectTransformCenterWorld(next);
                float dur = Mathf.Max(0.01f, moveTargetToNextDuration);

                Sequence s = DOTween.Sequence();
                s.Append(targetContainerToMove.DOMove(nextWorld, dur).SetEase(moveTargetToNextEase));

                if (scaleDownWhileMovingToNext)
                {
                    s.Join(targetContainerToMove.DOScale(Vector3.zero, dur).SetEase(moveToNextScaleEase));
                }

                if (fadeOutWhileMovingToNext)
                {
                    CanvasGroup cg = EnsureTargetContainerCanvasGroup();
                    if (cg != null)
                    {
                        s.Join(cg.DOFade(0f, dur).SetEase(moveToNextFadeEase));
                    }
                }

                yield return s.WaitForCompletion();

                if (scaleDownWhileMovingToNext)
                {
                    targetContainerToMove.localScale = Vector3.zero;
                }

                if (fadeOutWhileMovingToNext && targetContainerCanvasGroup != null)
                {
                    targetContainerCanvasGroup.alpha = 0f;
                }
            }
        }

        if (targetValueText != null && pickedReward > 0)
        {
            int current = ExtractInt(targetValueText.text);
            int end = current + pickedReward;

            if (targetAddTween != null && targetAddTween.IsActive()) targetAddTween.Kill();

            int v = current;
            targetValueText.text = targetValuePrefix + v;

            targetAddTween = DOTween.To(() => v, x =>
            {
                v = x;
                if (targetValueText != null) targetValueText.text = targetValuePrefix + v;
            }, end, Mathf.Max(0.01f, targetAddCountDuration)).SetEase(Ease.Linear);

            yield return targetAddTween.WaitForCompletion();

            GameWalletApi.CreditUpdateCoins(pickedReward, refreshWalletAfter: false);
        }

        if (targetContainerToMove != null)
        {
            float rd = Mathf.Max(0f, returnDelaySeconds);
            if (rd > 0f) yield return new WaitForSeconds(rd);

            RectTransform rt = returnTarget != null ? returnTarget : destroyTarget;
            if (rt != null)
            {
                if (returnTween != null && returnTween.IsActive()) returnTween.Kill();

                Vector3 targetWorld = GetRectTransformCenterWorld(rt);
                float dur = Mathf.Max(0.01f, returnMoveDuration);

                Sequence s = DOTween.Sequence();
                s.Append(targetContainerToMove.DOMove(targetWorld, dur).SetEase(returnMoveEase));
                s.Join(targetContainerToMove.DOScale(Vector3.zero, dur).SetEase(Ease.InQuad));

                if (returnFadeOut)
                {
                    CanvasGroup cg = EnsureTargetContainerCanvasGroup();
                    if (cg != null)
                    {
                        s.Join(cg.DOFade(0f, dur).SetEase(returnFadeEase));
                    }
                }
                returnTween = s;
                yield return s.WaitForCompletion();
            }

            targetContainerToMove.localScale = Vector3.zero;

            if (targetContainerCanvasGroup != null)
            {
                targetContainerCanvasGroup.alpha = 0f;
            }

            targetContainerToMove.gameObject.SetActive(false);
        }

        OnAllCoinsDestroyed?.Invoke();
    }

    private void ActivateTargetContainer()
    {
        if (targetContainerToMove == null) return;

        if (returnTween != null && returnTween.IsActive()) returnTween.Kill();

        targetContainerToMove.gameObject.SetActive(true);
        targetContainerToMove.localScale = Vector3.one;

        if (returnFadeOut)
        {
            CanvasGroup cg = EnsureTargetContainerCanvasGroup();
            if (cg != null) cg.alpha = 1f;
        }

        if (targetValueText != null)
        {
            int current = ExtractInt(targetValueText.text);
            targetValueText.text = targetValuePrefix + current;
        }
    }

    private int PickRewardValue()
    {
        if (rewardValues == null || rewardValues.Count == 0) return 0;
        int idx = UnityEngine.Random.Range(0, rewardValues.Count);
        return Mathf.Max(0, rewardValues[idx]);
    }

    private void StartFlyingCount(int reward, int coinCount)
    {
        if (coinFlyingCountText == null) return;
        if (reward <= 0) { coinFlyingCountText.text = string.Empty; return; }

        if (flyingCountTween != null && flyingCountTween.IsActive()) flyingCountTween.Kill();

        float dur = coinFlyingCountDuration;
        if (dur <= 0f)
        {
            dur = EstimateCoinToTargetTotalDurationSeconds(coinCount);
        }

        int v = 0;
        coinFlyingCountText.text = coinFlyingPrefix + v;

        flyingCountTween = DOTween.To(() => v, x =>
        {
            v = x;
            if (coinFlyingCountText != null) coinFlyingCountText.text = coinFlyingPrefix + v;
        }, reward, Mathf.Max(0.01f, dur))
            .SetDelay(Mathf.Max(0f, coinFlyingCountStartDelay))
            .SetEase(Ease.Linear);
    }

    private float EstimateCoinToTargetTotalDurationSeconds(int coinCount)
    {
        int c = Mathf.Max(1, coinCount);
        float lastDelay = (c - 1) * Mathf.Max(0f, spawnStagger);
        float baseMotion = 0.10f + Mathf.Max(0.01f, flyDuration);
        float toTarget = 0f;
        if (moveToTargetAndDestroy && destroyTarget != null)
        {
            toTarget = Mathf.Max(0f, moveToTargetDelay) + Mathf.Max(0.01f, moveToTargetDuration);
        }
        return lastDelay + baseMotion + toTarget;
    }

    private RectTransform GetNextTargetPoint()
    {
        if (targetPoints == null || targetPoints.Count == 0) return null;
        if (targetIndex < 0) targetIndex = 0;
        if (targetIndex >= targetPoints.Count) targetIndex = 0;
        RectTransform rt = targetPoints[targetIndex];
        targetIndex = (targetIndex + 1) % targetPoints.Count;
        return rt;
    }

    private int ExtractInt(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;

        int val = 0;
        bool any = false;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch < '0' || ch > '9') continue;
            any = true;
            val = (val * 10) + (ch - '0');
        }
        return any ? val : 0;
    }

    private Vector2 GetRandomTargetLocal(RectTransform resolvedParent, Vector2 localStart)
    {
        if (resolvedParent == null) return localStart;

        if (randomTargetArea != null)
        {
            Rect r = randomTargetArea.rect;
            float x = UnityEngine.Random.Range(r.xMin, r.xMax);
            float y = UnityEngine.Random.Range(r.yMin, r.yMax);
            Vector3 w = randomTargetArea.TransformPoint(new Vector3(x, y, 0f));

            Vector3 local3 = resolvedParent.InverseTransformPoint(w);
            return new Vector2(local3.x, local3.y);
        }

        // Fallback: random scatter around start.
        Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
        float dist = UnityEngine.Random.Range(scatterRadius * 0.55f, scatterRadius);
        return localStart + (dir * dist);
    }

    private RectTransform ResolveSpawnParent(RectTransform source)
    {
        if (source == null) return spawnParent;

        if (!string.IsNullOrEmpty(spawnParentChildNameUnderSource))
        {
            Transform t = FindChildRecursiveByName(source, spawnParentChildNameUnderSource);
            if (t != null)
            {
                RectTransform rt = t as RectTransform;
                if (rt != null) return rt;
            }
        }

        return spawnParent;
    }

    private Vector3 GetRectTransformCenterWorld(RectTransform rt)
    {
        if (rt == null) return Vector3.zero;

        // Center of the rect in local space, then convert to world.
        Vector3 localCenter = new Vector3(rt.rect.center.x, rt.rect.center.y, 0f);
        return rt.TransformPoint(localCenter);
    }

    private Transform FindChildRecursiveByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c == null) continue;
            if (string.Equals(c.name, targetName, System.StringComparison.OrdinalIgnoreCase)) return c;

            Transform nested = FindChildRecursiveByName(c, targetName);
            if (nested != null) return nested;
        }
        return null;
    }
}
