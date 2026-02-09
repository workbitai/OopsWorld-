/*
 * Player Path Manager - 2 players na simple paths manage karva mate
 * Har player na route path ni list aur home path ni list
 */

using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class PlayerPaths
{
    [Tooltip("Player nu route path (order vise - TopRow, DownRow, LeftRow, RightRow, etc.)")]
    public List<Transform> routePath = new List<Transform>();

    [Tooltip("Player nu home path (final path to home - order vise)")]
    public List<Transform> homePath = new List<Transform>();

    [Header("Start/Home Position (Path Count ma nahi aave)")]
  //  public Transform startHomePosition = null;

    [Tooltip("Start/Home position initially on/off (default: false = off/closed)")]
    public bool startPositionEnabled = false;
}

public class PlayerPathManager : MonoBehaviour
{
    [Header("Player 1 Paths")]
    [Tooltip("Player 1 na route path aur home path")]
    public PlayerPaths player1Paths = new PlayerPaths();

    [Header("Player 2 Paths")]
    [Tooltip("Player 2 na route path aur home path")]
    public PlayerPaths player2Paths = new PlayerPaths();

    [Header("Player 3 Paths")]
    [Tooltip("Player 3 na route path aur home path")]
    public PlayerPaths player3Paths = new PlayerPaths();

    [Header("Player 4 Paths")]
    [Tooltip("Player 4 na route path aur home path")]
    public PlayerPaths player4Paths = new PlayerPaths();

    [Header("Game Settings")]
    [Tooltip("Total active players (2 ya 4)")]
    [Range(2, 4)]
    public int playerCount = 2;

    [Header("Player UI")]
    [SerializeField] private TMP_Text player1UserNameText;
    [SerializeField] private TMP_Text player2UserNameText;
    [SerializeField] private TMP_Text player3UserNameText;
    [SerializeField] private TMP_Text player4UserNameText;

    [SerializeField] private Image player1AvatarImage;
    [SerializeField] private Image player2AvatarImage;
    [SerializeField] private Image player3AvatarImage;
    [SerializeField] private Image player4AvatarImage;

    [Header("Player Profile Roots")]
    [SerializeField] private GameObject player1ProfileRoot;
    [SerializeField] private GameObject player2ProfileRoot;
    [SerializeField] private GameObject player3ProfileRoot;
    [SerializeField] private GameObject player4ProfileRoot;

    [SerializeField] private float profilePopStartScale = 0f;
    [SerializeField] private float profilePopEndScale = 1.15f;
    [SerializeField] private float profilePopDuration = 0.22f;
    [SerializeField] private Ease profilePopEase = Ease.OutBack;

    private bool clearProfilesOnDisable;

    public void RequestClearProfilesOnNextDisable()
    {
        clearProfilesOnDisable = true;
    }

    public void SetPlayerProfile(int playerNumber, string playerName, Sprite avatarSprite)
    {
        clearProfilesOnDisable = false;

        TMP_Text nameText = GetUserNameText(playerNumber);
        if (nameText != null && !string.IsNullOrEmpty(playerName))
        {
            nameText.text = playerName;
            Debug.Log($"PlayerPathManager.SetPlayerProfile: Set name for P{playerNumber} = '{playerName}' ({nameText.gameObject.name})");
        }

        Image avatarImage = GetAvatarImage(playerNumber);
        if (avatarImage != null && avatarSprite != null)
        {
            avatarImage.sprite = avatarSprite;
            Debug.Log($"PlayerPathManager.SetPlayerProfile: Set avatar for P{playerNumber} sprite={avatarSprite.name} ({avatarImage.gameObject.name})");
        }
    }

    public void SetGameplayProfilesVisible(bool visible, bool resetScale)
    {
        int count = GetPlayerCount();
        for (int p = 1; p <= count; p++)
        {
            GameObject root = GetProfileRoot(p);
            if (root == null) continue;

            if (resetScale)
            {
                Transform t = root.transform;
                if (t != null)
                {
                    t.DOKill();
                    t.localScale = visible ? Vector3.one : Vector3.zero;
                }
            }

            root.SetActive(visible);
        }
    }

