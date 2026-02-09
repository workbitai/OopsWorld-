using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class BonusRewardTextAnimator : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI amountText;

    [Header("Reward Values")]
    [SerializeField] private List<int> rewardValues = new List<int>();

    [Header("Message")]
    [SerializeField] private string message = "congratulation , won Lucky coin";
    [SerializeField] private float messageDelay = 0.08f;
    [SerializeField] private float messageCharInterval = 0.02f;
    [SerializeField] private float messagePunchScale = 0.05f;
    [SerializeField] private float messagePunchDuration = 0.06f;

    [Header("Count Up")]
    [SerializeField] private float countUpDelay = 0.10f;
    [SerializeField] private float countUpDuration = 0.6f;

    private Tween countTween;

    public int PickRandomReward()
    {
        if (rewardValues == null || rewardValues.Count == 0) return 0;
        int idx = Random.Range(0, rewardValues.Count);
        return rewardValues[idx];
    }

    public void PlayRandom()
    {
        Play(PickRandomReward());
    }

    public void Play(int reward)
    {
        DOTween.Kill(this);

        Debug.Log($"BonusRewardTextAnimator: Play reward={reward}");

        if (countTween != null)
        {
            countTween.Kill();
            countTween = null;
        }

        TextMeshProUGUI msgText = messageText != null ? messageText : titleText;

        PrepareText(msgText);
        PrepareText(amountText);

        if (msgText != null) msgText.text = "";
        if (amountText != null) amountText.text = "+0";

        if (!isActiveAndEnabled)
        {
            if (msgText != null) msgText.text = message;
            if (amountText != null) amountText.text = "+" + Mathf.Max(0, reward).ToString();
            return;
        }

        string msg = message ?? "";
        int msgLen = msg.Length;
        float startDelay = Mathf.Max(0f, messageDelay);
        float charInterval = Mathf.Max(0f, messageCharInterval);

        if (msgLen <= 0)
        {
            if (msgText != null) msgText.text = msg;
        }
        else
        {
            for (int i = 0; i < msgLen; i++)
            {
                int captured = i;
                DOVirtual.DelayedCall(startDelay + (captured * charInterval), () =>
                {
                    if (msgText == null) return;
                    msgText.text = msg.Substring(0, Mathf.Min(msg.Length, captured + 1));

                    if (msgText.transform != null && messagePunchScale > 0f)
                    {
                        msgText.transform.DOKill();
                        msgText.transform.DOPunchScale(Vector3.one * messagePunchScale, Mathf.Max(0.01f, messagePunchDuration));
                    }
                }).SetTarget(this);
            }
        }

        float messageTime = startDelay + Mathf.Max(0f, (msgLen - 1) * charInterval);

        DOVirtual.DelayedCall(messageTime + Mathf.Max(0f, countUpDelay), () =>
        {
            if (amountText == null) return;

            int finalValue = Mathf.Max(0, reward);
            int startValue = finalValue > 0 ? 1 : 0;
            amountText.text = "+" + startValue.ToString();

            int current = startValue;
            countTween = DOTween.To(() => current, x =>
            {
                current = x;
                if (amountText != null) amountText.text = "+" + current.ToString();
            }, finalValue, Mathf.Max(0.01f, countUpDuration)).SetEase(Ease.Linear).SetTarget(this);
        }).SetTarget(this);
    }

    private void PrepareText(TextMeshProUGUI t)
    {
        if (t == null) return;
        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        t.enabled = true;

        Color c = t.color;
        if (c.a < 1f) c.a = 1f;
        t.color = c;

        if (t.transform != null)
        {
            t.transform.localScale = Vector3.one;
        }
    }
}
