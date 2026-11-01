using UnityEngine;

// A car driving in the same direction as the player, only slower.
public class MovingCar : Obstacle
{
    public float forwardSpeed = 10f;

    public override bool IsMoving => true;

    protected override float ApproachSpeed(float worldSpeed)
    {
        return worldSpeed - forwardSpeed;
    }
}
