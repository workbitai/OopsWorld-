using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using NewGame.API;

public class ProfileSelectionController : MonoBehaviour
{
    private const string PrefPlayerName = "PLAYER_NAME";
    private const string PrefAvatarIndex = "PLAYER_AVATAR_INDEX";

    [Serializable]
    private class UpdateUserRequest
    {
        public string user_id;
        public string username;
        public int avatar;
    }

    [Header("Self Profile")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button saveSelectionButton;
    [SerializeField] private Image selfPlayerAvatarImage;

    [Header("Avatar Selection")]
    [SerializeField] private List<Button> avatarButtons = new List<Button>();
    [SerializeField] private RectTransform selectedRing;
    [SerializeField] private Image selectedPreviewImage;

    [Header("Behavior")]
    [SerializeField] private bool saveOnEveryChange;
    [SerializeField] private float internetPollSeconds = 0.5f;

    private int selectedIndex;
    private bool suppress;

    private List<UnityAction> avatarClickActions = new List<UnityAction>();
    private readonly List<GameObject> perButtonSelectedRings = new List<GameObject>();

    private UnityAction<string> nameInputSelectAction;

    private bool pendingNameInputDeselect;

    private Coroutine internetWatchCoroutine;
    private NetworkReachability lastReachability;

    public int AvatarCount => avatarButtons != null ? avatarButtons.Count : 0;

    public Sprite GetAvatarSpriteForIndexPublic(int idx)
    {
        return GetSpriteForIndex(idx);
    }

    private void Awake()
    {
        CachePerButtonRingsIfNeeded();
    }

    private void OnEnable()
    {
        CachePerButtonRingsIfNeeded();
        WireAvatarButtons();
        WireNameSave();
        WireNameInput();
        EnsureOfflineInputBlocker();
        EnsureOfflineRaycastOverlay();
        StartInternetWatch();
        LoadToUI();
    }

    private void OnDisable()
    {
        StopInternetWatch();
        UnwireAvatarButtons();
        UnwireNameSave();
        UnwireNameInput();
    }

    private void StartInternetWatch()
    {
        StopInternetWatch();

        lastReachability = Application.internetReachability;
        ApplyNameInputInternetState(lastReachability != NetworkReachability.NotReachable);
        internetWatchCoroutine = StartCoroutine(WatchInternetReachability());
    }

    private void StopInternetWatch()
    {
        if (internetWatchCoroutine != null)
        {
            StopCoroutine(internetWatchCoroutine);
            internetWatchCoroutine = null;
        }
    }

    private IEnumerator WatchInternetReachability()
    {
        float interval = Mathf.Max(0.1f, internetPollSeconds);
        while (true)
        {
            yield return new WaitForSecondsRealtime(interval);

            NetworkReachability now = Application.internetReachability;
            if (now == lastReachability) continue;

            lastReachability = now;
            ApplyNameInputInternetState(now != NetworkReachability.NotReachable);
        }
    }

    private void ApplyNameInputInternetState(bool online)
    {
        if (nameInput == null) return;

        if (nameInput.interactable == online) return;

        nameInput.interactable = online;
        if (!online)
        {
            nameInput.DeactivateInputField();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == nameInput.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    private void CachePerButtonRingsIfNeeded()
    {
        if (selectedRing != null) return;
        if (avatarButtons == null) return;

        if (perButtonSelectedRings.Count == avatarButtons.Count) return;

        perButtonSelectedRings.Clear();
        for (int i = 0; i < avatarButtons.Count; i++)
        {
            Button b = avatarButtons[i];
            if (b == null)
            {
                perButtonSelectedRings.Add(null);
                continue;
            }

            perButtonSelectedRings.Add(FindSelectedRingChild(b.transform));
        }
    }

    private void EnsureOfflineInputBlocker()
    {
        if (nameInput == null) return;
        if (nameInput.GetComponent<OfflineNoInternetInputBlocker>() != null) return;

        nameInput.gameObject.AddComponent<OfflineNoInternetInputBlocker>();
    }

    private void EnsureOfflineRaycastOverlay()
    {
        if (nameInput == null) return;

        Transform existing = nameInput.transform.Find("OfflineNoInternetOverlay");
        if (existing != null) return;

       GameObject go = new GameObject("OfflineNoInternetOverlay", typeof(RectTransform), typeof(Image));
        go.layer = nameInput.gameObject.layer;
        go.transform.SetParent(nameInput.transform, false);
        go.transform.SetAsLastSibling();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
    }

    private GameObject FindSelectedRingChild(Transform root)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c == null) continue;

            string n = c.name != null ? c.name.ToLowerInvariant() : string.Empty;
            if (n.Contains("selected") && n.Contains("ring"))
            {
                return c.gameObject;
            }
        }

        return null;
    }

