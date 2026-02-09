using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

namespace My.UI
{
    [AddComponentMenu("Haptics/Haptics Manager")]
    public class HapticsManager : MonoBehaviour
    {
        public static HapticsManager Instance { get; private set; }

        public enum HapticsStrengthPreset
        {
            Light = 0,
            Medium = 1,
            Hard = 2,
            Custom = 3,
        }

        [Header("Defaults")]
        [SerializeField] private bool enabledDefault = true;
        [SerializeField] private HapticsStrengthPreset strengthPresetDefault = HapticsStrengthPreset.Hard;
        [SerializeField, Range(0f, 1f)] private float strengthDefault = 1f;

        const string PREF_HAPTICS_ON = "hm_on";
        const string PREF_HAPTICS_STRENGTH = "hm_strength";
        const string PREF_HAPTICS_STRENGTH_PRESET = "hm_strength_preset";
        public bool Enabled { get; private set; }
        public float Strength01 { get; private set; }
        public HapticsStrengthPreset StrengthPreset { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
    private bool hasAmplitudeControl;
#elif UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _iOS_PlayHapticImpact(int type);
    [DllImport("__Internal")]
    private static extern void _iOS_PlayHapticNotification(int type);
    [DllImport("__Internal")]
    private static extern void _iOS_PlayHapticSelection();
#endif


        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Enabled = PlayerPrefs.GetInt(PREF_HAPTICS_ON, enabledDefault ? 1 : 0) == 1;

            StrengthPreset = (HapticsStrengthPreset)PlayerPrefs.GetInt(
                PREF_HAPTICS_STRENGTH_PRESET,
                (int)strengthPresetDefault);

            if (StrengthPreset == HapticsStrengthPreset.Custom)
                Strength01 = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_HAPTICS_STRENGTH, strengthDefault));
            else
                Strength01 = PresetToStrength01(StrengthPreset);

#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        int sdk = AndroidSDK();
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            // Android 12+ : VibratorManager -> getDefaultVibrator()
            if (sdk >= 31)
            {
                using (var ctx = new AndroidJavaClass("android.content.Context"))
                {
                    string svcMgr = ctx.GetStatic<string>("VIBRATOR_MANAGER_SERVICE");
                    var vibMgr = currentActivity.Call<AndroidJavaObject>("getSystemService", svcMgr);
                    if (vibMgr != null)
                        vibrator = vibMgr.Call<AndroidJavaObject>("getDefaultVibrator");
                }
            }

            // Fallback: classic Vibrator
            if (vibrator == null)
            {
                using (var ctx = new AndroidJavaClass("android.content.Context"))
                {
                    string svc = ctx.GetStatic<string>("VIBRATOR_SERVICE");
                    vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", svc);
                }
            }

            // SDK 26+ j hoy to j amplitude control check karo
            hasAmplitudeControl = (sdk >= 26) && (vibrator != null) && vibrator.Call<bool>("hasAmplitudeControl");
        }
    }
    catch (System.Exception e)
    {
        Debug.LogWarning("[Haptics] Init failed, fallback to no-op: " + e.Message);
        vibrator = null;
        hasAmplitudeControl = false;
    }
#endif
        }


        void OnApplicationQuit() => PlayerPrefs.Save();

        public void SetEnabled(bool on)
        {
            Enabled = on;
            PlayerPrefs.SetInt(PREF_HAPTICS_ON, on ? 1 : 0);
        }

        public void SetHapticsStrength01(float strength01)
        {
            StrengthPreset = HapticsStrengthPreset.Custom;
            Strength01 = Mathf.Clamp01(strength01);
            PlayerPrefs.SetFloat(PREF_HAPTICS_STRENGTH, Strength01);
            PlayerPrefs.SetInt(PREF_HAPTICS_STRENGTH_PRESET, (int)StrengthPreset);
        }

        public void SetHapticsStrengthPreset(HapticsStrengthPreset preset)
        {
            StrengthPreset = preset;
            PlayerPrefs.SetInt(PREF_HAPTICS_STRENGTH_PRESET, (int)StrengthPreset);

            if (StrengthPreset == HapticsStrengthPreset.Custom)
            {
                Strength01 = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_HAPTICS_STRENGTH, strengthDefault));
            }
            else
            {
                Strength01 = PresetToStrength01(StrengthPreset);
            }
        }

        private float PresetToStrength01(HapticsStrengthPreset preset)
        {
            switch (preset)
            {
                case HapticsStrengthPreset.Light: return 0.35f;
                case HapticsStrengthPreset.Medium: return 0.65f;
                case HapticsStrengthPreset.Hard: return 1f;
                case HapticsStrengthPreset.Custom: return Mathf.Clamp01(strengthDefault);
                default: return 1f;
            }
        }

        // ---------- Presets (mobile friendly) ----------
        public void Light()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticImpact(0);
