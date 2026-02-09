using System.Collections;
using UnityEngine;

public class PurchasedPieceEffect : MonoBehaviour
{
    [SerializeField] private float delay = 0f;
    [SerializeField] private Animator animator;

    private Coroutine playRoutine;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        StartEffect();
    }

    private void OnDisable()
    {
        StopEffect();
    }

    public void StartEffect()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        StopEffect();
        playRoutine = StartCoroutine(PlayLoop());
    }

    public void StopEffect()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    private IEnumerator PlayLoop()
    {
        yield return null;

        bool firstPlay = true;

        while (isActiveAndEnabled)
        {
            if (animator == null)
            {
                yield break;
            }

            if (!firstPlay && delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            animator.enabled = true;
            animator.Play(0, 0, 0f);

            firstPlay = false;

            float duration = GetDuration();
            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        }
    }

    private float GetDuration()
    {
        if (animator == null) return 0.5f;

        RuntimeAnimatorController c = animator.runtimeAnimatorController;
        if (c == null) return 0.5f;

        AnimationClip[] clips = c.animationClips;
        if (clips == null || clips.Length == 0) return 0.5f;

        float max = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null) continue;
            max = Mathf.Max(max, clip.length);
        }

        return max > 0f ? max : 0.5f;
    }
}
