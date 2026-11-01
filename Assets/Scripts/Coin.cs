using UnityEngine;

public class Coin : Collectible
{
    protected override void Collect(PlayerCar player)
    {
        GameManager.Instance.AddCoin();
        AudioManager.Instance.PlayCoin();
    }
}
