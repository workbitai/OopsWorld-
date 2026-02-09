using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ContinuousRotateImage : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float durationSeconds = 1f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private RotateMode rotateMode = RotateMode.FastBeyond360;
    private Tween rotateTween;

    private void Reset()
    {
        Image img = GetComponent<Image>();
        if (img != null)
        {
            target = img.rectTransform;
            return;
        }

        target = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        StartRotation();
    }

    private void OnDisable()
    {
        StopRotation();
    }

    private void OnDestroy()
    {
        StopRotation();
    }

    public void StartRotation()
    {
        StopRotation();

        if (target == null)
        {
            target = GetComponent<RectTransform>();
        }

        if (target == null)
        {
            return;
        }

        float d = Mathf.Max(0.05f, durationSeconds);
        float dir = clockwise ? -360f : 360f;

        rotateTween = target
            .DOLocalRotate(new Vector3(0f, 0f, dir), d, rotateMode)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental)
            .SetUpdate(true);
    }

    public void StopRotation()
    {
        if (rotateTween != null)
        {
            rotateTween.Kill(false);
            rotateTween = null;
        }
    }
}
