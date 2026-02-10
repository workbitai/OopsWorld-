using System;
using UnityEngine;

public static class DailyTaskPrefs
{
    public enum TaskId
    {
        LoginDaily = 0,
        Win3Times = 1,
        Watch3Ads = 2,
        PlayWithFriend = 3,
        Spend2Hour = 4
    }

    private const string Prefix = "DAILY_TASK_";
    private const string DayKey = Prefix + "DAY";

    private static string TodayKey => DateTime.UtcNow.ToString("yyyyMMdd");

    private static string ProgressKey(TaskId id) => Prefix + TodayKey + "_P_" + (int)id;
    private static string CompletedKey(TaskId id) => Prefix + TodayKey + "_C_" + (int)id;
    private static string SpendSecondsKey => Prefix + TodayKey + "_SPEND_SECONDS";

    private static int GetEffectiveTarget(TaskId id, int uiTarget)
    {
        if (id == TaskId.Spend2Hour) return 100;
        return uiTarget;
    }

    private static void EnsureDay()
    {
        string stored = PlayerPrefs.GetString(DayKey, string.Empty);
        string today = TodayKey;
        if (string.Equals(stored, today, StringComparison.Ordinal)) return;

        PlayerPrefs.SetString(DayKey, today);

        foreach (TaskId id in Enum.GetValues(typeof(TaskId)))
        {
            PlayerPrefs.SetInt(ProgressKey(id), 0);
            PlayerPrefs.SetInt(CompletedKey(id), 0);
        }

        PlayerPrefs.SetFloat(SpendSecondsKey, 0f);

        PlayerPrefs.Save();
    }

    public static int GetProgress(TaskId id)
    {
        EnsureDay();
        return Mathf.Max(0, PlayerPrefs.GetInt(ProgressKey(id), 0));
    }

    public static bool IsCompleted(TaskId id)
    {
        EnsureDay();
        return PlayerPrefs.GetInt(CompletedKey(id), 0) == 1;
    }

    public static void SetProgress(TaskId id, int progress, int target)
    {
        EnsureDay();
        progress = Mathf.Max(0, progress);

        int effectiveTarget = GetEffectiveTarget(id, target);

        if (effectiveTarget > 0 && progress >= effectiveTarget)
        {
            PlayerPrefs.SetInt(ProgressKey(id), effectiveTarget);
            PlayerPrefs.SetInt(CompletedKey(id), 1);
        }
        else
        {
            PlayerPrefs.SetInt(ProgressKey(id), progress);
        }

        PlayerPrefs.Save();
    }

    public static void AddProgress(TaskId id, int amount, int target)
    {
        if (amount <= 0) return;
        EnsureDay();
        if (IsCompleted(id)) return;

        int cur = GetProgress(id);
        SetProgress(id, cur + amount, target);
    }

    public static void Complete(TaskId id, int target)
    {
        EnsureDay();
        int effectiveTarget = GetEffectiveTarget(id, target);
        PlayerPrefs.SetInt(ProgressKey(id), Mathf.Max(GetProgress(id), Mathf.Max(0, effectiveTarget)));
        PlayerPrefs.SetInt(CompletedKey(id), 1);
        PlayerPrefs.Save();
    }

    public static float GetSpendSeconds()
    {
        EnsureDay();
        return Mathf.Max(0f, PlayerPrefs.GetFloat(SpendSecondsKey, 0f));
    }

    public static void SetSpendSeconds(float seconds)
    {
        EnsureDay();
        PlayerPrefs.SetFloat(SpendSecondsKey, Mathf.Max(0f, seconds));
        PlayerPrefs.Save();
    }

    public static void MarkLoginToday()
    {
        EnsureDay();
        Complete(TaskId.LoginDaily, 1);
    }

    public static int GetCompletedPoints(Func<TaskId, int> rewardPointsByTask)
    {
        EnsureDay();
        int sum = 0;
        foreach (TaskId id in Enum.GetValues(typeof(TaskId)))
        {
            if (id == TaskId.PlayWithFriend) continue;
            if (!IsCompleted(id)) continue;
            if (rewardPointsByTask == null) continue;
            sum += Mathf.Max(0, rewardPointsByTask(id));
        }
        return sum;
    }
}
