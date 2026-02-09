/*
 * Lobby Data Entry - Inspector ma manually lobby data add karva mate
 * Serializable class jo Inspector ma list ma dikhse
 */

using UnityEngine;

namespace FancyScrollView.Example09
{
    [System.Serializable]
    public class LobbyDataEntry
    {
        [Header("Lobby Information")]
        [Tooltip("Lobby nu name")]
        public string lobbyName = "Lobby";

        [Header("Lobby Sprite")]
        [Tooltip("Lobby ni sprite/image")]
        public Sprite lobbySprite;

        [Header("Lobby Price")]
        [Tooltip("Lobby ni price (₹)")]
        public int price = 1000;

        [Header("Optional - Image URL")]
        [Tooltip("Agar sprite nahi hoy to URL thi load karvi (optional)")]
        public string imageUrl = "";

        // Constructor
        public LobbyDataEntry(string name, Sprite sprite, int lobbyPrice, string url = "")
        {
            lobbyName = name;
            lobbySprite = sprite;
            price = lobbyPrice;
            imageUrl = url;
        }
    }
}



