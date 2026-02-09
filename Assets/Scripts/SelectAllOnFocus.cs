using TMPro;
using UnityEngine;

public class SelectAllOnFocus : MonoBehaviour
{
    TMP_InputField input;
    private Coroutine selectRoutine;

    void Awake()
    {
        input = GetComponent<TMP_InputField>();
    }

    public void SelectAllText(string value)
    {
        // thodik delay jaruri hoy che click event complete thava mate
        if (input == null) return;

        if (selectRoutine != null)
        {
            StopCoroutine(selectRoutine);
            selectRoutine = null;
        }

        selectRoutine = StartCoroutine(SelectAllNextFrame());
    }

    private System.Collections.IEnumerator SelectAllNextFrame()
    {
        // Mobile par actual focus/keyboard next frame ma set thay che, etle next frame wait.
        yield return null;

        if (input == null) yield break;

        input.ActivateInputField();
        input.Select();

        // Sometimes one more frame is needed on mobile.
        yield return null;

        if (input == null) yield break;

        string text = input.text ?? string.Empty;
        int len = text.Length;

        input.ForceLabelUpdate();

        // Select all with highlight
        TrySetIntProperty(input, "stringPosition", 0);
        TrySetIntProperty(input, "stringSelectPosition", len);
        input.selectionStringAnchorPosition = 0;
        input.selectionStringFocusPosition = len;
        input.caretPosition = len;

        selectRoutine = null;
    }

    private static void TrySetIntProperty(TMP_InputField field, string propertyName, int value)
    {
        if (field == null) return;

        var p = typeof(TMP_InputField).GetProperty(propertyName);
        if (p == null || !p.CanWrite) return;
        if (p.PropertyType != typeof(int)) return;

        p.SetValue(field, value, null);
    }
}
