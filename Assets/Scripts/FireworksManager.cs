using UnityEngine;

// Fires random colourful bursts in the night sky ahead of the car.
public class FireworksManager : MonoBehaviour
{
    public PooledFx burstPrefab;
    public Vector2 intervalRange = new Vector2(0.6f, 2.2f);
    // Placed by angle from the camera so they always land in the part of the sky that is on screen.
    public Vector2 zRange = new Vector2(90f, 170f);
    public Vector2 heightRatio = new Vector2(0.3f, 0.5f);
    public Vector2 sideRatio = new Vector2(0.1f, 0.75f);

    float nextAt;

    public void Prewarm()
    {
        PoolManager.Instance.Prewarm(burstPrefab.gameObject, 8);
    }

    void Update()
    {
        GameState state = GameManager.Instance.State;
        if (state == GameState.Paused || Time.time < nextAt) return;

        nextAt = Time.time + Random.Range(intervalRange.x, intervalRange.y);

        float z = Random.Range(zRange.x, zRange.y);
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector3 pos = new Vector3(
            side * z * Random.Range(sideRatio.x, sideRatio.y),
            z * Random.Range(heightRatio.x, heightRatio.y),
            z);

        Color color = Random.ColorHSV(0f, 1f, 0.65f, 1f, 1f, 1f);
        PooledFx fx = PoolManager.Instance.Get(burstPrefab, pos, Quaternion.identity);
        fx.Play(color);
        AudioManager.Instance.PlayFirework();
    }
}
