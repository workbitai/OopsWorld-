/*
 * Screen Manager - Common screen management system
 * Ek screen on thy to biji badhi automatically bandh thy jay
 */

using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace FancyScrollView.Example09
{
    public class ScreenManager : MonoBehaviour
    {
        [Header("Screens List")]
        [Tooltip("Ahi sab screens add karo. Ek screen on thy to biji badhi bandh thy jase")]
        [SerializeField] List<GameObject> screens = new List<GameObject>();

        [Header("Default Screen (Optional)")]
        [Tooltip("Game start thay to ye screen automatically on thase")]
        [SerializeField] GameObject defaultScreen = null;

        [Header("Current Active Screen")]
        [SerializeField, Tooltip("Currently active screen (read-only)")]
        GameObject currentActiveScreen = null;

        public GameObject CurrentActiveScreen => currentActiveScreen;

        private bool isTransitioning = false;

        private bool RequiresInternetForScreen(GameObject screen)
        {
            if (screen == null) return false;
            string n = screen.name;
            if (string.IsNullOrEmpty(n)) return false;
            n = n.ToLowerInvariant();
            return n.Contains("ranking") || n.Contains("cosmetic") || n.Contains("dailytask") || (n.Contains("daily") && n.Contains("task")) || n.Contains("noads") || (n.Contains("no") && n.Contains("ads")) || n.Contains("shop") || n.Contains("store") || n.Contains("purchase") || n.Contains("iap") || n.Contains("inapp") || n.Contains("coin") || n.Contains("coins") || n.Contains("diamond") || n.Contains("diamonds");
        }

        void Start()
        {
            // Sab screens initially bandh karo
            CloseAllScreens();

            // Default screen on karo (agar assign kari hoy to)
            if (defaultScreen != null)
            {
                OpenScreen(defaultScreen);
            }
        }

        /// <summary>
        /// Specific screen open karo, biji badhi bandh karo
        /// </summary>
        public void OpenScreen(GameObject screen)
        {
            if (screen == null)
            {
                Debug.LogWarning("ScreenManager: Screen is null!");
                return;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable && RequiresInternetForScreen(screen))
            {
                global::NoInternetStrip.BlockIfOffline();
                return;
            }

            if (isTransitioning)
            {
                return;
            }

            if (currentActiveScreen == null)
            {
                CloseAllScreens();
                ActivateScreen(screen);
                return;
            }

            if (currentActiveScreen == screen)
            {
                return;
            }

            isTransitioning = true;
            GameObject previous = currentActiveScreen;

            var previousAnimator = previous != null ? previous.GetComponentInChildren<MenuItemsAnimator>(true) : null;
            if (previousAnimator != null)
            {
                previousAnimator.Close(() =>
                {
                    previous.SetActive(false);
                    CloseAllScreens();
                    ActivateScreen(screen);
                    isTransitioning = false;
                });
                return;
            }

            CloseAllScreens();
            ActivateScreen(screen);
            isTransitioning = false;
        }

        private void ActivateScreen(GameObject screen)
        {
            if (screen == null) return;

            screen.SetActive(true);
            currentActiveScreen = screen;

            if (screen.name == "BonusPanel")
            {
                if (screen.GetComponent<BonusPanelJellyAnimator>() == null)
                {
                    screen.AddComponent<BonusPanelJellyAnimator>();
                }
            }

            var animator = screen.GetComponentInChildren<MenuItemsAnimator>(true);
            if (animator != null)
            {
                animator.Play();
            }

            Debug.Log($"ScreenManager: Opened screen - {screen.name}");

            if (screen.name == "GamePlayScreen" || screen.name.Contains("GamePlay"))
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    gm.OnGameplayScreenOpened();
                }
            }

            if (screen.name.Contains("PlayerFinding"))
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    gm.OnPlayerFindingScreenOpened();
                }
            }

            if (screen.name == "LobbyPanel" || screen.name.Contains("Lobby") || screen.name.Contains("Home"))
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    gm.OnHomeScreenOpened();
                }
            }
        }

        /// <summary>
        /// Screen name thi open karo
        /// </summary>
        public void OpenScreenByName(string screenName)
        {
            GameObject screen = screens.Find(s => s != null && s.name == screenName);
            if (screen != null)
            {
                OpenScreen(screen);
            }
            else
            {
                Debug.LogWarning($"ScreenManager: Screen '{screenName}' not found in list!");
            }
        }

        /// <summary>
        /// Index thi screen open karo
        /// </summary>
        public void OpenScreenByIndex(int index)
        {
            if (index >= 0 && index < screens.Count && screens[index] != null)
            {
                OpenScreen(screens[index]);
            }
            else
            {
                Debug.LogWarning($"ScreenManager: Invalid screen index {index}!");
            }
        }

        /// <summary>
        /// Sab screens bandh karo
        /// </summary>
        public void CloseAllScreens()
        {
            foreach (var screen in screens)
            {
                if (screen != null)
                {
                    screen.SetActive(false);
                }
            }
            currentActiveScreen = null;
        }

        /// <summary>
        /// Specific screen bandh karo
        /// </summary>
        public void CloseScreen(GameObject screen)
        {
            if (screen != null)
            {
                screen.SetActive(false);
                if (currentActiveScreen == screen)
                {
                    currentActiveScreen = null;
                }
            }
        }

        /// <summary>
        /// Currently active screen bandh karo
        /// </summary>
        public void CloseCurrentScreen()
        {
            if (currentActiveScreen != null)
            {
                currentActiveScreen.SetActive(false);
                currentActiveScreen = null;
            }
        }

        /// <summary>
        /// Currently active screen return karo
        /// </summary>
        public GameObject GetCurrentActiveScreen()
        {
            return currentActiveScreen;
        }

        /// <summary>
        /// Screen active che ke nahi check karo
        /// </summary>
        public bool IsScreenActive(GameObject screen)
        {
            return screen != null && screen.activeSelf;
        }
    }
}



