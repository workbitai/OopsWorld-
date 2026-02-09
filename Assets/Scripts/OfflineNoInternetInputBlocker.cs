using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class OfflineNoInternetInputBlocker : MonoBehaviour, IPointerDownHandler, ISelectHandler
{
    [SerializeField] private TMP_InputField targetInput;
    [SerializeField] private string message = null;

    private bool pendingClear;

    private void Awake()
    {
        if (targetInput == null)
        {
            targetInput = GetComponent<TMP_InputField>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetInput != null && !targetInput.interactable)
        {
            if (eventData != null)
            {
                eventData.Use();
            }
            return;
        }

        if (!NoInternetStrip.BlockIfOffline(message)) return;

        if (eventData != null)
        {
            eventData.Use();
        }

        DeactivateAndDeselect();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (targetInput != null && !targetInput.interactable)
        {
            if (eventData != null)
            {
                eventData.Use();
            }
            DeactivateAndDeselect();
            return;
        }

        if (!NoInternetStrip.BlockIfOffline(message)) return;

        if (eventData != null)
        {
            eventData.Use();
        }

        DeactivateAndDeselect();
    }

    private void DeactivateAndDeselect()
    {
        if (targetInput != null)
        {
            targetInput.DeactivateInputField();
        }

        TryClearSelectionNextFrame();
    }

    private void TryClearSelectionNextFrame()
    {
        if (pendingClear) return;
        if (EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject != gameObject) return;

        pendingClear = true;
        StartCoroutine(ClearSelectionNextFrame());
    }

    private System.Collections.IEnumerator ClearSelectionNextFrame()
    {
        yield return null;
        pendingClear = false;

        if (EventSystem.current == null) yield break;
        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
