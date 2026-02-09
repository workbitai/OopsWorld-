using DG.Tweening;
using UnityEngine;

public class UIWobbleRotate : MonoBehaviour
{
    [Header("Auto Play")]
    [SerializeField] private bool playOnEnable = true;

    [Header("Wobble")]
    [SerializeField] private float angle = 6f;
    [SerializeField] private float delay = 0.35f;
    [SerializeField] private float halfDuration = 0.8f;
    [SerializeField] private Ease ease = Ease.InOutSine;
    [SerializeField] private bool useLocalRotation = true;

    private Tween rotateTween;
    private Vector3 baseEuler;

    private void OnEnable()
    {
        CacheBase();
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Kill();
        RestoreBase();
    }

    public void Play()
    {
        Kill();
        CacheBase();

        Vector3 target = new Vector3(baseEuler.x, baseEuler.y, baseEuler.z + angle);

        if (useLocalRotation)
        {
            rotateTween = transform
                .DOLocalRotate(target, Mathf.Max(0.01f, halfDuration), RotateMode.Fast)
                .SetEase(ease)
                .SetDelay(Mathf.Max(0f, delay))
                .SetLoops(-1, LoopType.Yoyo)
                .OnKill(RestoreBase);
        }
        else
        {
            rotateTween = transform
                .DORotate(target, Mathf.Max(0.01f, halfDuration), RotateMode.Fast)
                .SetEase(ease)
                .SetDelay(Mathf.Max(0f, delay))
                .SetLoops(-1, LoopType.Yoyo)
                .OnKill(RestoreBase);
        }
    }

    public void Kill()
    {
        if (rotateTween != null)
        {
            rotateTween.Kill();
            rotateTween = null;
        }

        transform.DOKill();
    }

    private void CacheBase()
    {
        baseEuler = useLocalRotation ? transform.localEulerAngles : transform.eulerAngles;
    }

    private void RestoreBase()
    {
        if (useLocalRotation)
        {
            transform.localEulerAngles = baseEuler;
        }
        else
        {
            transform.eulerAngles = baseEuler;
        }
    }
}