    private void WireAvatarButtons()
    {
        if (avatarButtons == null) return;

        avatarClickActions.Clear();
        for (int i = 0; i < avatarButtons.Count; i++)
        {
            avatarClickActions.Add(null);
        }

        for (int i = 0; i < avatarButtons.Count; i++)
        {
            int idx = i;
            Button b = avatarButtons[i];
            if (b == null) continue;

            UnityAction a = () => OnAvatarClicked(idx);
            avatarClickActions[i] = a;
            b.onClick.AddListener(a);
        }
    }

    private void UnwireAvatarButtons()
    {
        if (avatarButtons == null) return;

        for (int i = 0; i < avatarButtons.Count; i++)
        {
            Button b = avatarButtons[i];
            if (b == null) continue;

            if (avatarClickActions != null && i < avatarClickActions.Count && avatarClickActions[i] != null)
            {
                b.onClick.RemoveListener(avatarClickActions[i]);
            }
        }

        avatarClickActions.Clear();
    }

    private void WireNameSave()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveName);
        }

        if (saveSelectionButton != null)
        {
            saveSelectionButton.onClick.AddListener(SaveSelection);
        }
    }

    private void UnwireNameSave()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveName);
        }

        if (saveSelectionButton != null)
        {
            saveSelectionButton.onClick.RemoveListener(SaveSelection);
        }
    }

    private void WireNameInput()
    {
        if (nameInput == null) return;
        if (nameInputSelectAction != null) return;

        nameInputSelectAction = _ => HandleNameInputSelected();
        nameInput.onSelect.AddListener(nameInputSelectAction);
    }

    private void UnwireNameInput()
    {
        if (nameInput == null) return;
        if (nameInputSelectAction == null) return;

        nameInput.onSelect.RemoveListener(nameInputSelectAction);
        nameInputSelectAction = null;
    }

    private void HandleNameInputSelected()
    {
        if (!NoInternetStrip.BlockIfOffline()) return;

        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
        }

        TryDeselectNameInputNextFrame();
    }

    private void TryDeselectNameInputNextFrame()
    {
        if (pendingNameInputDeselect) return;
        if (EventSystem.current == null) return;
        if (nameInput == null) return;
        if (EventSystem.current.currentSelectedGameObject != nameInput.gameObject) return;

        pendingNameInputDeselect = true;
        StartCoroutine(DeselectNameInputNextFrame());
    }

    private IEnumerator DeselectNameInputNextFrame()
    {
        yield return null;
        pendingNameInputDeselect = false;

        if (EventSystem.current == null) yield break;
        if (nameInput == null) yield break;

        if (EventSystem.current.currentSelectedGameObject == nameInput.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void LoadToUI()
    {
        suppress = true;

        string storedName = PlayerPrefs.GetString(PrefPlayerName, string.Empty);
        if (GameManager.Instance != null)
        {
            storedName = GameManager.Instance.PlayerName;
        }
        if (nameInput != null)
        {
            nameInput.SetTextWithoutNotify(storedName);
        }

        int storedIndex = PlayerPrefs.GetInt(PrefAvatarIndex, 0);
        if (GameManager.Instance != null)
        {
            storedIndex = GameManager.Instance.PlayerAvatarIndex;
        }
        SetSelectedIndex(Mathf.Clamp(storedIndex, 0, Mathf.Max(0, avatarButtons.Count - 1)));

        suppress = false;
    }

    private void OnAvatarClicked(int idx)
    {
        if (suppress) return;

        if (NoInternetStrip.BlockIfOffline()) return;

        SetSelectedIndex(idx);

        if (saveOnEveryChange)
        {
            SaveSelection();
        }
    }

    private void SetSelectedIndex(int idx)
    {
        selectedIndex = Mathf.Clamp(idx, 0, Mathf.Max(0, avatarButtons.Count - 1));

        if (selectedRing != null)
        {
            Button b = selectedIndex >= 0 && selectedIndex < avatarButtons.Count ? avatarButtons[selectedIndex] : null;
            if (b != null)
            {
                selectedRing.gameObject.SetActive(true);
                selectedRing.SetParent(b.transform, worldPositionStays: false);
                selectedRing.SetAsLastSibling();
                selectedRing.anchoredPosition = Vector2.zero;
            }
            else
            {
                selectedRing.gameObject.SetActive(false);
            }
        }
        else
        {
            CachePerButtonRingsIfNeeded();
            if (perButtonSelectedRings != null)
            {
                for (int i = 0; i < perButtonSelectedRings.Count; i++)
                {
                    GameObject ring = perButtonSelectedRings[i];
                    if (ring == null) continue;
                    ring.SetActive(i == selectedIndex);
                }
            }
        }

        if (selectedPreviewImage != null)
        {
            Sprite s = GetSpriteForIndex(selectedIndex);
            if (s != null)
            {
                selectedPreviewImage.sprite = s;
            }
        }

        if (selfPlayerAvatarImage != null)
        {
            Sprite s = GetSpriteForIndex(selectedIndex);
            if (s != null)
            {
                selfPlayerAvatarImage.sprite = s;
            }
        }

        if (!suppress)
        {
            if (GameManager.Instance != null)
            {
                Sprite s = GetSpriteForIndex(selectedIndex);
                GameManager.Instance.SetPlayerAvatar(selectedIndex, s, saveToPrefs: true);
            }
            else
            {
                PlayerPrefs.SetInt(PrefAvatarIndex, selectedIndex);
                PlayerPrefs.Save();
            }
        }
    }

    private Sprite GetSpriteForIndex(int idx)
    {
        if (avatarButtons == null) return null;
        if (idx < 0 || idx >= avatarButtons.Count) return null;

        Button b = avatarButtons[idx];
        if (b == null) return null;

        Image img = b.GetComponent<Image>();
        if (img != null && img.sprite != null) return img.sprite;

        Image[] images = b.GetComponentsInChildren<Image>(true);
        if (images != null)
        {
            for (int i = 0; i < images.Length; i++)
            {
                Image im = images[i];
                if (im == null) continue;
                if (im.sprite == null) continue;
                return im.sprite;
            }
        }

        return img != null ? img.sprite : null;
    }

    public void SaveName()
    {
        if (NoInternetStrip.BlockIfOffline()) return;

        string value = nameInput != null ? nameInput.text : string.Empty;
        value = value != null ? value.Trim() : string.Empty;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerName(value, saveToPrefs: true);
            SendUpdateUserApi();
            return;
        }

        PlayerPrefs.SetString(PrefPlayerName, value);
        PlayerPrefs.Save();

        SendUpdateUserApi();
    }

    public void SaveSelection()
    {
        if (NoInternetStrip.BlockIfOffline()) return;

        Sprite s = GetSpriteForIndex(selectedIndex);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerAvatar(selectedIndex, s, saveToPrefs: true);
            SendUpdateUserApi();
            return;
        }

        PlayerPrefs.SetInt(PrefAvatarIndex, selectedIndex);
        PlayerPrefs.Save();

        SendUpdateUserApi();
    }

    public void SaveAll()
    {
        if (NoInternetStrip.BlockIfOffline()) return;

        SaveName();
        SaveSelection();
    }

    private void SendUpdateUserApi()
    {
        UserSession.LoadFromPrefs();

        if (string.IsNullOrWhiteSpace(UserSession.UserId))
        {
            return;
        }

        ApiManager api = ApiManager.Instance != null ? ApiManager.Instance : FindObjectOfType<ApiManager>();
        if (api == null)
        {
            return;
        }

        string username = nameInput != null ? nameInput.text : string.Empty;
        username = username != null ? username.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            username = UserSession.Username;
        }

        UpdateUserRequest req = new UpdateUserRequest
        {
            user_id = UserSession.UserId,
            username = username,
            avatar = Mathf.Clamp(selectedIndex + 1, 1, Mathf.Max(1, AvatarCount))
        };

        string json = JsonUtility.ToJson(req);

        api.Post(
            api.GetUserUpdateApiUrl(),
            json,
            _ => { },
            _ => { }
        );
    }
}
