using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayerFindingProfileSpinner : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Image profileImage;

    [Header("Auto")]
    [SerializeField] private bool playOnEnable = true;

    [Header("Swipe Animation")]
    [SerializeField] private bool useSwipeAnimation = true;
    [SerializeField, Min(1f)] private float swipeDistance = 140f;

    [Header("Sprites")]
    [SerializeField] private List<Sprite> profileSprites = new List<Sprite>();

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float spinDurationSeconds = 3f;
    [SerializeField] private bool randomizeSpinDuration = false;
    [SerializeField, Min(0.1f)] private float randomSpinMinSeconds = 2f;
    [SerializeField, Min(0.1f)] private float randomSpinMaxSeconds = 6f;
    [SerializeField, Min(0.02f)] private float changeIntervalSeconds = 0.06f;

    [SerializeField] private bool loopUntilStopped = false;

    private Coroutine spinCoroutine;
    private bool stopRequested;

    public event Action<bool> SpinStateChanged;
    public event Action SpinCompleted;

    private float currentSpinDurationSeconds;

    private Image nextImage;
    private RectTransform profileRt;
    private RectTransform nextRt;

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        Stop();

        if (profileImage == null) return;
        if (profileSprites == null || profileSprites.Count == 0) return;

        CacheOrCreateSwipeObjects();

        stopRequested = false;
        currentSpinDurationSeconds = GetSpinDurationSeconds();

        SpinStateChanged?.Invoke(true);
        spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private float GetSpinDurationSeconds()
    {
        if (!randomizeSpinDuration)
        {
            return Mathf.Max(0.1f, spinDurationSeconds);
        }

        float min = Mathf.Max(0.1f, randomSpinMinSeconds);
        float max = Mathf.Max(min, randomSpinMaxSeconds);
        return UnityEngine.Random.Range(min, max);
    }

    public void Stop()
    {
        stopRequested = true;
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }

        SpinStateChanged?.Invoke(false);
    }

    public void SetRandomSpriteImmediate()
    {
        SetRandomSpriteImmediate(exclude: null);
    }

    public Sprite SetRandomSpriteImmediate(ICollection<Sprite> exclude)
    {
        if (profileImage == null) return null;
        if (profileSprites == null || profileSprites.Count == 0) return null;

        Sprite chosen = null;

        if (exclude != null && exclude.Count > 0)
        {
            List<Sprite> candidates = null;
            for (int i = 0; i < profileSprites.Count; i++)
            {
                Sprite s = profileSprites[i];
                if (s == null) continue;
                if (exclude.Contains(s)) continue;

                if (candidates == null) candidates = new List<Sprite>();
                candidates.Add(s);
            }

            if (candidates != null && candidates.Count > 0)
            {
                chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }
        }

        if (chosen == null)
        {
            chosen = profileSprites[UnityEngine.Random.Range(0, profileSprites.Count)];
        }

        if (chosen != null)
        {
            profileImage.sprite = chosen;
        }

        return chosen;
    }

    public void StopAndComplete()
    {
        Stop();
        SpinCompleted?.Invoke();
    }

    public void SetLoopUntilStopped(bool value)
    {
        loopUntilStopped = value;
    }

    private IEnumerator SpinRoutine()
    {
        float duration = Mathf.Max(0.1f, currentSpinDurationSeconds);
        float interval = Mathf.Max(0.02f, changeIntervalSeconds);

        float endTime = Time.unscaledTime + duration;
        int lastIndex = -1;

        while (loopUntilStopped ? !stopRequested : (Time.unscaledTime < endTime))
        {
            int idx = UnityEngine.Random.Range(0, profileSprites.Count);
            if (profileSprites.Count > 1)
            {
                while (idx == lastIndex)
                {
                    idx = UnityEngine.Random.Range(0, profileSprites.Count);
                }
            }

            lastIndex = idx;
            Sprite s = profileSprites[idx];
            if (s != null)
            {
                if (useSwipeAnimation && CanUseSwipeAnimation())
                {
                    yield return SwipeToSprite(s, interval);
                }
                else
                {
                    profileImage.sprite = s;
                    yield return new WaitForSecondsRealtime(interval);
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        spinCoroutine = null;

        if (!loopUntilStopped)
        {
            SpinStateChanged?.Invoke(false);
            SpinCompleted?.Invoke();
        }
    }

    private void CacheOrCreateSwipeObjects()
    {
        if (profileImage == null) return;

        profileRt = profileImage.rectTransform;
        if (profileRt == null) return;

        if (nextImage != null)
        {
            nextRt = nextImage.rectTransform;
            return;
        }

        GameObject go = new GameObject("NextProfileImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(profileImage.transform.parent, worldPositionStays: false);
        go.transform.SetAsLastSibling();

        nextImage = go.GetComponent<Image>();
        nextRt = nextImage.rectTransform;

        nextRt.anchorMin = profileRt.anchorMin;
        nextRt.anchorMax = profileRt.anchorMax;
        nextRt.pivot = profileRt.pivot;
        nextRt.sizeDelta = profileRt.sizeDelta;
        nextRt.anchoredPosition = profileRt.anchoredPosition;
        nextRt.localRotation = profileRt.localRotation;
        nextRt.localScale = profileRt.localScale;

        nextImage.raycastTarget = false;
        nextImage.type = profileImage.type;
        nextImage.preserveAspect = profileImage.preserveAspect;
        nextImage.material = profileImage.material;
        nextImage.color = profileImage.color;
        nextImage.sprite = profileImage.sprite;

        nextImage.enabled = false;
    }

    private bool CanUseSwipeAnimation()
    {
        return profileImage != null && nextImage != null && profileRt != null && nextRt != null;
    }

    private IEnumerator SwipeToSprite(Sprite nextSprite, float durationSeconds)
    {
        if (!CanUseSwipeAnimation()) yield break;

        float dist = Mathf.Max(1f, swipeDistance);
        float dur = Mathf.Max(0.02f, durationSeconds);

        Vector2 center = Vector2.zero;
        Vector2 startNext = new Vector2(0f, dist);
        Vector2 endCurrent = new Vector2(0f, -dist);

        nextImage.sprite = nextSprite;
        nextImage.enabled = true;

        profileRt.anchoredPosition = center;
        nextRt.anchoredPosition = startNext;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);

            profileRt.anchoredPosition = Vector2.LerpUnclamped(center, endCurrent, a);
            nextRt.anchoredPosition = Vector2.LerpUnclamped(startNext, center, a);

            yield return null;
        }

        profileImage.sprite = nextSprite;
        profileRt.anchoredPosition = center;
        nextRt.anchoredPosition = center;
        nextImage.enabled = false;
    }
}
