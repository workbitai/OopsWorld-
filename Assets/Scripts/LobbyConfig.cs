using UnityEngine;

[CreateAssetMenu(fileName = "LobbyConfig", menuName = "SorryPartner/Lobby Config", order = 0)]
public class LobbyConfig : ScriptableObject
{
    [Header("Identity")]
    public string lobbyId;

    [Header("Visuals")]
    public Sprite boardSprite;
    public Sprite piecesSprite;

    [Header("Values")]
    public long winningCoin;
    public long winningDiamond;
    public long entryCoin;

    [Header("Lock")]
    public bool isLocked;
}
