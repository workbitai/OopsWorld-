using UnityEngine;

public class DailyTaskSpendTimeTracker : MonoBehaviour
{
    private const float TwoHoursSeconds = 2f * 60f * 60f;
    private float accumulator;
    private bool isPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (!Application.isPlaying) return;
        if (FindObjectOfType<DailyTaskSpendTimeTracker>() != null) return;

        GameObject go = new GameObject(nameof(DailyTaskSpendTimeTracker));
        go.AddComponent<DailyTaskSpendTimeTracker>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        isPaused = !Application.isFocused;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (isPaused) return;

        accumulator += Time.unscaledDeltaTime;
        if (accumulator < 1f) return;

        float stored = DailyTaskPrefs.GetSpendSeconds();
        stored += accumulator;
        accumulator = 0f;

        DailyTaskPrefs.SetSpendSeconds(stored);

        float pct01 = Mathf.Clamp01(stored / TwoHoursSeconds);
        int progressPct = Mathf.RoundToInt(pct01 * 100f);
        DailyTaskPrefs.SetProgress(DailyTaskPrefs.TaskId.Spend2Hour, progressPct, target: 2);
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            isPaused = true;
            Flush();
            return;
        }

        isPaused = false;
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            isPaused = true;
            Flush();
            return;
        }

        isPaused = false;
    }

    private void OnDisable()
    {
        Flush();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    private void Flush()
    {
        if (accumulator <= 0f) return;

        float stored = DailyTaskPrefs.GetSpendSeconds();
        stored += accumulator;
        accumulator = 0f;

        DailyTaskPrefs.SetSpendSeconds(stored);

        float pct01 = Mathf.Clamp01(stored / TwoHoursSeconds);
        int progressPct = Mathf.RoundToInt(pct01 * 100f);
        DailyTaskPrefs.SetProgress(DailyTaskPrefs.TaskId.Spend2Hour, progressPct, target: 2);
    }
}
