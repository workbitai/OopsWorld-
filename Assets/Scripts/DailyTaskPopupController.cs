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

        lastStateHash = CalculateStateHash();

        for (int i = 0; i < rows.Count && i < tasks.Count; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            var t = tasks[i];
            row.SetTaskName(t.title);
            row.SetPointsText(t.rewardPoints + " Point");
            row.SetProgress01(t.completed ? 1f : t.Progress01);
        }

        RefreshTopProgress();
    }

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

        float sum = 0f;
        int count = 0;

        for (int i = 1; i < tasks.Count && i <= 4; i++)
        {
            sum += tasks[i].completed ? 1f : tasks[i].Progress01;
            count++;
        }

        float value01 = count <= 0 ? 0f : Mathf.Clamp01(sum / count);
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

    public void AddProgress(int taskIndex, int amount)
    {
        EnsureTaskCount();
        if (taskIndex < 0 || taskIndex >= tasks.Count) return;
        if (amount <= 0) return;

        var t = tasks[taskIndex];
        if (t.completed) return;

        t.progress += amount;
        if (t.target > 0 && t.progress >= t.target)
        {
            t.progress = t.target;
            t.completed = true;
        }

        RefreshUI();
    }

    public void CompleteTask(int taskIndex)
    {
        EnsureTaskCount();
        if (taskIndex < 0 || taskIndex >= tasks.Count) return;

        var t = tasks[taskIndex];
        t.completed = true;
        t.progress = Mathf.Max(t.progress, t.target);
        RefreshUI();
    }

    public void ResetAll()
    {
        EnsureTaskCount();
        for (int i = 0; i < tasks.Count; i++)
        {
            tasks[i].progress = 0;
            tasks[i].completed = false;
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
