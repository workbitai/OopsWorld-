using UnityEngine;

public class CanvasSetting : MonoBehaviour
{
    private UnityEngine.UI.CanvasScaler canvasScaler;

    private void Awake()
    {
        canvasScaler = GetComponent<UnityEngine.UI.CanvasScaler>();
    }

    private void Start()
    {
        SetMatchRatio();
    }

    private void SetMatchRatio()
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height; 
        float scaleFactor = screenHeight / screenWidth;
        float standardFactor = 16f / 9f;

        canvasScaler.matchWidthOrHeight = (scaleFactor < standardFactor) ? 1 : 0;
    }
}
