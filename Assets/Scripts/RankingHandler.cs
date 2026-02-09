using UnityEngine;
using UnityEngine.UI;

public class RankingHandler : MonoBehaviour
{
    public enum Tab
    {
        World,
        Friends
    }

    [Header("Buttons")]
    [SerializeField] private Button worldRankingButton = null;
    [SerializeField] private Button friendsRankingButton = null;

    [Header("Selected Visual (Optional)")]
    [SerializeField] private GameObject worldSelectedObject = null;
    [SerializeField] private GameObject friendsSelectedObject = null;

    [Header("Board Sprite")]
    [SerializeField] private Image boardImage = null;
    [SerializeField] private Sprite worldBoardSprite = null;
    [SerializeField] private Sprite friendsBoardSprite = null;

    [Header("Sections")]
    [SerializeField] private GameObject worldRankingSection = null;
    [SerializeField] private GameObject friendsRankingSection = null;

    [Header("Friends Login")]
    [SerializeField] private bool isLoggedIn = false;
    [SerializeField] private GameObject friendsLoggedInSection = null;
    [SerializeField] private GameObject friendsLoggedOutSection = null;

    [Header("Default")]
    [SerializeField] private Tab defaultTab = Tab.World;

    private bool listenersBound = false;

    private void OnEnable()
    {
        BindButtonListenersIfNeeded();
        ApplyTab(Tab.World);
    }

    private void OnDisable()
    {
        UnbindButtonListeners();
    }

    public void OnClickWorldRanking()
    {
        ApplyTab(Tab.World);
    }

    public void OnClickFriendsRanking()
    {
        ApplyTab(Tab.Friends);
        ApplyFriendsLoginSections();
    }

    private void BindButtonListenersIfNeeded()
    {
        if (listenersBound) return;

        if (worldRankingButton != null)
        {
            worldRankingButton.onClick.RemoveListener(OnClickWorldRanking);
            worldRankingButton.onClick.AddListener(OnClickWorldRanking);
        }
        if (friendsRankingButton != null)
        {
            friendsRankingButton.onClick.RemoveListener(OnClickFriendsRanking);
            friendsRankingButton.onClick.AddListener(OnClickFriendsRanking);
        }

        listenersBound = true;
    }

    private void UnbindButtonListeners()
    {
        if (!listenersBound) return;

        if (worldRankingButton != null)
        {
            worldRankingButton.onClick.RemoveListener(OnClickWorldRanking);
        }
        if (friendsRankingButton != null)
        {
            friendsRankingButton.onClick.RemoveListener(OnClickFriendsRanking);
        }

        listenersBound = false;
    }

    public void SetLoggedIn(bool loggedIn)
    {
        isLoggedIn = loggedIn;
        ApplyFriendsLoginSections();
    }

    public void SetDefaultTab(int tab)
    {
        defaultTab = (Tab)Mathf.Clamp(tab, 0, 1);
        ApplyTab(defaultTab);
    }

    private void ApplyTab(Tab tab)
    {
        if (worldRankingSection != null) worldRankingSection.SetActive(tab == Tab.World);
        if (friendsRankingSection != null) friendsRankingSection.SetActive(tab == Tab.Friends);

        if (boardImage != null)
        {
            Sprite target = (tab == Tab.World) ? worldBoardSprite : friendsBoardSprite;
            if (target != null)
            {
                boardImage.sprite = target;
            }
        }

        if (worldSelectedObject != null) worldSelectedObject.SetActive(tab == Tab.World);
        if (friendsSelectedObject != null) friendsSelectedObject.SetActive(tab == Tab.Friends);

        if (tab == Tab.World)
        {
            if (friendsLoggedInSection != null) friendsLoggedInSection.SetActive(false);
            if (friendsLoggedOutSection != null) friendsLoggedOutSection.SetActive(false);
        }
    }

    private void ApplyFriendsLoginSections()
    {
        if (friendsLoggedInSection != null) friendsLoggedInSection.SetActive(isLoggedIn);
        if (friendsLoggedOutSection != null) friendsLoggedOutSection.SetActive(!isLoggedIn);
    }
}
