/*
 * Lobby Data - Lobby ni information store karva mate
 */

using UnityEngine;

namespace FancyScrollView.Example09
{
    class LobbyData
    {
        public int LobbyId { get; }
        public string LobbyName { get; }
        public Sprite LobbySprite { get; }
        public int Price { get; }
        public string ImageUrl { get; } // Agar URL thi load karvi hoy to

        public LobbyData(int lobbyId, string lobbyName, Sprite sprite, int price, string imageUrl = "")
        {
            LobbyId = lobbyId;
            LobbyName = lobbyName;
            LobbySprite = sprite;
            Price = price;
            ImageUrl = imageUrl;
        }
    }
}



