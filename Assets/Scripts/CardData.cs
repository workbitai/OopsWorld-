/*
 * Card Data - Card information (power, type, etc.)
 */

[System.Serializable]
public class CardData
{
    public enum CardType
    {
        Card1,      // 1-card: Move +1
        Card2,      // 2-card: Move +2
        Card3,      // 3-card: Move +3
        Card4,      // 4-card: Move -4 backward (reverse)
        Card5,      // 5-card: Move +5
        Card7,      // 7-card: Move +7 or split
        Card8,      // 8-card: Move +8
        Card10,     // 10-card: Move +10 or -1 backward
        Card11,     // 11-card: Move +11 or swap
        Card12,     // 12-card: Move +12
        SorryCard   // SORRY! card: Attack card
    }

    public CardType cardType;
    public string power1; // First power text
    public string power2; // Second power text (if dual action)
    public bool hasDualPower; // Card has 2 powers or not
 
     public int value1;
     public int value2;

    public CardData(CardType type, string p1, string p2 = "", bool dual = false, int v1 = 0, int v2 = 0)
    {
        cardType = type;
        power1 = p1;
        power2 = p2;
        hasDualPower = dual;
        value1 = v1;
        value2 = v2;
    }
}

