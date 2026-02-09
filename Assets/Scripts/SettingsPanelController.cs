using My.UI;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;

    [SerializeField] private Button musicButton;

    [SerializeField] private Button soundButton;

    [SerializeField] private Button vibrationButton;

    private Image musicButtonImage;
    private Image soundButtonImage;
    private Image vibrationButtonImage;

    [Header("Optional Audio")]
    [Tooltip("Assign if/when you add background music AudioSource.")]
    [SerializeField] private AudioSource musicSource;

    const string PREF_MUSIC_ON = "settings_music_on";
    const string PREF_SOUND_ON = "settings_sound_on";
    const string PREF_VIBRATION_ON = "settings_vibration_on";

    private bool suppressEvents;

    private void OnEnable()
    {
        CacheImages();
        LoadToUI();
        HookUI();
        ApplyAll();
    }

    private void OnDisable()
    {
        UnhookUI();
    }

    private void HookUI()
    {
        if (musicButton != null)
        {
            musicButton.onClick.RemoveListener(OnMusicButtonClicked);
            musicButton.onClick.AddListener(OnMusicButtonClicked);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveListener(OnSoundButtonClicked);
            soundButton.onClick.AddListener(OnSoundButtonClicked);
        }

        if (vibrationButton != null)
        {
            vibrationButton.onClick.RemoveListener(OnVibrationButtonClicked);
            vibrationButton.onClick.AddListener(OnVibrationButtonClicked);
        }
    }

    private void UnhookUI()
    {
        if (musicButton != null) musicButton.onClick.RemoveListener(OnMusicButtonClicked);
        if (soundButton != null) soundButton.onClick.RemoveListener(OnSoundButtonClicked);
        if (vibrationButton != null) vibrationButton.onClick.RemoveListener(OnVibrationButtonClicked);
    }

    private void LoadToUI()
    {
        suppressEvents = true;

        bool musicOn = PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) == 1;
        bool soundOn = PlayerPrefs.GetInt(PREF_SOUND_ON, 1) == 1;
        bool vibrationOn = PlayerPrefs.GetInt(PREF_VIBRATION_ON, 1) == 1;

        UpdateButtonVisuals(musicButtonImage, musicOn);
        UpdateButtonVisuals(soundButtonImage, soundOn);
        UpdateButtonVisuals(vibrationButtonImage, vibrationOn);

        suppressEvents = false;
    }

    private void CacheImages()
    {
        if (musicButtonImage == null && musicButton != null)
        {
            musicButtonImage = musicButton.GetComponent<Image>();
        }
        if (soundButtonImage == null && soundButton != null)
        {
            soundButtonImage = soundButton.GetComponent<Image>();
        }
        if (vibrationButtonImage == null && vibrationButton != null)
        {
            vibrationButtonImage = vibrationButton.GetComponent<Image>();
        }
    }

    private void UpdateButtonVisuals(Image img, bool on)
    {
        if (img == null) return;
        if (on && onSprite != null) img.sprite = onSprite;
        if (!on && offSprite != null) img.sprite = offSprite;
    }

    private void Save(string key, bool on)
    {
        PlayerPrefs.SetInt(key, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnMusicButtonClicked()
    {
        if (suppressEvents) return;
        bool on = PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) != 1;
        Save(PREF_MUSIC_ON, on);
        UpdateButtonVisuals(musicButtonImage, on);
        ApplyMusic(on);
    }

    private void OnSoundButtonClicked()
    {
        if (suppressEvents) return;
        bool on = PlayerPrefs.GetInt(PREF_SOUND_ON, 1) != 1;
        Save(PREF_SOUND_ON, on);
        UpdateButtonVisuals(soundButtonImage, on);
        ApplySound(on);
    }

    private void OnVibrationButtonClicked()
    {
        if (suppressEvents) return;
        bool on = PlayerPrefs.GetInt(PREF_VIBRATION_ON, 1) != 1;
        Save(PREF_VIBRATION_ON, on);
        UpdateButtonVisuals(vibrationButtonImage, on);
        ApplyVibration(on);
    }

    private void ApplyAll()
    {
        bool musicOn = PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) == 1;
        bool soundOn = PlayerPrefs.GetInt(PREF_SOUND_ON, 1) == 1;
        bool vibrationOn = PlayerPrefs.GetInt(PREF_VIBRATION_ON, 1) == 1;

        ApplyMusic(musicOn);
        ApplySound(soundOn);
        ApplyVibration(vibrationOn);
    }

    private void ApplyMusic(bool on)
    {
        if (musicSource != null)
        {
            musicSource.mute = !on;
        }
    }

    private void ApplySound(bool on)
    {
        // Fallback: global volume. Later you can replace this with AudioMixer groups.
        AudioListener.volume = on ? 1f : 0f;
    }

    private void ApplyVibration(bool on)
    {
        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.SetEnabled(on);
        }
    }
}
