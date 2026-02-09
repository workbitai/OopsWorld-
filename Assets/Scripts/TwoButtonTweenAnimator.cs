using System;
using DG.Tweening;
using UnityEngine;

public class TwoButtonTweenAnimator : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform button1;
    [SerializeField] private RectTransform button2;

    [Header("Animation")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private float startDelay = 0.05f;
    [SerializeField] private float betweenDelay = 0.08f;
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutBack;

    [Header("Close")]
    [SerializeField] private float closeDuration = 0.20f;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Hidden State")]
    [SerializeField] private Vector3 hiddenScale = Vector3.zero;

    private Vector3 baseScale1 = Vector3.one;
    private Vector3 baseScale2 = Vector3.one;

    private Sequence seq;

    private void Awake()
    {
        CacheBaseScales();
    }

    private void OnEnable()
    {
        CacheBaseScales();
        ResetHidden();

        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Kill();
    }

    private void CacheBaseScales()
    {
        if (button1 != null)
        {
            Vector3 s = button1.localScale;
            if (s.x > 0.0001f || s.y > 0.0001f) baseScale1 = s;
        }

        if (button2 != null)
        {
            Vector3 s = button2.localScale;
            if (s.x > 0.0001f || s.y > 0.0001f) baseScale2 = s;
        }
    }

    public void ResetHidden()
    {
        Kill();

        if (button1 != null)
        {
            button1.DOKill();
            button1.localScale = hiddenScale;
        }

        if (button2 != null)
        {
            button2.DOKill();
            button2.localScale = hiddenScale;
        }
    }

    public void Play()
    {
        Kill();

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        float d0 = Mathf.Max(0f, startDelay);
        float d1 = d0;
        float d2 = d0 + Mathf.Max(0f, betweenDelay);
        float dur = Mathf.Max(0.01f, duration);

        if (button1 != null)
        {
            button1.DOKill();
            seq.Insert(d1, button1.DOScale(baseScale1, dur).SetEase(ease));
        }

        if (button2 != null)
        {
            button2.DOKill();
            seq.Insert(d2, button2.DOScale(baseScale2, dur).SetEase(ease));
        }
    }

    public void Close(Action onComplete = null)
    {
        Kill();

        seq = DOTween.Sequence();
        seq.SetAutoKill(true);

        float d0 = Mathf.Max(0f, startDelay);
        float d1 = d0;
        float d2 = d0 + Mathf.Max(0f, betweenDelay);
        float dur = Mathf.Max(0.01f, closeDuration);

        if (button1 != null)
        {
            button1.DOKill();
            seq.Insert(d1, button1.DOScale(hiddenScale, dur).SetEase(closeEase));
        }

        if (button2 != null)
        {
            button2.DOKill();
            seq.Insert(d2, button2.DOScale(hiddenScale, dur).SetEase(closeEase));
        }

        seq.OnComplete(() => onComplete?.Invoke());
    }

    private void Kill()
    {
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }

        if (button1 != null) button1.DOKill();
        if (button2 != null) button2.DOKill();
    }
}
