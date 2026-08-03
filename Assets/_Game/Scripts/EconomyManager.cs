using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    int coins;
    public int Coins => coins;

    public void SetCoinsAfterMultiplier(int multiplier, int extraBoards)
    {
        coins = 100 * multiplier + extraBoards;
    }
    public void SetCoinsDependOnPlace(int place, int extraBoards)
    {
        switch (place)
        {
            case 2: coins = 75 + extraBoards; break;
            case 3: coins = 50 + extraBoards; break;
            case 4: coins = 25 + extraBoards; break;
            case 5: coins = 20 + extraBoards; break;
            case 6: coins = 15 + extraBoards; break;
            case 7: coins = 10 + extraBoards; break;
            case 8: coins = 5 + extraBoards; break;
            case 9: coins = 5 + extraBoards; break;
            case 10: coins = 5 + extraBoards; break;
        }
    }
}
