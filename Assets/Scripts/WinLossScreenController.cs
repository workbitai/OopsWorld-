using System.Collections.Generic;
using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinLossScreenController : MonoBehaviour
{
    [Header("Top")]
    [SerializeField] private Image winLossBannerImage;
    [SerializeField] private Sprite winBannerSprite;
    [SerializeField] private Sprite loseBannerSprite;

    [Header("Rewards")]
    [SerializeField] private GameObject winningCoinImage;
    [SerializeField] private GameObject winningDiamondImage;
    [SerializeField] private GameObject winningStarImage;
    [SerializeField] private TMP_Text winningCoinText;
    [SerializeField] private TMP_Text winningDiamondText;
    [SerializeField] private TMP_Text winningStarText;
    [SerializeField] private Sprite offlineStarSprite;

    [Header("Bottom")]
    [SerializeField] private Image winnerImage;
    [SerializeField] private Image loserImage;

    [Header("Sprite Mapping")]
    [SerializeField] private bool preferPlayerNumberSpritesForLocalWin = true;

    [Header("Sprites")]
    [SerializeField] private List<Sprite> winnerSpritesByPlayerNumber = new List<Sprite>();
    [SerializeField] private Sprite loserBlueSprite;

    [Header("Open Animation")]
    [SerializeField] private bool playOpenAnimation = true;
    [SerializeField] private bool refreshCachedValuesOnOpen = true;
    [SerializeField] private float openDelay = 0f;
    [SerializeField] private float bannerFromOffsetY = 120f;
    [SerializeField] private float rewardsFromOffsetY = -90f;
    [SerializeField] private float bottomFromOffsetY = -220f;
    [SerializeField] private float extraItemsFromOffsetY = -120f;
    [SerializeField] private float itemDuration = 0.28f;
    [SerializeField] private float itemStagger = 0.06f;
    [SerializeField] private Ease itemEase = Ease.OutBack;
    [SerializeField] private bool fadeItems = true;
    [SerializeField] private List<RectTransform> extraAnimatedItems = new List<RectTransform>();

    [Header("VFX")]
    [SerializeField] private GameObject[] vfxRootsToEnableOnOpen;
    [SerializeField] private ParticleSystem[] particleSystemsToPlayOnOpen;

    [Header("Reward Transfer Animation")]
    [SerializeField] private bool playRewardTransferOnOpen = false;
    [SerializeField] private RectTransform coinPrefab;
    [SerializeField] private RectTransform diamondPrefab;
    [SerializeField] private RectTransform starPrefab;
    [SerializeField] private RectTransform spawnRoot;
    [SerializeField] private RectTransform coinSpawnFrom;
    [SerializeField] private RectTransform diamondSpawnFrom;
    [SerializeField] private RectTransform starSpawnFrom;
    [SerializeField] private RectTransform coinTarget;
    [SerializeField] private RectTransform diamondTarget;
    [SerializeField] private RectTransform starTarget;
    [SerializeField] private TMP_Text coinTargetValueText;
    [SerializeField] private TMP_Text diamondTargetValueText;
    [SerializeField] private TMP_Text starTargetValueText;

    [Header("Optional HUD Roots")]
    [SerializeField] private GameObject coinHudRoot;
    [SerializeField] private GameObject diamondHudRoot;
    [SerializeField] private GameObject starHudRoot;
    [SerializeField] private int burstCount = 10;
    [SerializeField] private float burstStagger = 0.03f;
    [SerializeField] private float transferStartDelay = 0.05f;
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private float curveHeight = 160f;
    [SerializeField] private float curveSideOffset = 0f;
    [SerializeField] private Ease flyEase = Ease.OutCubic;

    private Sequence openSeq;
    private Sequence transferSeq;

    private struct RectBaseState
    {
        public Vector2 anchoredPos;
        public Vector3 localScale;
        public CanvasGroup canvasGroup;
        public float alpha;
    }

    private readonly Dictionary<RectTransform, RectBaseState> baseStates = new Dictionary<RectTransform, RectBaseState>();
    private Coroutine openRoutine;
    private readonly List<RectTransform> spawnedRewardItems = new List<RectTransform>();
    private int cachedWinCoinAmount;
    private int cachedWinDiamondAmount;
    private bool cachedLocalWon;

    private string lastCreditedMatchKey;

    public void ShowResult(int winnerPlayerNumber, int localPlayerNumber, long winCoinAmount, long winDiamondAmount, Sprite localWinnerSprite)
    {
        int local = Mathf.Clamp(localPlayerNumber, 1, 4);
        int winner = Mathf.Clamp(winnerPlayerNumber, 1, 4);
        bool localWon = winner == local;

        GameManager gm = GameManager.Instance;
        bool offlineStarMatch = gm != null && gm.CurrentMatchUsesOfflineStarRewards;

        if (offlineStarMatch)
        {
            if (coinHudRoot != null) coinHudRoot.SetActive(false);
            if (diamondHudRoot != null) diamondHudRoot.SetActive(false);
            if (starHudRoot != null) starHudRoot.SetActive(true);
        }
        else
        {
            if (coinHudRoot != null) coinHudRoot.SetActive(true);
            if (diamondHudRoot != null) diamondHudRoot.SetActive(true);
            if (starHudRoot != null) starHudRoot.SetActive(false);
        }

        SetActiveSafe(winningCoinImage, localWon && !offlineStarMatch);
        SetActiveSafe(winningDiamondImage, localWon && !offlineStarMatch);
        SetActiveSafe(winningStarImage, localWon && offlineStarMatch);

        if (offlineStarMatch && winningStarImage != null && offlineStarSprite != null)
        {
            Image img = winningStarImage.GetComponent<Image>();
            if (img != null) img.sprite = offlineStarSprite;
        }

        cachedLocalWon = localWon;
        cachedWinCoinAmount = ClampToInt(winCoinAmount);
        cachedWinDiamondAmount = ClampToInt(winDiamondAmount);

        if (!playRewardTransferOnOpen)
        {
            TryCreditWinRewards(localWon, cachedWinCoinAmount, cachedWinDiamondAmount);
        }

        if (winLossBannerImage != null)
        {
            Sprite s = localWon ? winBannerSprite : loseBannerSprite;
            if (s != null)
            {
                winLossBannerImage.sprite = s;
            }
        }

        if (winningCoinText != null)
        {
            winningCoinText.text = (localWon && !offlineStarMatch) ? winCoinAmount.ToString() : string.Empty;
            winningCoinText.gameObject.SetActive(localWon && !offlineStarMatch);
        }

        if (winningStarText != null)
        {
            winningStarText.text = (localWon && offlineStarMatch) ? winCoinAmount.ToString() : string.Empty;
            winningStarText.gameObject.SetActive(localWon && offlineStarMatch);
        }

        if (winningDiamondText != null)
        {
            winningDiamondText.text = (localWon && !offlineStarMatch) ? winDiamondAmount.ToString() : string.Empty;
            winningDiamondText.gameObject.SetActive(localWon && !offlineStarMatch);
        }

        if (winnerImage != null)
        {
            Sprite winnerSprite;
            if (localWon)
            {
                winnerSprite = preferPlayerNumberSpritesForLocalWin
                    ? GetWinnerSpriteByNumber(local)
                    : (localWinnerSprite != null ? localWinnerSprite : GetWinnerSpriteByNumber(local));
            }
            else
            {
                winnerSprite = GetWinnerSpriteByNumber(winner);
            }
            if (winnerSprite != null)
            {
                winnerImage.sprite = winnerSprite;
            }
            winnerImage.gameObject.SetActive(true);
        }

        if (loserImage != null)
        {
            if (localWon)
            {
                loserImage.gameObject.SetActive(false);
            }
            else
            {
                if (loserBlueSprite != null)
                {
                    loserImage.sprite = loserBlueSprite;
                }
                loserImage.gameObject.SetActive(true);
            }
        }
    }

    private void TryCreditWinRewards(bool localWon, int coinAmount, int diamondAmount)
    {
        if (!localWon) return;

        if (coinAmount <= 0 && diamondAmount <= 0) return;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        string roomId = gm.CurrentRoomId;
        string matchKey = !string.IsNullOrEmpty(roomId) ? $"room:{roomId}" : $"local:{gm.MatchNonce}";

        if (string.Equals(lastCreditedMatchKey, matchKey, System.StringComparison.Ordinal))
        {
            return;
        }

        lastCreditedMatchKey = matchKey;

        if (gm.CurrentMatchUsesOfflineStarRewards)
        {
            if (coinAmount > 0)
            {
                if (PlayerWallet.Instance != null) PlayerWallet.Instance.AddOfflineStars(coinAmount);
                else PlayerWallet.SetOfflineStarsForCurrentUser(PlayerWallet.GetOfflineStarsForCurrentUser() + coinAmount);
            }
            return;
        }

        GameWalletApi.CreditUpdate(
            coinsAmount: coinAmount > 0 ? coinAmount : null,
            diamonds: diamondAmount > 0 ? diamondAmount : null,
            onSuccess: null,
            onError: error =>
            {
                Debug.LogWarning($"WinLossScreenController: reward credit failed: {error}");
            },
            refreshWalletAfter: true
        );
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!playOpenAnimation) return;

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }
        openRoutine = StartCoroutine(PlayOpenNextFrame());
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        KillOpenTweens();
        KillTransferTweens();
        StopVfx();

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }
    }

    private IEnumerator PlayOpenNextFrame()
    {
        yield return new WaitForEndOfFrame();

        if (!isActiveAndEnabled) yield break;

        Canvas.ForceUpdateCanvases();
        PlayOpenAnimation();
        openRoutine = null;
    }

    private void PlayOpenAnimation()
    {
        KillOpenTweens();

        KillTransferTweens();

        RestoreToCachedBases();
        if (refreshCachedValuesOnOpen)
        {
            CacheBases();
        }
        else if (baseStates.Count == 0)
        {
            CacheBases();
        }

        SetHiddenStartState();

        PlayVfx();

        float dur = Mathf.Max(0.01f, itemDuration);
        float stg = Mathf.Max(0f, itemStagger);

        openSeq = DOTween.Sequence();
        openSeq.SetAutoKill(true);

        if (openDelay > 0f)
        {
            openSeq.AppendInterval(openDelay);
        }

        float baseTime = openSeq.Duration();

        float t = 0f;
        AppendTarget(GetRectTransform(winLossBannerImage), baseTime + t, dur);
        t += stg;

        AppendTarget(GetRectTransform(winningCoinImage), baseTime + t, dur);
        t += stg;
        AppendTarget(GetRectTransform(winningDiamondImage), baseTime + t, dur);
        t += stg;
        AppendTarget(GetRectTransform(winningStarImage), baseTime + t, dur);
        t += stg;

        AppendTarget(GetRectTransform(winningCoinText), baseTime + t, dur);
        t += stg;
        AppendTarget(GetRectTransform(winningDiamondText), baseTime + t, dur);
        t += stg;
        AppendTarget(GetRectTransform(winningStarText), baseTime + t, dur);
        t += stg;

        AppendTarget(GetRectTransform(winnerImage), baseTime + t, dur);
        t += stg;
        AppendTarget(GetRectTransform(loserImage), baseTime + t, dur);

        if (extraAnimatedItems != null)
        {
            float extraBase = baseTime + t + stg;
            for (int i = 0; i < extraAnimatedItems.Count; i++)
            {
                RectTransform rt = extraAnimatedItems[i];
                if (rt == null) continue;
                AppendTarget(rt, extraBase + (i * stg), dur);
            }
        }

        openSeq.OnComplete(() =>
        {
            if (!isActiveAndEnabled) return;
            if (playRewardTransferOnOpen)
            {
                PlayRewardTransfer();
            }
        });
    }

    private void AppendTarget(RectTransform rt, float insertAt, float duration)
    {
        if (rt == null) return;
        if (!rt.gameObject.activeInHierarchy) return;
        if (!baseStates.TryGetValue(rt, out RectBaseState b)) return;

        rt.DOKill();
        if (b.canvasGroup != null) b.canvasGroup.DOKill();

        openSeq.Insert(insertAt, rt.DOScale(b.localScale, duration).SetEase(itemEase));
        openSeq.Insert(insertAt, rt.DOAnchorPos(b.anchoredPos, duration).SetEase(itemEase));

        if (fadeItems && b.canvasGroup != null)
        {
            openSeq.Insert(insertAt, b.canvasGroup.DOFade(b.alpha, Mathf.Max(0.01f, duration * 0.7f)));
        }
    }

    private RectTransform GetRectTransform(Component c)
    {
        if (c == null) return null;
        return c.transform as RectTransform;
    }

    private RectTransform GetRectTransform(GameObject go)
    {
        if (go == null) return null;
        return go.transform as RectTransform;
    }

    private void CacheBases()
    {
        baseStates.Clear();

        CacheOne(GetRectTransform(winLossBannerImage));
        CacheOne(GetRectTransform(winningCoinImage));
        CacheOne(GetRectTransform(winningDiamondImage));
        CacheOne(GetRectTransform(winningStarImage));
        CacheOne(GetRectTransform(winningCoinText));
        CacheOne(GetRectTransform(winningDiamondText));
        CacheOne(GetRectTransform(winningStarText));
        CacheOne(GetRectTransform(winnerImage));
        CacheOne(GetRectTransform(loserImage));

        if (extraAnimatedItems != null)
        {
            for (int i = 0; i < extraAnimatedItems.Count; i++)
            {
                CacheOne(extraAnimatedItems[i]);
            }
        }
    }

    private void CacheOne(RectTransform rt)
    {
        if (rt == null) return;
        RectBaseState st = new RectBaseState
        {
            anchoredPos = rt.anchoredPosition,
            localScale = rt.localScale,
            canvasGroup = fadeItems ? EnsureCanvasGroup(rt) : null,
            alpha = 1f
        };
        if (st.canvasGroup != null)
        {
            st.alpha = st.canvasGroup.alpha;
        }
        baseStates[rt] = st;
    }

    private CanvasGroup EnsureCanvasGroup(RectTransform rt)
    {
        if (rt == null) return null;
        CanvasGroup cg = rt.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
        }
        return cg;
    }

    private void RestoreToCachedBases()
    {
        if (baseStates.Count == 0) return;
        foreach (var kv in baseStates)
        {
            RectTransform rt = kv.Key;
            RectBaseState b = kv.Value;
            if (rt == null) continue;
            rt.anchoredPosition = b.anchoredPos;
            rt.localScale = b.localScale;
            if (b.canvasGroup != null) b.canvasGroup.alpha = b.alpha;
        }
    }

    private void SetHiddenStartState()
    {
        SetHiddenFor(GetRectTransform(winLossBannerImage), new Vector2(0f, bannerFromOffsetY));

        SetHiddenFor(GetRectTransform(winningCoinImage), new Vector2(0f, rewardsFromOffsetY));
        SetHiddenFor(GetRectTransform(winningDiamondImage), new Vector2(0f, rewardsFromOffsetY));
        SetHiddenFor(GetRectTransform(winningStarImage), new Vector2(0f, rewardsFromOffsetY));
        SetHiddenFor(GetRectTransform(winningCoinText), new Vector2(0f, rewardsFromOffsetY));
        SetHiddenFor(GetRectTransform(winningDiamondText), new Vector2(0f, rewardsFromOffsetY));
        SetHiddenFor(GetRectTransform(winningStarText), new Vector2(0f, rewardsFromOffsetY));

        SetHiddenFor(GetRectTransform(winnerImage), new Vector2(0f, bottomFromOffsetY));
        SetHiddenFor(GetRectTransform(loserImage), new Vector2(0f, bottomFromOffsetY));

        if (extraAnimatedItems != null)
        {
            for (int i = 0; i < extraAnimatedItems.Count; i++)
            {
                SetHiddenFor(extraAnimatedItems[i], new Vector2(0f, extraItemsFromOffsetY));
            }
        }
    }

    private void SetHiddenFor(RectTransform rt, Vector2 fromOffset)
    {
        if (rt == null) return;
        if (!baseStates.TryGetValue(rt, out RectBaseState b)) return;

        rt.DOKill();
        rt.anchoredPosition = b.anchoredPos + fromOffset;
        rt.localScale = Vector3.zero;

        if (fadeItems && b.canvasGroup != null)
        {
            b.canvasGroup.DOKill();
            b.canvasGroup.alpha = 0f;
        }
    }

    private void KillOpenTweens()
    {
        if (openSeq != null)
        {
            openSeq.Kill();
            openSeq = null;
        }

        foreach (var kv in baseStates)
        {
            RectTransform rt = kv.Key;
            RectBaseState b = kv.Value;
            if (rt != null) rt.DOKill();
            if (b.canvasGroup != null) b.canvasGroup.DOKill();
        }
    }

    private void PlayRewardTransfer()
    {
        KillTransferTweens();

        if (!cachedLocalWon) return;

        GameManager gm = GameManager.Instance;
        bool offlineStarMatch = gm != null && gm.CurrentMatchUsesOfflineStarRewards;

        if (offlineStarMatch)
        {
            bool hasStar = cachedWinCoinAmount > 0 && starPrefab != null;
            if (!hasStar) return;

            TryCreditWinRewards(true, cachedWinCoinAmount, 0);

            if (spawnRoot == null)
            {
                Canvas c = GetComponentInParent<Canvas>();
                if (c != null)
                {
                    spawnRoot = (c.rootCanvas != null ? c.rootCanvas.transform : c.transform) as RectTransform;
                }
            }

            Canvas.ForceUpdateCanvases();

            float starStartDelay = Mathf.Max(0f, transferStartDelay);
            float starDur = Mathf.Max(0.01f, flyDuration);
            float starStg = Mathf.Max(0f, burstStagger);

            TMP_Text fromWinText = winningStarText != null ? winningStarText : winningCoinText;
            RectTransform fromWinImage = GetRectTransform(winningStarImage != null ? winningStarImage : winningCoinImage);

            StartValueTransfer(fromWinText, starTargetValueText, cachedWinCoinAmount, starStartDelay, starDur + ((Mathf.Max(1, burstCount) - 1) * starStg));
            AppendBurst(starPrefab, starSpawnFrom != null ? starSpawnFrom : fromWinImage, starTarget, starStartDelay, starDur, starStg);
            return;
        }

        bool hasCoin = cachedWinCoinAmount > 0 && coinPrefab != null;
        bool hasDiamond = cachedWinDiamondAmount > 0 && diamondPrefab != null;

        if (!hasCoin && !hasDiamond) return;

        TryCreditWinRewards(true, cachedWinCoinAmount, cachedWinDiamondAmount);

        if (spawnRoot == null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                spawnRoot = (c.rootCanvas != null ? c.rootCanvas.transform : c.transform) as RectTransform;
            }
        }

        Canvas.ForceUpdateCanvases();

        float startDelay = Mathf.Max(0f, transferStartDelay);
        float dur = Mathf.Max(0.01f, flyDuration);
        float stg = Mathf.Max(0f, burstStagger);

        if (hasCoin)
        {
            StartValueTransfer(winningCoinText, coinTargetValueText, cachedWinCoinAmount, startDelay, dur + ((Mathf.Max(1, burstCount) - 1) * stg));
            AppendBurst(coinPrefab, coinSpawnFrom != null ? coinSpawnFrom : GetRectTransform(winningCoinImage), coinTarget, startDelay, dur, stg);
        }

        if (hasDiamond)
        {
            StartValueTransfer(winningDiamondText, diamondTargetValueText, cachedWinDiamondAmount, startDelay, dur + ((Mathf.Max(1, burstCount) - 1) * stg));
            AppendBurst(diamondPrefab, diamondSpawnFrom != null ? diamondSpawnFrom : GetRectTransform(winningDiamondImage), diamondTarget, startDelay, dur, stg);
        }
    }

    private void StartValueTransfer(TMP_Text fromWinText, TMP_Text toHudText, int amount, float delay, float duration)
    {
        if (amount <= 0) return;
        float d = Mathf.Max(0f, delay);
        float dur = Mathf.Max(0.01f, duration);

        if (fromWinText != null)
        {
            int start = amount;
            fromWinText.text = start.ToString();
            DOTween.To(() => start, v =>
            {
                start = v;
                if (fromWinText != null) fromWinText.text = start.ToString();
            }, 0, dur).SetDelay(d).SetEase(Ease.Linear).SetTarget(fromWinText).SetUpdate(true);
        }

        if (toHudText != null)
        {
            int baseVal = ExtractInt(toHudText.text);
            int end = baseVal + amount;
            int cur = baseVal;
            toHudText.text = cur.ToString();
            DOTween.To(() => cur, v =>
            {
                cur = v;
                if (toHudText != null) toHudText.text = cur.ToString();
            }, end, dur).SetDelay(d).SetEase(Ease.Linear).SetTarget(toHudText).SetUpdate(true);
        }
    }

    private void AppendBurst(RectTransform prefab, RectTransform from, RectTransform to, float startDelay, float duration, float stagger)
    {
        if (prefab == null) return;
        if (from == null || to == null) return;
        if (spawnRoot == null) return;

        int count = Mathf.Max(1, burstCount);
        float baseDelay = Mathf.Max(0f, startDelay);

        for (int i = 0; i < count; i++)
        {
            float delay = baseDelay + (i * stagger);
            float cH = curveHeight;
            float dirSign = Mathf.Sign((to.position - from.position).x);
            float cSide = Mathf.Abs(curveSideOffset) * (dirSign == 0f ? 1f : dirSign);

            RectTransform inst = Instantiate(prefab, spawnRoot);
            inst.gameObject.SetActive(true);
            inst.SetAsLastSibling();
            inst.position = from.position;

            Vector3 baseScale = prefab.localScale;
            if (baseScale.sqrMagnitude < 0.0001f) baseScale = Vector3.one;
            inst.localScale = baseScale;
            inst.localRotation = Quaternion.identity;

            CanvasGroup cg = inst.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            spawnedRewardItems.Add(inst);

            inst.DOKill();
            Tween t = CreateCurvedWorldMoveTween(inst, to.position, cH, cSide, duration)
                .SetDelay(delay)
                .SetEase(flyEase)
                .SetUpdate(true);
            t.SetTarget(inst);
            t.OnComplete(() =>
            {
                if (inst != null)
                {
                    spawnedRewardItems.Remove(inst);
                    Destroy(inst.gameObject);
                }
            });
        }
    }

    private Tween CreateCurvedWorldMoveTween(RectTransform rt, Vector3 endWorld, float curveHeightOffset, float curveSideOffsetWorld, float duration)
    {
        if (rt == null) return null;
        Vector3 startWorld = rt.position;
        Vector3 mid = (startWorld + endWorld) * 0.5f;
        Vector3 control = mid;
        control.y += curveHeightOffset;

        Vector3 dir = endWorld - startWorld;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        if (perp.sqrMagnitude > 0.0001f)
        {
            perp.Normalize();
            control += perp * curveSideOffsetWorld;
        }

        return DOTween.To(() => 0f, x =>
        {
            float t = x;
            float omt = 1f - t;
            Vector3 p = (omt * omt * startWorld) + (2f * omt * t * control) + (t * t * endWorld);
            if (rt != null) rt.position = p;
        }, 1f, Mathf.Max(0.01f, duration));
    }

    private void KillTransferTweens()
    {
        if (transferSeq != null)
        {
            transferSeq.Kill();
            transferSeq = null;
        }

        if (winningCoinText != null) DOTween.Kill(winningCoinText);
        if (winningDiamondText != null) DOTween.Kill(winningDiamondText);
        if (winningStarText != null) DOTween.Kill(winningStarText);
        if (coinTargetValueText != null) DOTween.Kill(coinTargetValueText);
        if (diamondTargetValueText != null) DOTween.Kill(diamondTargetValueText);
        if (starTargetValueText != null) DOTween.Kill(starTargetValueText);

        for (int i = spawnedRewardItems.Count - 1; i >= 0; i--)
        {
            RectTransform rt = spawnedRewardItems[i];
            if (rt == null) continue;
            rt.DOKill();
            Destroy(rt.gameObject);
        }
        spawnedRewardItems.Clear();
    }

    private int ClampToInt(long v)
    {
        if (v > int.MaxValue) return int.MaxValue;
        if (v < int.MinValue) return int.MinValue;
        return (int)v;
    }

    private void PlayVfx()
    {
        if (vfxRootsToEnableOnOpen != null)
        {
            for (int i = 0; i < vfxRootsToEnableOnOpen.Length; i++)
            {
                if (vfxRootsToEnableOnOpen[i] != null) vfxRootsToEnableOnOpen[i].SetActive(true);
            }
        }

        if (particleSystemsToPlayOnOpen != null)
        {
            for (int i = 0; i < particleSystemsToPlayOnOpen.Length; i++)
            {
                ParticleSystem ps = particleSystemsToPlayOnOpen[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }

    private void StopVfx()
    {
        if (particleSystemsToPlayOnOpen != null)
        {
            for (int i = 0; i < particleSystemsToPlayOnOpen.Length; i++)
            {
                ParticleSystem ps = particleSystemsToPlayOnOpen[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (vfxRootsToEnableOnOpen != null)
        {
            for (int i = 0; i < vfxRootsToEnableOnOpen.Length; i++)
            {
                if (vfxRootsToEnableOnOpen[i] != null) vfxRootsToEnableOnOpen[i].SetActive(false);
            }
        }
    }

    private Sprite GetWinnerSpriteByNumber(int playerNumber)
    {
        int idx = Mathf.Clamp(playerNumber, 1, 4) - 1;
        if (winnerSpritesByPlayerNumber == null) return null;
        if (idx < 0 || idx >= winnerSpritesByPlayerNumber.Count) return null;
        return winnerSpritesByPlayerNumber[idx];
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
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

    private void Reset()
    {
        winLossBannerImage = FindImageByName("Win Loss Image");

        winningCoinImage = FindChildGameObjectByName("winning coin Image");
        winningDiamondImage = FindChildGameObjectByName("winning Diamond Image");

        winningCoinText = FindTmpTextByName("WinningCointext");
        winningDiamondText = FindTmpTextByName("WinningDiamondText");

        winnerImage = FindImageByName("winner Image");
        loserImage = FindImageByName("loser Image");
    }

    private Image FindImageByName(string n)
    {
        Transform t = FindDeepChild(transform, n);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private TMP_Text FindTmpTextByName(string n)
    {
        Transform t = FindDeepChild(transform, n);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private GameObject FindChildGameObjectByName(string n)
    {
        Transform t = FindDeepChild(transform, n);
        return t != null ? t.gameObject : null;
    }

    private Transform FindDeepChild(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;

        Queue<Transform> q = new Queue<Transform>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            Transform cur = q.Dequeue();
            if (cur.name == name) return cur;
            for (int i = 0; i < cur.childCount; i++)
            {
                q.Enqueue(cur.GetChild(i));
            }
        }

        return null;
    }
}
