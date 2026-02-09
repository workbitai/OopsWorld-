using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LogoAnimator : MonoBehaviour
{
    [Header("Auto Play")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private float startDelay = 0.1f;

    [Header("Pieces")]
    [SerializeField] private RectTransform character;
    [SerializeField] private RectTransform oopsBack;
    [SerializeField] private RectTransform worldBackRibbon;
    [SerializeField] private List<RectTransform> worldLetters = new List<RectTransform>();

    [Header("Next Animation")]
    [SerializeField] private MenuItemsAnimator menuItemsAnimator;

    [Header("Timing")]
    [SerializeField] private float characterDuration = 0.45f;
    [SerializeField] private float oopsDuration = 0.35f;
    [SerializeField] private float ribbonDuration = 0.35f;
    [SerializeField] private float worldLetterDuration = 0.28f;
    [SerializeField] private float worldLetterStagger = 0.06f;

    [Header("Post Logo Rotate")]
    [SerializeField] private bool playCharacterRotateAfterLogo = true;
    [SerializeField] private float characterRotateDelay = 0.35f;
    [SerializeField] private float characterRotateAngle = 6f;
    [SerializeField] private float characterRotateHalfDuration = 0.35f;
    [SerializeField] private Ease characterRotateEase = Ease.InOutSine;

    [Header("WORLD Overshoot")]
    [SerializeField] private float worldLetterOvershootScale = 1.15f;
    [SerializeField] private float worldLetterOvershootUpDuration = 0.10f;
    [SerializeField] private float worldLetterOvershootDownDuration = 0.12f;

    [Header("Eases")]
    [SerializeField] private Ease characterEase = Ease.OutBack;
    [SerializeField] private Ease oopsEase = Ease.OutBack;
    [SerializeField] private Ease ribbonEase = Ease.OutCubic;
    [SerializeField] private Ease worldLetterEase = Ease.OutBack;

    private Sequence activeSeq;
    private Vector3 characterBaseScale;
    private bool characterBaseScaleCached;

    private Tween characterRotateTween;
    private float characterBaseRotationZ;

    private void OnEnable()
    {
        CacheCharacterBaseScale();
        if (menuItemsAnimator != null)
        {
            menuItemsAnimator.Hide();
        }
        ResetToHiddenState();
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Kill();
    }

    public void Play()
    {
        Kill();
        CacheCharacterBaseScale();
        ResetToHiddenState();

        activeSeq = DOTween.Sequence();
        activeSeq.SetAutoKill(true);

        if (startDelay > 0f)
        {
            activeSeq.AppendInterval(startDelay);
        }

        float t = 0f;

        if (worldBackRibbon != null)
        {
            activeSeq.Insert(t, worldBackRibbon.DOScale(1f, ribbonDuration).SetEase(ribbonEase));
        }

        t += Mathf.Max(0.1f, ribbonDuration * 0.9f);

        if (character != null)
        {
            activeSeq.Insert(t, character.DOScale(characterBaseScale, characterDuration).SetEase(characterEase));
        }

        t += Mathf.Max(0.1f, characterDuration * 0.75f);

        if (oopsBack != null)
        {
            activeSeq.Insert(t, oopsBack.DOScale(1f, oopsDuration).SetEase(oopsEase));
        }

        t += Mathf.Max(0.1f, oopsDuration * 0.65f);

        for (int i = 0; i < worldLetters.Count; i++)
        {
            RectTransform letter = worldLetters[i];
            if (letter == null) continue;

            float st = t + (i * worldLetterStagger);
            activeSeq.Insert(st, letter.DOScale(worldLetterOvershootScale, worldLetterOvershootUpDuration).SetEase(worldLetterEase));
            activeSeq.Insert(st + worldLetterOvershootUpDuration, letter.DOScale(1f, worldLetterOvershootDownDuration).SetEase(Ease.OutQuad));
        }

        activeSeq.OnComplete(() =>
        {
            if (menuItemsAnimator != null)
            {
                menuItemsAnimator.Play();
            }

            StartCharacterRotate();
        });
    }

    public void Kill()
    {
        if (activeSeq != null)
        {
            activeSeq.Kill();
            activeSeq = null;
        }

        if (characterRotateTween != null)
        {
            characterRotateTween.Kill();
            characterRotateTween = null;
        }

        if (character != null) character.DOKill();
        if (oopsBack != null) oopsBack.DOKill();
        if (worldBackRibbon != null) worldBackRibbon.DOKill();

        for (int i = 0; i < worldLetters.Count; i++)
        {
            if (worldLetters[i] != null) worldLetters[i].DOKill();
        }
    }

    private void ResetToHiddenState()
    {
        Kill();

        if (character != null)
        {
            CacheCharacterBaseScale();
            characterBaseRotationZ = character.localEulerAngles.z;
            character.localScale = Vector3.zero;
        }
        if (oopsBack != null) oopsBack.localScale = Vector3.zero;
        if (worldBackRibbon != null) worldBackRibbon.localScale = Vector3.zero;

        for (int i = 0; i < worldLetters.Count; i++)
        {
            if (worldLetters[i] != null) worldLetters[i].localScale = Vector3.zero;
        }
    }

    private void CacheCharacterBaseScale()
    {
        if (character == null) return;

        Vector3 s = character.localScale;
        if (!characterBaseScaleCached && s.y > 0.0001f)
        {
            characterBaseScale = s;
            characterBaseScaleCached = true;
        }
        else if (characterBaseScaleCached && characterBaseScale.y <= 0.0001f && s.y > 0.0001f)
        {
            characterBaseScale = s;
        }
        else if (!characterBaseScaleCached)
        {
            characterBaseScale = new Vector3(s.x == 0 ? 1f : s.x, 1f, s.z == 0 ? 1f : s.z);
        }
    }

    private void StartCharacterRotate()
    {
        if (!playCharacterRotateAfterLogo) return;
        if (character == null) return;

        if (characterRotateTween != null)
        {
            characterRotateTween.Kill();
            characterRotateTween = null;
        }

        characterBaseRotationZ = character.localEulerAngles.z;
        float targetZ = characterBaseRotationZ + characterRotateAngle;

        characterRotateTween = character
            .DOLocalRotate(new Vector3(0f, 0f, targetZ), characterRotateHalfDuration, RotateMode.Fast)
            .SetEase(characterRotateEase)
            .SetDelay(Mathf.Max(0f, characterRotateDelay))
            .SetLoops(-1, LoopType.Yoyo)
            .OnKill(() =>
            {
                if (character != null)
                {
                    Vector3 e = character.localEulerAngles;
                    character.localEulerAngles = new Vector3(e.x, e.y, characterBaseRotationZ);
                }
            });
    }
}
