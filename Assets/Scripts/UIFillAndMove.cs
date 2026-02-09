using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;

public class UIFillAndMove : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage;              // your filled Image
    [SerializeField] private RectTransform movingObject;   // SplashPiece / rocket
    [SerializeField] private Transform scaleObject1;       // Optional scale-in object 1
    [SerializeField] private Transform scaleObject2;       // Optional scale-in object 2

    [Header("Move")]
    [SerializeField] private Vector2 moveOffset = new Vector2(500f, 0f);

    [Header("Speed")]
    [SerializeField] private float fillSpeed = 0.35f;      // per second (0..1)
    [SerializeField] private float scaleInDuration = 0.5f; // Scale-in animation duration
    [SerializeField] private Ease scaleInEase = Ease.OutBack; // Scale-in animation ease
    [SerializeField] private float scaleInDelay = 0.15f;

    [Header("On Complete")]
    [SerializeField] private FancyScrollView.Example09.ScreenManager screenManager;
    [SerializeField] private GameObject nextScreen;
    [SerializeField] private int nextScreenIndex = 1;
    [SerializeField] private float onCompleteDelay = 0f;

    [Header("On Complete Event (Optional)")]
    [SerializeField] private UnityEvent onComplete;
    [SerializeField] private bool skipDefaultNavigation;

    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f;

    private Vector2 movingObjectStartAnchoredPos;
    private bool completed;

    private void OnEnable()
    {
        completed = false;
        PlayScaleIn(scaleObject1);
        PlayScaleIn(scaleObject2);
    }

    private void Awake()
    {
        if (movingObject != null)
            movingObjectStartAnchoredPos = movingObject.anchoredPosition;
        Apply(progress);

        if (!completed && progress >= 0.999f)
        {
            completed = true;
            OnFillComplete();
        }
    }

    private void Update()
    {
        progress = Mathf.MoveTowards(progress, 1f, fillSpeed * Time.deltaTime);
        Apply(progress);

        if (!completed && progress >= 0.999f)
        {
            completed = true;
            Debug.Log($"UIFillAndMove: Progress complete (progress={progress}). Triggering next screen...");
            OnFillComplete();
        }
    }

    private void Apply(float t)
    {
        if (fillImage != null)
            fillImage.fillAmount = t;

        if (movingObject != null)
            movingObject.anchoredPosition = movingObjectStartAnchoredPos + (moveOffset * t);
    }

    // Optional: call this from other script to control progress manually
    public void SetProgress(float t)
    {
        progress = Mathf.Clamp01(t);
        Apply(progress);

        if (!completed && progress >= 0.999f)
        {
            completed = true;
            OnFillComplete();
        }
    }

    private void OnFillComplete()
    {
        if (onCompleteDelay > 0f)
        {
            DOVirtual.DelayedCall(onCompleteDelay, OpenNextScreen);
        }
        else
        {
            OpenNextScreen();
        }
    }

    private void OpenNextScreen()
    {
        if (onComplete != null)
        {
            onComplete.Invoke();
        }

        if (skipDefaultNavigation)
        {
            return;
        }

        if (screenManager == null)
        {
            screenManager = FindObjectOfType<FancyScrollView.Example09.ScreenManager>();
        }

        if (screenManager == null)
        {
            Debug.LogWarning("UIFillAndMove: ScreenManager not assigned/found, cannot open next screen.");
            return;
        }

        if (nextScreen != null)
        {
            Debug.Log($"UIFillAndMove: Opening screen '{nextScreen.name}'...");
            screenManager.OpenScreen(nextScreen);
        }
        else
        {
            Debug.Log($"UIFillAndMove: Opening ScreenManager index {nextScreenIndex}...");
            screenManager.OpenScreenByIndex(nextScreenIndex);
        }

        GameObject current = screenManager.GetCurrentActiveScreen();
        Debug.Log($"UIFillAndMove: After opening next screen, currentActiveScreen={(current != null ? current.name : "NULL")}");
    }

    public void SetSkipDefaultNavigation(bool skip)
    {
        skipDefaultNavigation = skip;
    }

    private void PlayScaleIn(Transform target)
    {
        if (target == null) return;

        target.DOKill();
        target.localScale = Vector3.zero;
        target.DOScale(Vector3.one, scaleInDuration).SetEase(scaleInEase).SetDelay(scaleInDelay);
    }
}