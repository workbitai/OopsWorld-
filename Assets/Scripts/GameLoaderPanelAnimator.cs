using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class GameLoaderPanelAnimator : MonoBehaviour
{
    [Header("Sections")]
    [SerializeField] private GameObject loaderSection;
    [SerializeField] private GameObject errorSection;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private string defaultErrorText = "Something went wrong.";

    [Header("Rotate")]
    [SerializeField] private RectTransform rotatingImage;
    [SerializeField] private float rotationDegreesPerSecond = 360f;

    [Header("Loading Dots")]
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string baseLoadingText = "Loading";
    [SerializeField] private int maxDots = 3;
    [SerializeField] private float dotInterval = 0.25f;

    [Header("Behavior")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool resetToLoaderOnEnable = true;

    [Header("Dismiss")]
    [SerializeField] private bool dismissErrorOnPointerDown = true;
    [SerializeField] private bool dismissErrorOnBackKey = true;
    [SerializeField] private UnityEvent onErrorDismissed;

    private Tween rotateTween;
    private Sequence dotsSeq;

    private Coroutine autoHideCoroutine;

    private bool isInErrorMode;

    private void Awake()
    {
        CacheBaseLoadingText();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (resetToLoaderOnEnable)
        {
            ResetToLoader();
        }

        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        Stop();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!isInErrorMode) return;
        if (errorSection == null || !errorSection.activeInHierarchy) return;

        if (dismissErrorOnBackKey && Input.GetKeyDown(KeyCode.Escape))
        {
            DismissError();
            return;
        }

        if (!dismissErrorOnPointerDown) return;

        if (Input.GetMouseButtonDown(0))
        {
            DismissError();
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                DismissError();
            }
        }
    }

    public void ShowLoader(string loadingBaseTextOverride = null)
    {
        StopAutoHide();
        if (!string.IsNullOrWhiteSpace(loadingBaseTextOverride))
        {
            baseLoadingText = loadingBaseTextOverride.Trim();
        }

        isInErrorMode = false;
        SetSections(showLoader: true, showError: false);
        Play();
    }

    public void ShowError(string message)
    {
        ShowError(message, 0f);
    }

    public void ShowError(string message, float autoHideSeconds)
    {
        StopAutoHide();
        isInErrorMode = true;
        Stop();
        SetSections(showLoader: false, showError: true);

        if (errorText != null)
        {
            errorText.text = string.IsNullOrWhiteSpace(message) ? defaultErrorText : message;
        }

        if (Application.isPlaying && autoHideSeconds > 0f)
        {
            autoHideCoroutine = StartCoroutine(AutoHideAfterDelay(autoHideSeconds));
        }
    }

    public void Hide()
    {
        StopAutoHide();
        Stop();
        isInErrorMode = false;
        gameObject.SetActive(false);
    }

    public void DismissError()
    {
        Hide();
        if (onErrorDismissed != null)
        {
            onErrorDismissed.Invoke();
        }
    }

    public void ResetToLoader()
    {
        StopAutoHide();
        isInErrorMode = false;
        if (errorText != null)
        {
            errorText.text = defaultErrorText;
        }
        SetSections(showLoader: true, showError: false);
    }

    private IEnumerator AutoHideAfterDelay(float seconds)
    {
        float t = Mathf.Max(0f, seconds);
        if (t > 0f)
        {
            yield return new WaitForSecondsRealtime(t);
        }
        autoHideCoroutine = null;
        Hide();
    }

    private void StopAutoHide()
    {
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }
    }

    private void SetSections(bool showLoader, bool showError)
    {
        if (loaderSection != null) loaderSection.SetActive(showLoader);
        if (errorSection != null) errorSection.SetActive(showError);
    }

    public void Play()
    {
        Stop();

        if (isInErrorMode)
        {
            return;
        }

        if (rotatingImage != null)
        {
            float speed = Mathf.Max(1f, rotationDegreesPerSecond);
            float dur = Mathf.Max(0.01f, 360f / speed);
            rotateTween = rotatingImage
                .DORotate(new Vector3(0f, 0f, -360f), dur, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }

        CacheBaseLoadingText();
        if (loadingText != null)
        {
            int dots = Mathf.Clamp(maxDots, 1, 10);
            float interval = Mathf.Max(0.01f, dotInterval);

            dotsSeq = DOTween.Sequence();
            dotsSeq.SetAutoKill(false);
            for (int i = 0; i <= dots; i++)
            {
                int n = i;
                dotsSeq.AppendCallback(() =>
                {
                    if (loadingText == null) return;
                    loadingText.text = baseLoadingText + new string('.', n);
                });
                dotsSeq.AppendInterval(interval);
            }
            dotsSeq.SetLoops(-1, LoopType.Restart);
            dotsSeq.SetUpdate(true);
        }
    }

    public void Stop()
    {
        StopAutoHide();
        if (rotateTween != null)
        {
            rotateTween.Kill();
            rotateTween = null;
        }

        if (dotsSeq != null)
        {
            dotsSeq.Kill();
            dotsSeq = null;
        }

        if (rotatingImage != null) rotatingImage.DOKill();

        if (loadingText != null)
        {
            loadingText.DOKill();
            loadingText.text = baseLoadingText;
        }
    }

    private void CacheBaseLoadingText()
    {
        if (loadingText == null) return;
        if (!string.IsNullOrEmpty(baseLoadingText)) return;

        string s = loadingText.text ?? string.Empty;
        s = s.Trim();
        while (s.EndsWith("."))
        {
            s = s.Substring(0, s.Length - 1);
        }

        baseLoadingText = string.IsNullOrEmpty(s) ? "Loading" : s;
    }
}