    public void PopGameplayProfiles()
    {
        int count = GetPlayerCount();
        float dur = Mathf.Max(0.01f, profilePopDuration);
        float startS = Mathf.Max(0f, profilePopStartScale);
        float endS = Mathf.Max(0.01f, profilePopEndScale);

        for (int p = 1; p <= count; p++)
        {
            GameObject root = GetProfileRoot(p);
            if (root == null) continue;
            root.SetActive(true);

            Transform t = root.transform;
            if (t == null) continue;

            t.DOKill();
            t.localScale = Vector3.one * startS;
            t.DOScale(Vector3.one * endS, dur).SetEase(profilePopEase);
        }
    }

    private GameObject GetProfileRoot(int playerNumber)
    {
        if (playerNumber == 1) return player1ProfileRoot;
        if (playerNumber == 2) return player2ProfileRoot;
        if (playerNumber == 3) return player3ProfileRoot;
        if (playerNumber == 4) return player4ProfileRoot;
        return null;
    }

    private TMP_Text GetUserNameText(int playerNumber)
    {
        if (playerNumber == 1) return player1UserNameText;
        if (playerNumber == 2) return player2UserNameText;
        if (playerNumber == 3) return player3UserNameText;
        if (playerNumber == 4) return player4UserNameText;
        return null;
    }

    private Image GetAvatarImage(int playerNumber)
    {
        if (playerNumber == 1) return player1AvatarImage;
        if (playerNumber == 2) return player2AvatarImage;
        if (playerNumber == 3) return player3AvatarImage;
        if (playerNumber == 4) return player4AvatarImage;
        return null;
    }

    /// <summary>
    /// Player na paths get karo
    /// </summary>
    public PlayerPaths GetPlayerPaths(int playerNumber)
    {
        if (playerNumber == 1)
            return player1Paths;
        else if (playerNumber == 2)
            return player2Paths;
        else if (playerNumber == 3)
            return player3Paths;
        else if (playerNumber == 4)
            return player4Paths;
        
        Debug.LogError($"Invalid player number: {playerNumber}");
        return null;
    }

    public int GetPlayerCount()
    {
        return Mathf.Clamp(playerCount, 2, 4);
    }

    /// <summary>
    /// Player na route path get karo
    /// </summary>
    public List<Transform> GetPlayerRoutePath(int playerNumber)
    {
        PlayerPaths playerPaths = GetPlayerPaths(playerNumber);
        return playerPaths != null ? playerPaths.routePath : null;
    }

    /// <summary>
    /// Player na home path get karo
    /// </summary>
    public List<Transform> GetPlayerHomePath(int playerNumber)
    {
        PlayerPaths playerPaths = GetPlayerPaths(playerNumber);
        return playerPaths != null ? playerPaths.homePath : null;
    }

    /// <summary>
    /// Player na complete path get karo (route + home)
    /// NOTE: Start/Home position path count ma nahi aave (separate object che)
    /// </summary>
    public List<Transform> GetCompletePlayerPath(int playerNumber)
    {
        PlayerPaths playerPaths = GetPlayerPaths(playerNumber);
        if (playerPaths == null) return null;

        List<Transform> completePath = new List<Transform>();

        // Route path add karo
        foreach (var position in playerPaths.routePath)
        {
            if (position != null)
            {
                completePath.Add(position);
            }
        }

        // Home path add karo
        foreach (var position in playerPaths.homePath)
        {
            if (position != null)
            {
                completePath.Add(position);
            }
        }

        // NOTE: startHomePosition path count ma nahi add kariye (separate object che)

        return completePath;
    }

    /// <summary>
    /// Specific position get karo (route + home combined)
    /// </summary>
    public Transform GetPathPosition(int playerNumber, int positionIndex)
    {
        List<Transform> completePath = GetCompletePlayerPath(playerNumber);
        if (completePath == null || positionIndex < 0 || positionIndex >= completePath.Count)
        {
            return null;
        }

        return completePath[positionIndex];
    }

    /// <summary>
    /// Total path length get karo (route + home)
    /// </summary>
    public int GetPathLength(int playerNumber)
    {
        List<Transform> completePath = GetCompletePlayerPath(playerNumber);
        return completePath != null ? completePath.Count : 0;
    }

    void Start()
    {
        // Validate paths
        ValidatePaths();
        
        // Start positions initialize karo (initially off/closed)
        InitializeStartPositions();
    }

