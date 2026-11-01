using UnityEngine;

// Something that costs a life when the player hits it.
public abstract class Obstacle : Interactable
{
    public PooledFx impactFx;

    public abstract bool IsMoving { get; }

    public override void OnPlayerContact(PlayerCar player)
    {
        if (!player.TakeHit(this)) return;

        SpawnFx(impactFx, transform.position + Vector3.up, new Color(1f, 0.55f, 0.15f));
        Despawn();
    }
}
