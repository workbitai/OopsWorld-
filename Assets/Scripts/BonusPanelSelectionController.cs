using DG.Tweening;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using My.UI;

public class BonusPanelSelectionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform bonus1;
    [SerializeField] private RectTransform bonus2;
    [SerializeField] private RectTransform bonus3;
    [SerializeField] private Button btn1;
    [SerializeField] private Button btn2;
    [SerializeField] private Button btn3;
    [SerializeField] private GameObject darkBg;

    [Header("Instruction (Optional)")]
    [SerializeField] private GameObject instructionText;

    [Header("Selected Item Animator (Optional)")]
    [SerializeField] private Animator bonus1Animator;
    [SerializeField] private Animator bonus2Animator;
    [SerializeField] private Animator bonus3Animator;
    [SerializeField] private string selectedAnimatorStateName = "";
    [SerializeField] private float selectedAnimatorDelay = 0f;

    [Header("Coin Burst (Optional)")]
    [SerializeField] private BonusCoinBurstVfx coinBurst;
    [SerializeField] private bool coinBurstAfterAnimator = true;
    [SerializeField] private float coinBurstDelaySeconds = 0f;

    [Header("Reward Text (Optional)")]
    [SerializeField] private BonusRewardTextAnimator rewardText;

    [Header("Wallet Text (Optional)")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text diamondText;

    [Header("Animation")]
    [SerializeField] private float moveToCenterDuration = 0.35f;
    [SerializeField] private Ease moveToCenterEase = Ease.OutCubic;
    [SerializeField] private float scaleUpMultiplier = 1.25f;
    [SerializeField] private float scaleUpDuration = 0.30f;
    [SerializeField] private Ease scaleUpEase = Ease.OutBack;
    [SerializeField] private float otherHideDuration = 0.20f;
    [SerializeField] private Ease otherHideEase = Ease.InBack;

    [Header("Target Position")]
    [SerializeField] private Vector2 targetAnchoredPosition = Vector2.zero;

    private readonly Dictionary<RectTransform, Vector3> baseScale = new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Vector2> baseAnchoredPos = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, int> baseSibling = new Dictionary<RectTransform, int>();

    private Sequence seq;
    private bool hasSelected = false;
    private RectTransform selected;

    private bool burstSubscribed = false;
    private int pendingRewardValue = 0;

    public event Action<int> OnRewardFinalized;

    private void OnEnable()
    {
        CacheBase(bonus1);
        CacheBase(bonus2);
        CacheBase(bonus3);

        ResetState();
        HookButtons();
        RefreshWalletTexts();

        if (HapticsManager.Instance != null)
        {
            PlayImpactHaptic();
        }
    }

    private void RefreshWalletTexts()
    {
        int coins = 0;
        int diamonds = 0;

        if (GameManager.Instance != null)
        {
            coins = GameManager.Instance.Coins;
            diamonds = GameManager.Instance.Diamonds;
        }
        else if (PlayerWallet.Instance != null)
        {
            coins = PlayerWallet.Instance.Coins;
            diamonds = PlayerWallet.Instance.Diamonds;
        }
        else
        {
            coins = Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_COINS", 0));
            diamonds = Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_DIAMONDS", 0));
        }

        if (coinText != null) coinText.text = Mathf.Max(0, coins).ToString();
        if (diamondText != null) diamondText.text = Mathf.Max(0, diamonds).ToString();
    }

    private void OnDisable()
    {
        Kill();

        if (coinBurst != null && burstSubscribed)
        {
            coinBurst.OnAllCoinsDestroyed -= HandleAllCoinsDestroyed;
            burstSubscribed = false;
        }

        UnhookButtons();
    }

    private void CacheBase(RectTransform rt)
    {
        if (rt == null) return;

        if (!baseScale.ContainsKey(rt))
        {
            Vector3 s = rt.localScale;
            if (s.x < 0.001f && s.y < 0.001f)
            {
                s = Vector3.one;
            }
            baseScale[rt] = s;
        }
        if (!baseAnchoredPos.ContainsKey(rt)) baseAnchoredPos[rt] = rt.anchoredPosition;
        if (!baseSibling.ContainsKey(rt)) baseSibling[rt] = rt.GetSiblingIndex();
    }

    private void HookButtons()
    {
        if (btn1 != null)
        {
            btn1.onClick.RemoveListener(OnClick1);
            btn1.onClick.AddListener(OnClick1);
        }
        if (btn2 != null)
        {
            btn2.onClick.RemoveListener(OnClick2);
            btn2.onClick.AddListener(OnClick2);
        }
        if (btn3 != null)
        {
            btn3.onClick.RemoveListener(OnClick3);
            btn3.onClick.AddListener(OnClick3);
        }
    }

    private void UnhookButtons()
    {
        if (btn1 != null) btn1.onClick.RemoveListener(OnClick1);
        if (btn2 != null) btn2.onClick.RemoveListener(OnClick2);
        if (btn3 != null) btn3.onClick.RemoveListener(OnClick3);
    }

    private void OnClick1() => Select(bonus1);
    private void OnClick2() => Select(bonus2);
    private void OnClick3() => Select(bonus3);

    private void Select(RectTransform selected)
    {
        if (hasSelected) return;
        if (selected == null) return;

        hasSelected = true;

        if (instructionText != null)
        {
            instructionText.SetActive(false);
        }

        BonusPanelJellyAnimator jelly = GetComponent<BonusPanelJellyAnimator>();
        if (jelly != null)
        {
            jelly.StopAndRestoreBaseImmediate();
        }

        if (btn1 != null) btn1.interactable = false;
        if (btn2 != null) btn2.interactable = false;
        if (btn3 != null) btn3.interactable = false;

        if (darkBg != null) darkBg.SetActive(true);

        Kill();
        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        RectTransform[] all = new RectTransform[] { bonus1, bonus2, bonus3 };
        for (int i = 0; i < all.Length; i++)
        {
            RectTransform rt = all[i];
            if (rt == null) continue;
            if (rt == selected) continue;

            seq.Join(rt.DOScale(Vector3.zero, Mathf.Max(0.01f, otherHideDuration)).SetEase(otherHideEase));
        }

        selected.SetAsLastSibling();

        Vector3 baseS;
        if (!baseScale.TryGetValue(selected, out baseS))
        {
            baseS = selected.localScale;
        }

        if (baseS.x < 0.001f && baseS.y < 0.001f)
        {
            Vector3 current = selected.localScale;
            if (current.x > 0.001f || current.y > 0.001f)
            {
                baseS = current;
            }
            else
            {
                baseS = Vector3.one;
            }
            baseScale[selected] = baseS;
        }

        seq.Join(selected.DOAnchorPos(targetAnchoredPosition, Mathf.Max(0.01f, moveToCenterDuration)).SetEase(moveToCenterEase));
        seq.Join(selected.DOScale(baseS * Mathf.Max(0.01f, scaleUpMultiplier), Mathf.Max(0.01f, scaleUpDuration)).SetEase(scaleUpEase));

        seq.OnComplete(() =>
        {
            for (int i = 0; i < all.Length; i++)
            {
                RectTransform rt = all[i];
                if (rt == null) continue;
                if (rt == selected) continue;
                rt.gameObject.SetActive(false);
            }

            Animator a = GetAnimatorFor(selected);
            if (a != null)
            {
                float d = Mathf.Max(0f, selectedAnimatorDelay);
                if (d <= 0f)
                {
                    PlayAnimatorOnce(a);
                }
                else
                {
                    DOVirtual.DelayedCall(d, () => PlayAnimatorOnce(a));
                }

                if (coinBurst != null)
                {
                    PrepareRewardSubscription();
                    float coinDelay = Mathf.Max(0f, coinBurstDelaySeconds);
                    if (coinBurstAfterAnimator)
                    {
                        float animLen = GetAnimatorStateLengthSeconds(a);
                        coinDelay += Mathf.Max(0f, d) + Mathf.Max(0f, animLen);
                    }
                    else
                    {
                        coinDelay += Mathf.Max(0f, d);
                    }

                    if (coinDelay <= 0f)
                    {
                        coinBurst.PlayBurst(selected);
                    }
                    else
                    {
                        DOVirtual.DelayedCall(coinDelay, () =>
                        {
                            if (coinBurst != null) coinBurst.PlayBurst(selected);
                        });
                    }
                }
            }
            else
            {
                if (coinBurst != null)
                {
                    PrepareRewardSubscription();
                    float coinDelay = Mathf.Max(0f, coinBurstDelaySeconds);
                    if (coinDelay <= 0f)
                    {
                        coinBurst.PlayBurst(selected);
                    }
                    else
                    {
                        DOVirtual.DelayedCall(coinDelay, () =>
                        {
                            if (coinBurst != null) coinBurst.PlayBurst(selected);
                        });
                    }
                }
            }
        });
    }

    private void PrepareRewardSubscription()
    {
        if (rewardText != null)
        {
            pendingRewardValue = rewardText.PickRandomReward();
        }
        else
        {
            pendingRewardValue = 0;
        }

        Debug.Log($"BonusPanelSelectionController: Prepared reward={pendingRewardValue} rewardText={(rewardText != null ? rewardText.name : "null")}");

        if (coinBurst == null) return;

        if (burstSubscribed)
        {
            coinBurst.OnAllCoinsDestroyed -= HandleAllCoinsDestroyed;
            burstSubscribed = false;
        }

        coinBurst.OnAllCoinsDestroyed += HandleAllCoinsDestroyed;
        burstSubscribed = true;
    }

    private void HandleAllCoinsDestroyed()
    {
        Debug.Log($"BonusPanelSelectionController: All coins destroyed. Playing reward={pendingRewardValue}");

        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Pulse(140, 255);
        }

        if (coinBurst != null && burstSubscribed)
        {
            coinBurst.OnAllCoinsDestroyed -= HandleAllCoinsDestroyed;
            burstSubscribed = false;
        }

        if (rewardText != null)
        {
            rewardText.Play(pendingRewardValue);
        }

        OnRewardFinalized?.Invoke(pendingRewardValue);
    }

    private Animator GetAnimatorFor(RectTransform selected)
    {
        if (selected == null) return null;
        if (selected == bonus1) return bonus1Animator;
        if (selected == bonus2) return bonus2Animator;
        if (selected == bonus3) return bonus3Animator;
        return null;
    }

    private void PlayAnimatorOnce(Animator animator)
    {
        if (animator == null) return;
        if (!animator.gameObject.activeInHierarchy) return;

        animator.enabled = true;
        animator.speed = 1f;

        animator.Rebind();
        animator.Update(0f);

        PlayImpactHaptic();

        if (!string.IsNullOrEmpty(selectedAnimatorStateName))
        {
            animator.Play(selectedAnimatorStateName, 0, 0f);
        }
        else
        {
            animator.Play(0, 0, 0f);
        }

        animator.Update(0f);
    }

    private void PlayImpactHaptic()
    {
        if (HapticsManager.Instance == null) return;
        HapticsManager.Instance.Pattern(new long[] { 0, 35, 25, 75 }, new int[] { 160, 0, 255 });
    }

    private float GetAnimatorStateLengthSeconds(Animator animator)
    {
        if (animator == null) return 0f;

        // Note: length is only reliable after the state has been entered. In this flow we call
        // Rebind/Play/Update(0) before scheduling coins, so it should be OK.
        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
        float len = st.length;
        if (float.IsNaN(len) || float.IsInfinity(len) || len < 0f) return 0f;
        return len;
    }

    public void ResetState()
    {
        hasSelected = false;
        Kill();

        if (instructionText != null)
        {
            instructionText.SetActive(true);
        }

        if (darkBg != null) darkBg.SetActive(false);

        Restore(bonus1);
        Restore(bonus2);
        Restore(bonus3);

        if (btn1 != null) btn1.interactable = true;
        if (btn2 != null) btn2.interactable = true;
        if (btn3 != null) btn3.interactable = true;
    }

    private void Restore(RectTransform rt)
    {
        if (rt == null) return;

        rt.gameObject.SetActive(true);

        Vector3 s;
        if (baseScale.TryGetValue(rt, out s))
        {
            if (s.x < 0.001f && s.y < 0.001f) s = Vector3.one;
            rt.localScale = s;
        }

        Vector2 p;
        if (baseAnchoredPos.TryGetValue(rt, out p)) rt.anchoredPosition = p;

        int sib;
        if (baseSibling.TryGetValue(rt, out sib)) rt.SetSiblingIndex(sib);

        rt.DOKill();
    }

    private void Kill()
    {
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }

        if (bonus1 != null) bonus1.DOKill();
        if (bonus2 != null) bonus2.DOKill();
        if (bonus3 != null) bonus3.DOKill();
    }
}
