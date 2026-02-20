using System;
using System.Collections.Generic;
using UnityEngine;

namespace My.UI
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        public enum SfxId
        {
            Click = 0,
            PopupOpen = 1,
            PopupClose = 2,
            Win = 3,
            Lose = 4,
        }

        [Serializable]
        public class SfxClip
        {
            public SfxId id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField] private bool playMusicOnStart = true;

        [Header("SFX Library")]
        [SerializeField] private List<SfxClip> clips = new List<SfxClip>();

        private readonly Dictionary<SfxId, SfxClip> clipMap = new Dictionary<SfxId, SfxClip>();

        const string PREF_MUSIC_ON = "settings_music_on";
        const string PREF_SOUND_ON = "settings_sound_on";

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
                if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            }

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            sfxSource.playOnAwake = false;

            RebuildMap();

            ApplyMusicPreference();
            if (playMusicOnStart) PlayMusic();
        }

        private void OnValidate()
        {
            if (clips == null) clips = new List<SfxClip>();
            RebuildMap();
        }

        private void RebuildMap()
        {
            clipMap.Clear();
            if (clips == null) return;

            for (int i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                if (entry == null) continue;
                clipMap[entry.id] = entry;
            }
        }

        public bool IsSoundOn()
        {
            return PlayerPrefs.GetInt(PREF_SOUND_ON, 1) == 1;
        }

        public bool IsMusicOn()
        {
            return PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) == 1;
        }

        public void ApplyMusicPreference()
        {
            if (musicSource == null) return;
            musicSource.mute = !IsMusicOn();
        }

        public void PlayMusic()
        {
            if (musicSource == null) return;
            ApplyMusicPreference();
            musicSource.volume = Mathf.Clamp01(musicVolume);

            if (musicClip != null && musicSource.clip != musicClip)
            {
                musicSource.clip = musicClip;
            }

            if (musicSource.clip == null) return;
            if (!musicSource.isPlaying) musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource == null) return;
            if (musicSource.isPlaying) musicSource.Stop();
        }

        public void Play(SfxId id)
        {
            if (!IsSoundOn()) return;
            if (sfxSource == null) return;

            if (!clipMap.TryGetValue(id, out var entry) || entry == null || entry.clip == null) return;

            sfxSource.PlayOneShot(entry.clip, Mathf.Clamp01(entry.volume));
        }
    }
}
