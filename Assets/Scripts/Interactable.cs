using System.Collections.Generic;
using UnityEngine;

// Base class for everything on the road that the player can touch.
public abstract class Interactable : MonoBehaviour
{
    public const float DespawnZ = -10f;

    public static readonly List<Interactable> Active = new List<Interactable>();

    public int Lane { get; private set; }

    public virtual void Init(int lane)
    {
        Lane = lane;
    }

    // Called by the player when the trigger hits this object.
    public abstract void OnPlayerContact(PlayerCar player);

    // How fast the object moves towards the player along -Z.
    protected virtual float ApproachSpeed(float worldSpeed) => worldSpeed;

    protected virtual void OnEnable()
    {
        Active.Add(this);
    }

    protected virtual void OnDisable()
    {
        Active.Remove(this);
    }

    protected virtual void Update()
    {
        float speed = ApproachSpeed(GameManager.Instance.WorldSpeed);
        transform.position += Vector3.back * (speed * Time.deltaTime);

        if (transform.position.z < DespawnZ) Despawn();
    }

    public virtual void Despawn()
    {
        PoolManager.Instance.Release(gameObject);
    }

    protected static void SpawnFx(PooledFx prefab, Vector3 position, Color color)
    {
        if (prefab == null) return;
        PooledFx fx = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
        fx.Play(color);
    }
}
