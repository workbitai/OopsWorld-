using System;
using System.Collections.Generic;
using NewGame.API;
using UnityEngine;

public static class GameWalletApi
{
    [Serializable]
    public sealed class LobbyGame
    {
        public string _id;
        public long coinsWon;
        public long diamondsWon;
        public long entryCoinsUsed;
        public int players;
        public bool isLock;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    private sealed class CreditUpdateResponse
    {
        public bool success;
        public bool status;
        public string message;
        public CreditUpdateData data;
    }

    [Serializable]
    private sealed class CreditUpdateData
    {
        public int coins;
        public int diamonds;
        public string expiresAt;
    }

    public static LobbyGame[] LastTwoPlayersGames { get; private set; } = Array.Empty<LobbyGame>();
    public static LobbyGame[] LastFourPlayersGames { get; private set; } = Array.Empty<LobbyGame>();

    [Serializable]
    private sealed class GameWalletSelectRequest
    {
        public string user_id;
    }

    [Serializable]
    private sealed class GameWalletSelectResponse
    {
        public bool status;
        public bool success;
        public GameWalletSelectData data;
    }

    [Serializable]
    private sealed class GameWalletSelectData
    {
        public int coins;
        public int diamonds;

        public LobbyGame[] twoPlayersGames;
        public LobbyGame[] fourPlayersGames;
    }

    public static void FetchAndApplyWallet(Action onSuccess = null, Action<string> onError = null)
    {
        UserSession.LoadFromPrefs();

        if (string.IsNullOrWhiteSpace(UserSession.UserId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UserSession.JwtToken))
        {
            return;
        }

        ApiManager api = ApiManager.Instance != null ? ApiManager.Instance : UnityEngine.Object.FindObjectOfType<ApiManager>();
        if (api == null)
        {
            return;
        }

        GameWalletSelectRequest request = new GameWalletSelectRequest
        {
            user_id = UserSession.UserId
        };

        string json = JsonUtility.ToJson(request);

        api.Post(
            api.GetGameWalletSelectUrl(),
            json,
            response =>
            {
                if (TryApplyWalletResponse(response, out string error))
                {
                    onSuccess?.Invoke();
                }
                else
                {
                    onError?.Invoke(error);
                }
            },
            error =>
            {
                onError?.Invoke(error);
            }
        );
    }

    private static bool TryApplyWalletResponse(string response, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(response))
        {
            error = "Empty response";
            return false;
        }

        GameWalletSelectResponse parsed;
        try
        {
            parsed = JsonUtility.FromJson<GameWalletSelectResponse>(response);
        }
        catch
        {
            error = "Invalid response";
            return false;
        }

        bool ok = parsed != null && (parsed.status || parsed.success);
        if (!ok || parsed.data == null)
        {
            error = "Request failed";
            return false;
        }

        int coins = Mathf.Max(0, parsed.data.coins);
        int diamonds = Mathf.Max(0, parsed.data.diamonds);

        UserSession.Apply(
            username: UserSession.Username,
            userId: UserSession.UserId,
            avatarIndex: UserSession.AvatarIndex,
            isGuest: UserSession.IsGuest,
            coins: coins,
            diamonds: diamonds,
            jwtToken: UserSession.JwtToken,
            saveToPrefs: true
        );

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetCoins(coins);
            PlayerWallet.Instance.SetDiamonds(diamonds);
        }

        LastTwoPlayersGames = parsed.data.twoPlayersGames ?? Array.Empty<LobbyGame>();
        LastFourPlayersGames = parsed.data.fourPlayersGames ?? Array.Empty<LobbyGame>();

