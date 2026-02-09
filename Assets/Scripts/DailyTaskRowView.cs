using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyTaskRowView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text taskNameText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private Image fillImage;

    private bool warnedMissingFill;
    private bool warnedFillType;

    public void SetTaskName(string name)
    {
        if (taskNameText != null)
        {
            taskNameText.text = name;
        }
    }

    public void SetPointsText(string text)
    {
        if (pointsText != null)
        {
            pointsText.text = text;
        }
    }

    public void SetProgress01(float value01)
    {
        if (Application.isPlaying)
        {
            if (fillImage == null)
            {
                if (!warnedMissingFill)
                {
                    warnedMissingFill = true;
                    Debug.LogWarning($"DailyTaskRowView: Fill Image is not assigned on row '{gameObject.name}'.", this);
                }
                return;
            }

            if (fillImage.type != Image.Type.Filled)
            {
                if (!warnedFillType)
                {
                    warnedFillType = true;
                    Debug.LogWarning($"DailyTaskRowView: Fill Image type is '{fillImage.type}' (must be Filled) on row '{gameObject.name}'.", fillImage);
                }
            }
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = Mathf.Clamp01(value01);
        }
    }
}
