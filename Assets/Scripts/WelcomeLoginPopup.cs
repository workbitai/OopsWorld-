using System;
using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class WelcomeLoginPopup : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private RectTransform boardRect;
    [SerializeField] private RectTransform congratulationsRect;
    [SerializeField] private TMP_Text commonText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private string usernameFormat = "{0}";
    [SerializeField] private Button closeButton;

    [Header("Intro Animation")]
    [SerializeField] private float boardDropDuration = 0.35f;
    [SerializeField] private float congratsDropDuration = 0.25f;
    [SerializeField] private float dropFromYOffset = 900f;
    [SerializeField] private float afterBoardDelay = 0.05f;
    [SerializeField] private float afterCongratsDelay = 0.05f;
    [SerializeField] private Ease boardDropEase = Ease.OutBack;
    [SerializeField] private Ease congratsDropEase = Ease.OutBack;

    [Header("Typing")]
    [SerializeField] private float commonCharDelay = 0.01f;
    [SerializeField] private float usernameCharDelay = 0.015f;

    private const string PrefKeyPrefix = "WELCOME_LOGIN_SHOWN_";

    private Action onClosed;

    private Vector2 boardTargetPos;
    private Vector2 congratsTargetPos;
    private Tween boardTween;
    private Tween congratsTween;
    private Coroutine typingRoutine;

    public static bool HasShownForUser(string userId)
    {
        string id = userId ?? string.Empty;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        string key = PrefKeyPrefix + id;
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        CacheTargets();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        CacheTargets();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    public bool ShowIfFirstTime(string userId, string username, Action onClosedCallback = null)
    {
        string id = userId ?? string.Empty;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        string key = PrefKeyPrefix + id;
        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            return false;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        Show(username, onClosedCallback);

        return true;
    }

    public void Show(string username, Action onClosedCallback = null)
    {
        onClosed = onClosedCallback;

        KillAnims();

        if (usernameText != null)
        {
            string formatted;
            try
            {
                formatted = string.Format(usernameFormat, username ?? string.Empty);
            }
            catch
            {
                formatted = username ?? string.Empty;
            }

            usernameText.text = formatted;
        }

        string fullCommon = commonText != null ? commonText.text : string.Empty;
        string fullUsername = usernameText != null ? usernameText.text : string.Empty;

        if (commonText != null) commonText.text = string.Empty;
        if (usernameText != null) usernameText.text = string.Empty;

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        if (closeButton != null) closeButton.interactable = false;

        PlayIntro(fullCommon, fullUsername);
    }

    public void Close()
    {
        KillAnims();

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        Action cb = onClosed;
        onClosed = null;
        cb?.Invoke();
    }

    private void CacheTargets()
    {
        if (boardRect == null) boardRect = popupRoot != null ? popupRoot.GetComponentInChildren<RectTransform>() : null;

        if (boardRect != null) boardTargetPos = boardRect.anchoredPosition;
        if (congratulationsRect != null) congratsTargetPos = congratulationsRect.anchoredPosition;
    }

    private void KillAnims()
    {
        if (boardTween != null)
        {
            boardTween.Kill();
            boardTween = null;
        }

        if (congratsTween != null)
        {
            congratsTween.Kill();
            congratsTween = null;
        }

        if (boardRect != null) boardRect.DOKill();
        if (congratulationsRect != null) congratulationsRect.DOKill();
        if (commonText != null) commonText.DOKill();
        if (usernameText != null) usernameText.DOKill();

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
    }

    private void PlayIntro(string fullCommon, string fullUsername)
    {
        CacheTargets();

        if (boardRect != null)
        {
            boardRect.anchoredPosition = boardTargetPos + new Vector2(0f, dropFromYOffset);
        }

        if (congratulationsRect != null)
        {
            congratulationsRect.anchoredPosition = congratsTargetPos + new Vector2(0f, dropFromYOffset);
        }

        float boardDur = Mathf.Max(0.01f, boardDropDuration);
        float congratsDur = Mathf.Max(0.01f, congratsDropDuration);

        if (boardRect != null)
        {
            boardTween = boardRect
                .DOAnchorPos(boardTargetPos, boardDur)
                .SetEase(boardDropEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (congratulationsRect != null)
                    {
                        float delay = Mathf.Max(0f, afterBoardDelay);
                        congratsTween = congratulationsRect
                            .DOAnchorPos(congratsTargetPos, congratsDur)
                            .SetDelay(delay)
                            .SetEase(congratsDropEase)
                            .SetUpdate(true)
                            .OnComplete(() =>
                            {
                                float delay2 = Mathf.Max(0f, afterCongratsDelay);
                                typingRoutine = StartCoroutine(TypeSequence(fullCommon, fullUsername, delay2));
                            });
                    }
                    else
                    {
                        float delay2 = Mathf.Max(0f, afterBoardDelay);
                        typingRoutine = StartCoroutine(TypeSequence(fullCommon, fullUsername, delay2));
                    }
                });
        }
        else
        {
            typingRoutine = StartCoroutine(TypeSequence(fullCommon, fullUsername, 0f));
        }
    }

    private IEnumerator TypeSequence(string fullCommon, string fullUsername, float initialDelay)
    {
        float d0 = Mathf.Max(0f, initialDelay);
        if (d0 > 0f) yield return new WaitForSecondsRealtime(d0);

        if (commonText != null)
        {
            yield return TypeText(commonText, fullCommon, commonCharDelay);
        }

        if (usernameText != null)
        {
            yield return TypeText(usernameText, fullUsername, usernameCharDelay);
        }

        if (closeButton != null) closeButton.interactable = true;
    }

    private IEnumerator TypeText(TMP_Text target, string fullText, float perCharDelay)
    {
        if (target == null) yield break;

        string s = fullText ?? string.Empty;
        target.text = string.Empty;

        float dt = Mathf.Max(0f, perCharDelay);
        for (int i = 0; i < s.Length; i++)
        {
            target.text += s[i];
            if (dt > 0f) yield return new WaitForSecondsRealtime(dt);
        }
    }
}
