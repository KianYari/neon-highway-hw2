using UnityEngine;

// Something the player picks up. Spins and bobs while waiting on the road.
public abstract class Collectible : Interactable
{
    public PooledFx collectFx;
    public Color fxColor = Color.white;
    public Transform visual;
    public float spinSpeed = 140f;
    public float bobHeight = 0.2f;

    float baseY;

    public override void Init(int lane)
    {
        base.Init(lane);
        if (visual != null) baseY = visual.localPosition.y;
    }

    protected override void Update()
    {
        base.Update();

        if (visual == null) return;
        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        Vector3 p = visual.localPosition;
        p.y = baseY + Mathf.Sin(Time.time * 4f + transform.position.z * 0.3f) * bobHeight;
        visual.localPosition = p;
    }

    public override void OnPlayerContact(PlayerCar player)
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        Collect(player);
        SpawnFx(collectFx, transform.position + Vector3.up * 0.8f, fxColor);
        Despawn();
    }

    protected abstract void Collect(PlayerCar player);
}