    /// <summary>
    /// Start/Home positions initialize karo - initially off/closed rakho
    /// </summary>
    void InitializeStartPositions()
    {
        // Player 1 start position
       /* if (player1Paths.startHomePosition != null)
        {
            player1Paths.startHomePosition.gameObject.SetActive(player1Paths.startPositionEnabled);
            Debug.Log($"Player 1 start position initialized: {(player1Paths.startPositionEnabled ? "ON" : "OFF")}");
        }

        // Player 2 start position
        if (player2Paths.startHomePosition != null)
        {
            player2Paths.startHomePosition.gameObject.SetActive(player2Paths.startPositionEnabled);
            Debug.Log($"Player 2 start position initialized: {(player2Paths.startPositionEnabled ? "ON" : "OFF")}");
        }*/
    }

    /// <summary>
    /// Paths validate karo (check karo sabhi assign che ke nahi)
    /// </summary>
    void ValidatePaths()
    {
        ValidatePlayerPaths(player1Paths, 1);
        ValidatePlayerPaths(player2Paths, 2);
        ValidatePlayerPaths(player3Paths, 3);
        ValidatePlayerPaths(player4Paths, 4);
    }

    void ValidatePlayerPaths(PlayerPaths playerPaths, int playerNum)
    {
        if (playerPaths == null)
        {
            Debug.LogWarning($"Player {playerNum} paths not initialized!");
            return;
        }

      //  Debug.Log($"=== Player {playerNum} Paths ===");
        
        int routePathCount = 0;
        foreach (var pos in playerPaths.routePath)
        {
            if (pos != null) routePathCount++;
        }
       // Debug.Log($"  Route Path: {routePathCount}/{playerPaths.routePath.Count} positions");

        int homePathCount = 0;
        foreach (var pos in playerPaths.homePath)
        {
            if (pos != null) homePathCount++;
        }
       // Debug.Log($"  Home Path: {homePathCount}/{playerPaths.homePath.Count} positions");
        
        // Start/Home position check karo
       /* if (playerPaths.startHomePosition != null)
        {
            Debug.Log($"  Start/Home Position: {(playerPaths.startPositionEnabled ? "ON" : "OFF")} - Path count ma nahi aave");
        }
        else
        {
            Debug.LogWarning($"  Start/Home Position: Not assigned!");
        }*/
    }

    /// <summary>
    /// Player na start/home position get karo (path count ma nahi aave)
    /// </summary>
  /*  public Transform GetPlayerStartHomePosition(int playerNumber)
    {
        PlayerPaths playerPaths = GetPlayerPaths(playerNumber);
        return playerPaths != null ? playerPaths.startHomePosition : null;
    }*/

    /// <summary>
    /// Player na start/home position enable/disable karo (on/off)
    /// </summary>
   /* public void SetPlayerStartHomePositionEnabled(int playerNumber, bool enabled)
    {
        PlayerPaths playerPaths = GetPlayerPaths(playerNumber);
        if (playerPaths != null && playerPaths.startHomePosition != null)
        {
            playerPaths.startPositionEnabled = enabled;
            playerPaths.startHomePosition.gameObject.SetActive(enabled);
            Debug.Log($"Player {playerNumber} start/home position: {(enabled ? "ON" : "OFF")}");
        }
    }*/

    /// <summary>
    /// Player na start/home position currently enabled che ke nahi check karo
    /// </summary>
    public bool IsPlayerStartHomePositionEnabled(int playerNumber)
    {
        PlayerPaths playerPaths = GetPlayerPaths(playerNumber);
        return playerPaths != null && playerPaths.startPositionEnabled;
    }

    private void OnDisable()
    {
        if (!clearProfilesOnDisable) return;
        clearProfilesOnDisable = false;

        for (int p = 1; p <= 4; p++)
        {
            TMP_Text t = GetUserNameText(p);
            if (t != null)
            {
                t.text = string.Empty;
              //  Debug.Log($"PlayerPathManager.OnDisable: Cleared name text for P{p} ({t.gameObject.name})");
            }

            Image img = GetAvatarImage(p);
            if (img != null)
            {
                img.sprite = null;
               // Debug.Log($"PlayerPathManager.OnDisable: Cleared avatar sprite for P{p} ({img.gameObject.name})");
            }
        }
    }
}