        return true;
    }

    public static void CreditUpdate(int? coinsAmount = null, int? diamonds = null, Action onSuccess = null, Action<string> onError = null, bool refreshWalletAfter = true)
    {
        if (!coinsAmount.HasValue && !diamonds.HasValue)
        {
            onError?.Invoke("Nothing to update");
            return;
        }

        UserSession.LoadFromPrefs();

        if (string.IsNullOrWhiteSpace(UserSession.UserId))
        {
            onError?.Invoke("Missing user_id");
            return;
        }

        if (string.IsNullOrWhiteSpace(UserSession.JwtToken))
        {
            onError?.Invoke("Missing token");
            return;
        }

        ApiManager api = ApiManager.Instance != null ? ApiManager.Instance : UnityEngine.Object.FindObjectOfType<ApiManager>();
        if (api == null)
        {
            onError?.Invoke("ApiManager not found");
            return;
        }

        string json = BuildCreditUpdateJson(UserSession.UserId, coinsAmount, diamonds);

        api.Post(
            api.GetUserCreditUpdateApiUrl(),
            json,
            response =>
            {
                TryApplyCreditUpdateResponse(response);

                if (refreshWalletAfter)
                {
                    FetchAndApplyWallet(onSuccess, onError);
                    return;
                }

                onSuccess?.Invoke();
            },
            error =>
            {
                onError?.Invoke(error);
            }
        );
    }

    private static bool TryApplyCreditUpdateResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;

        CreditUpdateResponse parsed;
        try
        {
            parsed = JsonUtility.FromJson<CreditUpdateResponse>(response);
        }
        catch
        {
            return false;
        }

        bool ok = parsed != null && (parsed.success || parsed.status) && parsed.data != null;
        if (!ok) return false;

        int coins = Mathf.Max(0, parsed.data.coins);
        int diamonds = Mathf.Max(0, parsed.data.diamonds);

        UserSession.LoadFromPrefs();
        UserSession.Apply(
            username: UserSession.Username,
            userId: UserSession.UserId,
            avatarIndex: UserSession.AvatarIndex,
            isGuest: UserSession.IsGuest,
            coins: coins,
            diamonds: diamonds,
            jwtToken: UserSession.JwtToken,
            saveToPrefs: true
        );

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.SetCoins(coins);
            PlayerWallet.Instance.SetDiamonds(diamonds);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshSessionDebugFields();
        }

        return true;
    }

    public static void CreditUpdateCoins(int amount, Action onSuccess = null, Action<string> onError = null, bool refreshWalletAfter = true)
    {
        CreditUpdate(coinsAmount: amount, diamonds: null, onSuccess: onSuccess, onError: onError, refreshWalletAfter: refreshWalletAfter);
    }

    public static void CreditUpdateDiamonds(int diamonds, Action onSuccess = null, Action<string> onError = null, bool refreshWalletAfter = true)
    {
        CreditUpdate(coinsAmount: null, diamonds: diamonds, onSuccess: onSuccess, onError: onError, refreshWalletAfter: refreshWalletAfter);
    }

    public static void DebitUpdate(int? coinsAmount = null, int? diamonds = null, Action onSuccess = null, Action<string> onError = null, bool refreshWalletAfter = true)
    {
        if (!coinsAmount.HasValue && !diamonds.HasValue)
        {
            onError?.Invoke("Nothing to update");
            return;
        }

        UserSession.LoadFromPrefs();

        if (string.IsNullOrWhiteSpace(UserSession.UserId))
        {
            onError?.Invoke("Missing user_id");
            return;
        }

        if (string.IsNullOrWhiteSpace(UserSession.JwtToken))
        {
            onError?.Invoke("Missing token");
            return;
        }

        ApiManager api = ApiManager.Instance != null ? ApiManager.Instance : UnityEngine.Object.FindObjectOfType<ApiManager>();
        if (api == null)
        {
            onError?.Invoke("ApiManager not found");
            return;
        }

        string json = BuildCreditUpdateJson(UserSession.UserId, coinsAmount, diamonds);

        api.Post(
            api.GetUserDebitUpdateApiUrl(),
            json,
            response =>
            {
                TryApplyCreditUpdateResponse(response);

                if (refreshWalletAfter)
                {
                    FetchAndApplyWallet(onSuccess, onError);
                    return;
                }

                onSuccess?.Invoke();
            },
            error =>
            {
                onError?.Invoke(error);
            }
        );
    }

    public static void DebitUpdateCoins(int amount, Action onSuccess = null, Action<string> onError = null, bool refreshWalletAfter = true)
    {
        DebitUpdate(coinsAmount: amount, diamonds: null, onSuccess: onSuccess, onError: onError, refreshWalletAfter: refreshWalletAfter);
    }

    public static void DebitUpdateDiamonds(int diamonds, Action onSuccess = null, Action<string> onError = null, bool refreshWalletAfter = true)
    {
        DebitUpdate(coinsAmount: null, diamonds: diamonds, onSuccess: onSuccess, onError: onError, refreshWalletAfter: refreshWalletAfter);
    }

    private static string BuildCreditUpdateJson(string userId, int? coinsAmount, int? diamonds)
    {
        List<string> parts = new List<string>(3)
        {
            "\"user_id\":" + QuoteJson(userId)
        };

        if (coinsAmount.HasValue)
        {
            parts.Add("\"amount\":" + Mathf.Max(0, coinsAmount.Value));
        }

        if (diamonds.HasValue)
        {
            parts.Add("\"diamonds\":" + Mathf.Max(0, diamonds.Value));
        }

        return "{" + string.Join(",", parts) + "}";
    }

    private static string QuoteJson(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + EscapeJson(s) + "\"";
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
