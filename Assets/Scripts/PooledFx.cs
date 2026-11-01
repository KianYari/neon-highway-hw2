using UnityEngine;

// A particle effect that plays once and then goes back to the pool by itself.
public class PooledFx : MonoBehaviour
{
    public float lifetime = 2f;
    // Effects on the road have to move back with it. Sky effects stay where they are.
    public bool scrollWithWorld;

    ParticleSystem[] systems;
    float releaseAt;

    void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Play(Color color)
    {
        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            if (ps.name != "Smoke") main.startColor = color;
            ps.Clear();
            ps.Play();
        }
        releaseAt = Time.time + lifetime;
    }

    void Update()
    {
        if (scrollWithWorld)
            transform.position += Vector3.back * (GameManager.Instance.WorldSpeed * Time.deltaTime);

        if (Time.time >= releaseAt) PoolManager.Instance.Release(gameObject);
    }
}