#else
            Pulse(60, 120);
#endif
        }

        public void Medium()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticImpact(1);
#else
            Pulse(45, 180);
#endif
        }

        public void Heavy()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticImpact(2);
#else
            Pulse(60, 255);
#endif
        }

        public void Selection()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticSelection();
#else
            Pulse(40, 150);
#endif
        }

        public void Success()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticNotification(0);
#else
            Pattern(new long[] { 0, 35, 40, 30 }, new int[] { 120, 0, 200 });
#endif
        }

        public void Warning()
        {
            if (!Enabled) return;
            return;
        }

        public void Failure()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticNotification(2);
#else
            Pattern(new long[] { 0, 70, 40, 70, 40, 50 }, new int[] { 255, 0, 200, 0, 150 });
#endif
        }

        public void ButtonClick()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _iOS_PlayHapticSelection();
#else
            Pulse(30, 60);
#endif
        }


        /// <summary>Custom single pulse (ms, 0..255 amplitude). iOS uses Handheld.Vibrate fallback.</summary>
        public void Pulse(int durationMs, int amplitude = 255)
        {
            if (!Enabled) return;

            float s = Strength01;
            durationMs = Mathf.Max(1, Mathf.RoundToInt(durationMs * Mathf.Clamp(s, 0.1f, 1f)));
            amplitude = Mathf.Clamp(Mathf.RoundToInt(amplitude * s), 1, 255);

#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        int sdk = AndroidSDK();
        if (vibrator == null)
        {
            // last-resort simple fallback
            Handheld.Vibrate();
            return;
        }

        if (sdk >= 26)
        {
            using (var veClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                var effect = veClass.CallStatic<AndroidJavaObject>(
                    "createOneShot",
                    (long)Mathf.Max(1, durationMs),
                    Mathf.Clamp(amplitude, 1, 255));
                vibrator.Call("vibrate", effect);
            }
        }
        else
        {
            vibrator.Call("vibrate", (long)Mathf.Max(1, durationMs));
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning("[Haptics] Pulse failed, fallback vibrate: " + ex.Message);
        Handheld.Vibrate();
    }
#elif UNITY_IOS && !UNITY_EDITOR
    _iOS_PlayHapticImpact(1);
#else
            // Editor/others: no-op
#endif
        }


        /// <summary>Custom pattern. timings[0] = delay, then on/off segments. amplitudes length can be N or N-1 (Android handles both).</summary>
        public void Pattern(long[] timingsMs, int[] amplitudes = null)
        {
            if (!Enabled) return;

            int[] ampsScaled = null;
            if (amplitudes != null)
            {
                ampsScaled = new int[amplitudes.Length];
                float s = Strength01;
                for (int i = 0; i < amplitudes.Length; i++)
                {
                    if (amplitudes[i] <= 0) { ampsScaled[i] = 0; continue; }
                    ampsScaled[i] = Mathf.Clamp(Mathf.RoundToInt(amplitudes[i] * s), 1, 255);
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        int sdk = AndroidSDK();
        if (vibrator == null || timingsMs == null || timingsMs.Length == 0)
        {
            Handheld.Vibrate();
            return;
        }

        if (sdk >= 26)
        {
            using (var veClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                AndroidJavaObject effect;
                if (ampsScaled != null && ampsScaled.Length > 0)
                    effect = veClass.CallStatic<AndroidJavaObject>("createWaveform", timingsMs, ampsScaled, -1);
                else
                    effect = veClass.CallStatic<AndroidJavaObject>("createWaveform", timingsMs, -1);
                vibrator.Call("vibrate", effect);
            }
        }
        else
        {
            vibrator.Call("vibrate", timingsMs, -1);
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning("[Haptics] Pattern failed, fallback vibrate: " + ex.Message);
        Handheld.Vibrate();
    }
#elif UNITY_IOS && !UNITY_EDITOR
    _iOS_PlayHapticNotification(0);
#else
            // Editor: no-op
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
    private int AndroidSDK()
    {
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            return version.GetStatic<int>("SDK_INT");
    }
#endif
    }
}
