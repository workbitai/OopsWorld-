using UnityEngine;
using UnityEngine.UI;
using My.UI;

public class UISfxButton : MonoBehaviour
{
    [SerializeField] private SoundManager.SfxId sfx = SoundManager.SfxId.Click;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(Play);
            button.onClick.AddListener(Play);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Play);
        }
    }

    public void Play()
    {
        if (SoundManager.Instance == null) return;
        SoundManager.Instance.Play(sfx);
    }

    public void Play(SoundManager.SfxId id)
    {
        if (SoundManager.Instance == null) return;
        SoundManager.Instance.Play(id);
    }
}
