using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfileUIBinder : MonoBehaviour
{
    [SerializeField] private List<TMP_Text> nameTexts = new List<TMP_Text>();
    [SerializeField] private List<Image> avatarImages = new List<Image>();
    [SerializeField] private string nameFormat = "{0}";
    [SerializeField] private Sprite defaultOfflineAvatarSprite = null;
    [SerializeField] private bool useDefaultAvatarWhenOffline = true;

    private GameManager gm;
    private Coroutine ensureGmCoroutine;
    private Coroutine internetWatchCoroutine;
    private NetworkReachability lastReachability;

    private void OnEnable()
    {
        if (ensureGmCoroutine != null)
        {
            StopCoroutine(ensureGmCoroutine);
            ensureGmCoroutine = null;
        }

        ensureGmCoroutine = StartCoroutine(EnsureGameManagerAndSubscribe());

        if (internetWatchCoroutine != null)
        {
            StopCoroutine(internetWatchCoroutine);
            internetWatchCoroutine = null;
        }

        lastReachability = Application.internetReachability;
        internetWatchCoroutine = StartCoroutine(WatchInternetReachability());
    }

    private void OnDisable()
    {
        if (ensureGmCoroutine != null)
        {
            StopCoroutine(ensureGmCoroutine);
            ensureGmCoroutine = null;
        }

        if (internetWatchCoroutine != null)
        {
            StopCoroutine(internetWatchCoroutine);
            internetWatchCoroutine = null;
        }

        if (gm != null)
        {
            gm.ProfileChanged -= Refresh;
        }
    }

    private IEnumerator WatchInternetReachability()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(0.5f);

            NetworkReachability now = Application.internetReachability;
            if (now == lastReachability) continue;

            lastReachability = now;
            Refresh();
        }
    }

    private IEnumerator EnsureGameManagerAndSubscribe()
    {
        // Wait until GameManager exists so we don't miss the initial ProfileChanged event on startup.
        while (gm == null)
        {
            gm = GameManager.Instance != null ? GameManager.Instance : FindObjectOfType<GameManager>();
            if (gm != null) break;
            yield return null;
        }

        if (gm != null)
        {
            gm.ProfileChanged -= Refresh;
            gm.ProfileChanged += Refresh;
        }

        Refresh();
        ensureGmCoroutine = null;
    }

    private void Refresh()
    {
        if (gm == null)
        {
            gm = GameManager.Instance != null ? GameManager.Instance : FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.ProfileChanged -= Refresh;
                gm.ProfileChanged += Refresh;
            }
        }

        string n = gm != null ? gm.PlayerName : string.Empty;
        if (string.IsNullOrEmpty(n)) n = "Player";

        string formatted;
        try
        {
            formatted = string.Format(nameFormat, n);
        }
        catch
        {
            formatted = n;
        }

        if (nameTexts != null)
        {
            for (int i = 0; i < nameTexts.Count; i++)
            {
                TMP_Text t = nameTexts[i];
                if (t == null) continue;
                t.text = formatted;
            }
        }

        Sprite s = null;
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
        if (offline && useDefaultAvatarWhenOffline && defaultOfflineAvatarSprite != null)
        {
            s = defaultOfflineAvatarSprite;
        }
        else
        {
            s = gm != null ? gm.PlayerAvatarSprite : null;
        }

        if (s != null && avatarImages != null)
        {
            for (int i = 0; i < avatarImages.Count; i++)
            {
                Image img = avatarImages[i];
                if (img == null) continue;
                img.sprite = s;
            }
        }
    }
}
