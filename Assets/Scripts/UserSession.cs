using System;
using UnityEngine;

public static class UserSession
{
    public const string UserIdKey = "USER_ID";
    public const string UsernameKey = "USER_USERNAME";
    public const string AvatarIndexKey = "USER_AVATAR_INDEX";
    public const string IsGuestKey = "USER_IS_GUEST";
    public const string JwtTokenKey = "JWT_TOKEN";

    public static string UserId { get; private set; } = string.Empty;
    public static string Username { get; private set; } = string.Empty;
    public static int AvatarIndex { get; private set; } = 0;
    public static bool IsGuest { get; private set; } = false;
    public static int Coins { get; private set; } = 0;
    public static int Diamonds { get; private set; } = 0;
    public static string JwtToken { get; private set; } = string.Empty;

    [Serializable]
    private class LoginApiResponse
    {
        public bool success;
        public LoginApiData data;
    }

    [Serializable]
    private class LoginApiData
    {
        public string username;
        public string user_id;
        public int avatar;
        public bool isGuest;
        public int coins;
        public int diamonds;
        public string jwtToken;
    }

    public static bool TryApplyLoginResponse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        LoginApiResponse parsed;
        try
        {
            parsed = JsonUtility.FromJson<LoginApiResponse>(rawJson);
        }
        catch
        {
            return false;
        }

        if (parsed == null || !parsed.success || parsed.data == null) return false;

        string username = parsed.data.username ?? string.Empty;
        string userId = parsed.data.user_id ?? string.Empty;

        int unityAvatarIndex = Mathf.Max(0, parsed.data.avatar - 1);

        Apply(
            username: username,
            userId: userId,
            avatarIndex: unityAvatarIndex,
            isGuest: parsed.data.isGuest,
            coins: Mathf.Max(0, parsed.data.coins),
            diamonds: Mathf.Max(0, parsed.data.diamonds),
            jwtToken: parsed.data.jwtToken ?? string.Empty,
            saveToPrefs: true
        );

        return true;
    }

    public static void Apply(
        string username,
        string userId,
        int avatarIndex,
        bool isGuest,
        int coins,
        int diamonds,
        string jwtToken,
        bool saveToPrefs)
    {
        Username = username ?? string.Empty;
        UserId = userId ?? string.Empty;
        AvatarIndex = Mathf.Max(0, avatarIndex);
        IsGuest = isGuest;
        Coins = Mathf.Max(0, coins);
        Diamonds = Mathf.Max(0, diamonds);
        JwtToken = jwtToken ?? string.Empty;

        if (!saveToPrefs) return;

        PlayerPrefs.SetString(UsernameKey, Username);
        PlayerPrefs.SetString(UserIdKey, UserId);
        PlayerPrefs.SetInt(AvatarIndexKey, AvatarIndex);
        PlayerPrefs.SetInt(IsGuestKey, IsGuest ? 1 : 0);
        PlayerPrefs.SetString(JwtTokenKey, JwtToken);

        PlayerPrefs.SetString(GameManager.PlayerNameKey, Username);
        PlayerPrefs.SetInt(GameManager.PlayerAvatarIndexKey, AvatarIndex);

        PlayerPrefs.SetInt("PLAYER_COINS", Coins);
        PlayerPrefs.SetInt("PLAYER_DIAMONDS", Diamonds);

        PlayerPrefs.Save();
    }

    public static void LoadFromPrefs()
    {
        Username = PlayerPrefs.GetString(UsernameKey, string.Empty);
        UserId = PlayerPrefs.GetString(UserIdKey, string.Empty);
        AvatarIndex = Mathf.Max(0, PlayerPrefs.GetInt(AvatarIndexKey, 0));
        IsGuest = PlayerPrefs.GetInt(IsGuestKey, 0) == 1;
        JwtToken = PlayerPrefs.GetString(JwtTokenKey, string.Empty);

        Coins = Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_COINS", 0));
        Diamonds = Mathf.Max(0, PlayerPrefs.GetInt("PLAYER_DIAMONDS", 0));
    }
}
