using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelCarousel : MonoBehaviour
{
    [SerializeField] private RectTransform container;
    [SerializeField] private List<RectTransform> pages = new List<RectTransform>();
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private bool loop = true;

    private int index;
    private bool isAnimating;

    private void Awake()
    {
        if (container == null)
        {
            container = transform as RectTransform;
        }
    }

    private void OnEnable()
    {
        HookButtons();
        InitializeLayout();
    }

    private void OnDisable()
    {
        UnhookButtons();
    }

    private void HookButtons()
    {
        if (leftButton != null)
        {
            leftButton.onClick.RemoveListener(Prev);
            leftButton.onClick.AddListener(Prev);
        }

        if (rightButton != null)
        {
            rightButton.onClick.RemoveListener(Next);
            rightButton.onClick.AddListener(Next);
        }
    }

    private void UnhookButtons()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(Prev);
        if (rightButton != null) rightButton.onClick.RemoveListener(Next);
    }

    private void InitializeLayout()
    {
        if (pages == null || pages.Count == 0) return;

        float w = GetWidth();
        for (int i = 0; i < pages.Count; i++)
        {
            RectTransform p = pages[i];
            if (p == null) continue;
            p.SetParent(container, true);
            p.anchorMin = new Vector2(0.5f, 0.5f);
            p.anchorMax = new Vector2(0.5f, 0.5f);
            p.pivot = new Vector2(0.5f, 0.5f);
            p.anchoredPosition = (i == index) ? Vector2.zero : new Vector2(w * 2f, 0f);
            p.gameObject.SetActive(i == index);
        }

        UpdateButtonInteractable();
    }

    private float GetWidth()
    {
        if (container == null) return 0f;
        float w = container.rect.width;
        if (w <= 0f)
        {
            Canvas c = container.GetComponentInParent<Canvas>();
            if (c != null)
            {
                RectTransform r = c.transform as RectTransform;
                if (r != null) w = r.rect.width;
            }
        }
        return Mathf.Max(1f, w);
    }

    public void Next()
    {
        Slide(+1);
    }

    public void Prev()
    {
        Slide(-1);
    }

    private void Slide(int dir)
    {
        if (isAnimating) return;
        if (pages == null || pages.Count == 0) return;

        int next = index + dir;
        if (loop)
        {
            next = (next % pages.Count + pages.Count) % pages.Count;
        }
        else
        {
            next = Mathf.Clamp(next, 0, pages.Count - 1);
        }

        if (next == index) return;

        RectTransform current = pages[index];
        RectTransform target = pages[next];
        if (current == null || target == null) return;

        float w = GetWidth();
        float fromX = dir > 0 ? w : -w;
        float toX = dir > 0 ? -w : w;

        isAnimating = true;

        target.gameObject.SetActive(true);
        target.anchoredPosition = new Vector2(fromX, 0f);

        DOTween.Kill(current);
        DOTween.Kill(target);

        Sequence s = DOTween.Sequence();
        s.Join(current.DOAnchorPos(new Vector2(toX, 0f), animDuration).SetEase(ease));
        s.Join(target.DOAnchorPos(Vector2.zero, animDuration).SetEase(ease));
        s.OnComplete(() =>
        {
            current.gameObject.SetActive(false);
            current.anchoredPosition = new Vector2(w * 2f, 0f);
            index = next;
            isAnimating = false;
            UpdateButtonInteractable();
        });
    }

    private void UpdateButtonInteractable()
    {
        if (loop) return;
        if (leftButton != null) leftButton.interactable = index > 0;
        if (rightButton != null) rightButton.interactable = index < pages.Count - 1;
    }

    public void SetIndex(int newIndex)
    {
        if (pages == null || pages.Count == 0) return;
        newIndex = Mathf.Clamp(newIndex, 0, pages.Count - 1);
        index = newIndex;
        InitializeLayout();
    }
}
