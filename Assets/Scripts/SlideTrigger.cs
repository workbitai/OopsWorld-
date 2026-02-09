using UnityEngine;

public class SlideTrigger : MonoBehaviour
{
    [Header("Slide Settings")]
    [Tooltip("Which player/color can use this slide")]
    public int ownerPlayer = 1;

    [Tooltip("How many steps to slide forward on outer track")]
    public int slideSteps = 4;

    [Header("Slide Visual Swap")]
    public GameObject slideVisualObject;
}
