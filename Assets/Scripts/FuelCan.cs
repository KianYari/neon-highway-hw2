using UnityEngine;

public class FuelCan : Collectible
{
    public float fuelAmount = 35f;

    protected override void Collect(PlayerCar player)
    {
        player.AddFuel(fuelAmount);
        AudioManager.Instance.PlayFuel();
    }
}
