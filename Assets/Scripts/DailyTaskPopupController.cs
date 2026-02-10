using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyTaskPopupController : MonoBehaviour
{
    [Serializable]
    private class TaskState
    {
        public string title;
        public int target;
        public int rewardPoints;
        public int progress;
        public bool completed;

        public float Progress01
        {
            get
            {
                if (progress <= 0) return 0f;

                if (target > 0)
                {
                    float byTarget = (float)progress / target;
                    if (progress > target && progress <= 100)
                    {
                        return Mathf.Clamp01(progress / 100f);
                    }

                    return Mathf.Clamp01(byTarget);
                }

                if (progress <= 100)
                {
                    return Mathf.Clamp01(progress / 100f);
                }

                return 0f;
            }
        }
    }

    [Header("Rows (exactly 5)")]
    [SerializeField] private List<DailyTaskRowView> rows = new List<DailyTaskRowView>(5);

    [Header("Top progress bar (optional)")]
    [SerializeField] private Image topFillImage;

    [Header("Top Texts (optional)")]
    [SerializeField] private List<TMP_Text> topCoinTexts = new List<TMP_Text>();
    [SerializeField] private List<TMP_Text> topPointTexts = new List<TMP_Text>();

    [Header("Top Text Values (manual)")]
    [SerializeField] private List<float> topCoinValues = new List<float>();
    [SerializeField] private List<float> topPointValues = new List<float>();

    [Header("Setup")]
    [SerializeField] private bool autoInitOnEnable = true;

    [SerializeField] private bool autoRefreshInPlayMode = true;

    [Header("Task Data (do not add extra)")]
    [SerializeField] private List<TaskState> tasks = new List<TaskState>(5)
    {
        new TaskState { title = "Login Daily", target = 1, rewardPoints = 50 },
        new TaskState { title = "Win 3 Times", target = 3, rewardPoints = 50 },
        new TaskState { title = "watch 3 ads", target = 3, rewardPoints = 50 },
        new TaskState { title = "Play with Friend", target = 1, rewardPoints = 50 },
        new TaskState { title = "Spend 2 hour", target = 2, rewardPoints = 50 },
    };

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!autoInitOnEnable) return;
        DailyTaskPrefs.MarkLoginToday();
        RefreshUI();
    }

    private int lastStateHash;

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!autoRefreshInPlayMode) return;

        int h = CalculateStateHash();
        if (h == lastStateHash) return;

        lastStateHash = h;
        RefreshUI();
    }

    public void RefreshUI()
    {
        EnsureTaskCount();

        ApplyCanonicalTaskDefinitions();

        SyncFromPrefs();

        lastStateHash = CalculateStateHash();

        int rowIndex = 0;
        for (int i = 0; i < tasks.Count; i++)
        {
            if (IsSkippedTaskIndex(i)) continue;
            if (rowIndex >= rows.Count) break;

            var row = rows[rowIndex];
            if (row != null)
            {
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                var t = tasks[i];
                row.SetTaskName(t.title);
                row.SetPointsText(t.rewardPoints + " Point");
                row.SetProgress01(t.completed ? 1f : t.Progress01);
            }

            rowIndex++;
        }

        for (int i = rowIndex; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null) continue;
            if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
        }

        RefreshTopProgress();
    }

    private void SyncFromPrefs()
    {
        if (tasks == null) return;

        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            if (t == null) continue;

            if (!TryGetTaskIdByIndex(i, out DailyTaskPrefs.TaskId id))
            {
                t.progress = 0;
                t.completed = false;
                continue;
            }

            t.progress = DailyTaskPrefs.GetProgress(id);
            t.completed = DailyTaskPrefs.IsCompleted(id);
        }
    }

    private bool TryGetTaskIdByIndex(int taskIndex, out DailyTaskPrefs.TaskId id)
    {
        id = DailyTaskPrefs.TaskId.LoginDaily;
        if (taskIndex == 0) { id = DailyTaskPrefs.TaskId.LoginDaily; return true; }
        if (taskIndex == 1) { id = DailyTaskPrefs.TaskId.Win3Times; return true; }
        if (taskIndex == 2) { id = DailyTaskPrefs.TaskId.Watch3Ads; return true; }
        if (taskIndex == 3) { id = DailyTaskPrefs.TaskId.PlayWithFriend; return true; }
        if (taskIndex == 4) { id = DailyTaskPrefs.TaskId.Spend2Hour; return true; }
        return false;
    }

    private bool IsSkippedTaskIndex(int taskIndex) => taskIndex == 3;

    private int CalculateStateHash()
    {
        unchecked
        {
            int h = 17;
            if (tasks == null) return h;

            for (int i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                if (t == null) continue;

                h = (h * 31) + (t.title != null ? t.title.GetHashCode() : 0);
                h = (h * 31) + t.target;
                h = (h * 31) + t.rewardPoints;
                h = (h * 31) + t.progress;
                h = (h * 31) + (t.completed ? 1 : 0);
            }

            if (topCoinValues != null)
            {
                for (int i = 0; i < topCoinValues.Count; i++)
                {
                    h = (h * 31) + topCoinValues[i].GetHashCode();
                }
            }

            if (topPointValues != null)
            {
                for (int i = 0; i < topPointValues.Count; i++)
                {
                    h = (h * 31) + topPointValues[i].GetHashCode();
                }
            }

            return h;
        }
    }

    private void RefreshTopProgress()
    {
        RefreshTopTexts();

        if (topFillImage == null) return;

        int completedPoints = 0;
        int totalPoints = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            if (IsSkippedTaskIndex(i)) continue;
            var t = tasks[i];
            if (t == null) continue;

            totalPoints += Mathf.Max(0, t.rewardPoints);
            if (t.completed)
            {
                completedPoints += Mathf.Max(0, t.rewardPoints);
            }
        }

        float denom = 0f;
        if (topPointValues != null && topPointValues.Count > 0)
        {
            denom = Mathf.Max(0f, topPointValues[topPointValues.Count - 1]);
        }
        if (denom <= 0f)
        {
            denom = Mathf.Max(1f, totalPoints);
        }

        float value01 = Mathf.Clamp01(completedPoints / denom);
        topFillImage.fillAmount = value01;
    }

    private void RefreshTopTexts()
    {
        if (topCoinTexts != null)
        {
            for (int i = 0; i < topCoinTexts.Count; i++)
            {
                var t = topCoinTexts[i];
                if (t == null) continue;

                float v = (topCoinValues != null && i < topCoinValues.Count) ? topCoinValues[i] : 0f;
                t.text = v.ToString("0");
            }
        }

        if (topPointTexts != null)
        {
            for (int i = 0; i < topPointTexts.Count; i++)
            {
                var t = topPointTexts[i];
                if (t == null) continue;

                float v = (topPointValues != null && i < topPointValues.Count) ? topPointValues[i] : 0f;
                t.text = v.ToString("0");
            }
        }
    }

    private void EnsureTaskCount()
    {
        if (tasks == null) tasks = new List<TaskState>();

        while (tasks.Count < 5) tasks.Add(new TaskState());
        if (tasks.Count > 5) tasks.RemoveRange(5, tasks.Count - 5);
    }

    private void ApplyCanonicalTaskDefinitions()
    {
        EnsureTaskCount();

        if (tasks[0] != null) { tasks[0].title = "Login Daily"; tasks[0].target = 1; tasks[0].rewardPoints = 50; }
        if (tasks[1] != null) { tasks[1].title = "Win 3 Times"; tasks[1].target = 3; tasks[1].rewardPoints = 50; }
        if (tasks[2] != null) { tasks[2].title = "watch 3 ads"; tasks[2].target = 3; tasks[2].rewardPoints = 50; }
        if (tasks[3] != null) { tasks[3].title = "Play with Friend"; tasks[3].target = 1; tasks[3].rewardPoints = 50; }
        if (tasks[4] != null) { tasks[4].title = "Spend 2 hour"; tasks[4].target = 2; tasks[4].rewardPoints = 50; }
    }

    public void AddProgress(int taskIndex, int amount)
    {
        EnsureTaskCount();
        if (taskIndex < 0 || taskIndex >= tasks.Count) return;
        if (amount <= 0) return;

        if (!TryGetTaskIdByIndex(taskIndex, out DailyTaskPrefs.TaskId id)) return;
        if (id == DailyTaskPrefs.TaskId.PlayWithFriend) return;

        var t = tasks[taskIndex];
        DailyTaskPrefs.AddProgress(id, amount, t != null ? t.target : 0);
        RefreshUI();
    }

    public void CompleteTask(int taskIndex)
    {
        EnsureTaskCount();
        if (taskIndex < 0 || taskIndex >= tasks.Count) return;

        if (!TryGetTaskIdByIndex(taskIndex, out DailyTaskPrefs.TaskId id)) return;
        if (id == DailyTaskPrefs.TaskId.PlayWithFriend) return;

        var t = tasks[taskIndex];
        DailyTaskPrefs.Complete(id, t != null ? t.target : 0);
        RefreshUI();
    }

    public void ResetAll()
    {
        EnsureTaskCount();
        for (int i = 0; i < tasks.Count; i++)
        {
            if (!TryGetTaskIdByIndex(i, out DailyTaskPrefs.TaskId id)) continue;
            DailyTaskPrefs.SetProgress(id, 0, tasks[i] != null ? tasks[i].target : 0);
        }

        RefreshUI();
    }

    [ContextMenu("Debug/Refresh UI")]
    private void DebugRefreshUI() => RefreshUI();

    [ContextMenu("Debug/Reset All")]
    private void DebugResetAll() => ResetAll();

    [ContextMenu("Debug/Add Progress Task 1")]
    private void DebugAddProgress1() => AddProgress(0, 1);

    [ContextMenu("Debug/Add Progress Task 2")]
    private void DebugAddProgress2() => AddProgress(1, 1);

    [ContextMenu("Debug/Add Progress Task 3")]
    private void DebugAddProgress3() => AddProgress(2, 1);

    [ContextMenu("Debug/Add Progress Task 4")]
    private void DebugAddProgress4() => AddProgress(3, 1);

    [ContextMenu("Debug/Add Progress Task 5")]
    private void DebugAddProgress5() => AddProgress(4, 1);
}
