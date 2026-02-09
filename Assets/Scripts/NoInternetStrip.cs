using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class NoInternetStrip : MonoBehaviour
{
    public static NoInternetStrip Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string defaultMessage = "No internet connection. Please check and try again.";

    [Header("Animation (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimatorTriggers = false;
    [SerializeField] private string showTrigger = "Show";
    [SerializeField] private string hideTrigger = "Hide";
    [SerializeField] private float hideDeactivateDelaySeconds = 0.2f;

    [Header("Scale Tween (Optional)")]
    [SerializeField] private bool useScaleTween = false;
    [SerializeField] private Transform scaleTarget;
    [SerializeField] private Vector3 hiddenScale = Vector3.zero;
    [SerializeField] private Vector3 shownScale = Vector3.one;
    [SerializeField] private float showScaleDuration = 0.18f;
    [SerializeField] private float hideScaleDuration = 0.14f;
    [SerializeField] private Ease showScaleEase = Ease.OutBack;
    [SerializeField] private Ease hideScaleEase = Ease.InBack;

    [Header("Behavior")]
    [SerializeField] private float autoHideSeconds = 2f;

    private Coroutine autoHideRoutine;
    private Tween scaleTween;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (root == null)
        {
            root = gameObject;
        }

        if (scaleTarget == null)
        {
            scaleTarget = root != null ? root.transform : transform;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        StopAutoHide();
        KillScaleTween();
    }

    public void Show(string message = null)
    {
        StopAutoHide();
        KillScaleTween();

        if (messageText != null)
        {
            string msg = string.IsNullOrWhiteSpace(message) ? defaultMessage : message;
            messageText.text = msg;
        }

        if (root != null && !root.activeSelf)
        {
            root.SetActive(true);
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (useScaleTween && scaleTarget != null)
        {
            scaleTarget.localScale = hiddenScale;
            scaleTween = scaleTarget
                .DOScale(shownScale, Mathf.Max(0.01f, showScaleDuration))
                .SetEase(showScaleEase)
                .SetUpdate(true);
        }

        if (useAnimatorTriggers && animator != null && !string.IsNullOrWhiteSpace(showTrigger))
        {
            animator.ResetTrigger(hideTrigger);
            animator.SetTrigger(showTrigger);
        }

        float t = Mathf.Max(0f, autoHideSeconds);
        if (Application.isPlaying && t > 0f)
        {
            autoHideRoutine = StartCoroutine(AutoHideAfterDelay(t));
        }
    }

    public void Hide()
    {
        StopAutoHide();
        KillScaleTween();

        if (useScaleTween && scaleTarget != null)
        {
            float dur = Mathf.Max(0.01f, hideScaleDuration);
            scaleTween = scaleTarget
                .DOScale(hiddenScale, dur)
                .SetEase(hideScaleEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (root != null)
                    {
                        root.SetActive(false);
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }
                });
            return;
        }

        if (useAnimatorTriggers && animator != null && !string.IsNullOrWhiteSpace(hideTrigger))
        {
            animator.ResetTrigger(showTrigger);
            animator.SetTrigger(hideTrigger);

            float d = Mathf.Max(0f, hideDeactivateDelaySeconds);
            if (Application.isPlaying && d > 0f)
            {
                autoHideRoutine = StartCoroutine(DeactivateAfterDelay(d));
                return;
            }
        }

        if (root != null)
        {
            root.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator AutoHideAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        autoHideRoutine = null;
        Hide();
    }

    private IEnumerator DeactivateAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        autoHideRoutine = null;

        if (root != null)
        {
            root.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void StopAutoHide()
    {
        if (autoHideRoutine == null) return;
        StopCoroutine(autoHideRoutine);
        autoHideRoutine = null;
    }

    private void KillScaleTween()
    {
        if (scaleTween != null)
        {
            scaleTween.Kill();
            scaleTween = null;
        }

        if (scaleTarget != null)
        {
            scaleTarget.DOKill();
        }
    }

    public static bool BlockIfOffline(string message = null)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            return false;
        }

        if (Instance == null)
        {
            Instance = FindObjectOfType<NoInternetStrip>(true);

            if (Instance == null)
            {
                NoInternetStrip[] all = Resources.FindObjectsOfTypeAll<NoInternetStrip>();
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        NoInternetStrip s = all[i];
                        if (s == null) continue;
                        if (!s.gameObject.scene.IsValid()) continue;
                        Instance = s;
                        break;
                    }
                }
            }
        }

        if (Instance != null)
        {
            Instance.Show(message);
        }

        return true;
    }
}
