using System.Collections.Generic;
using UnityEngine;

// Spawns rows of cars, coin lines and fuel cans far ahead of the player, always from the pools.
// A row never blocks all three lanes, and cars that drive in the same direction as the player
// are kept to one lane at a time so the other two are always usable.
public class ObstacleSpawner : MonoBehaviour
{
    public StaticCar[] staticCars;
    public MovingCar[] movingCars;
    public Coin coin;
    public FuelCan fuelCan;

    public float spawnZ = 160f;
    public float firstRowZ = 70f;
    public float fuelEveryMeters = 280f;
    public int coinsPerLine = 5;
    public float coinSpacing = 3.5f;

    float sinceRow;
    float sinceFuel;
    float nextGap = 30f;
    readonly List<int> lanes = new List<int>(3);

    public void Prewarm()
    {
        PoolManager pool = PoolManager.Instance;
        foreach (StaticCar c in staticCars) pool.Prewarm(c.gameObject, 4);
        foreach (MovingCar c in movingCars) pool.Prewarm(c.gameObject, 3);
        pool.Prewarm(coin.gameObject, 24);
        pool.Prewarm(fuelCan.gameObject, 3);
        pool.Prewarm(staticCars[0].impactFx.gameObject, 4);
        pool.Prewarm(coin.collectFx.gameObject, 6);
        pool.Prewarm(fuelCan.collectFx.gameObject, 2);
    }

    public void ResetSpawner(bool prefill)
    {
        for (int i = Interactable.Active.Count - 1; i >= 0; i--)
            Interactable.Active[i].Despawn();

        sinceRow = 0f;
        sinceFuel = fuelEveryMeters * 0.5f;
        nextGap = RowGap();

        if (!prefill) return;

        for (float z = firstRowZ; z < spawnZ; z += nextGap)
            SpawnRow(z);
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm.State != GameState.Playing) return;

        float moved = gm.WorldSpeed * Time.deltaTime;
        sinceRow += moved;
        sinceFuel += moved;

        while (sinceRow >= nextGap)
        {
            sinceRow -= nextGap;
            SpawnRow(spawnZ - sinceRow);
        }
    }

    float RowGap()
    {
        float d = GameManager.Instance != null ? GameManager.Instance.Difficulty01 : 0f;
        return Mathf.Lerp(32f, 15f, d);
    }

    void SpawnRow(float z)
    {
        float d = GameManager.Instance.Difficulty01;
        int movingLane = FindMovingLane();
        float extraGap = 0f;

        if (sinceFuel >= fuelEveryMeters)
        {
            sinceFuel = 0f;
            SpawnFuel(z, movingLane);
        }
        else if (Random.value < 0.28f)
        {
            extraGap = SpawnCoinLine(z, movingLane);
        }
        else
        {
            SpawnObstacles(z, movingLane, d);
        }

        nextGap = RowGap() + extraGap;
    }

    void SpawnObstacles(float z, int movingLane, float difficulty)
    {
        lanes.Clear();
        for (int lane = 0; lane < GameManager.LaneCount; lane++)
            if (lane != movingLane) lanes.Add(lane);

        int maxBlocks = lanes.Count - 1;
        int count = 1;
        if (maxBlocks >= 2 && Random.value < Mathf.Lerp(0.1f, 0.55f, difficulty)) count = 2;

        float movingChance = Mathf.Lerp(0.25f, 0.5f, difficulty);

        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, lanes.Count);
            int lane = lanes[pick];
            lanes.RemoveAt(pick);

            bool canMove = movingLane < 0 && !AnyActiveNear(spawnZ - 40f);
            if (canMove && Random.value < movingChance)
            {
                SpawnMoving(lane, z);
                movingLane = lane;
            }
            else
            {
                SpawnStatic(lane, z);
            }
        }
    }

    // Returns the extra distance to wait before the next row so rows never overlap the line.
    float SpawnCoinLine(float z, int movingLane)
    {
        lanes.Clear();
        for (int lane = 0; lane < GameManager.LaneCount; lane++)
            if (lane != movingLane) lanes.Add(lane);

        int chosen = lanes[Random.Range(0, lanes.Count)];
        for (int i = 0; i < coinsPerLine; i++)
            Place(coin, chosen, z + i * coinSpacing);

        return (coinsPerLine - 1) * coinSpacing;
    }

    void SpawnFuel(float z, int movingLane)
    {
        lanes.Clear();
        for (int lane = 0; lane < GameManager.LaneCount; lane++)
            if (lane != movingLane) lanes.Add(lane);

        Place(fuelCan, lanes[Random.Range(0, lanes.Count)], z);
    }

    void SpawnStatic(int lane, float z)
    {
        Place(staticCars[Random.Range(0, staticCars.Length)], lane, z);
    }

    void SpawnMoving(int lane, float z)
    {
        Place(movingCars[Random.Range(0, movingCars.Length)], lane, z);
    }

    T Place<T>(T prefab, int lane, float z) where T : Interactable
    {
        Vector3 pos = new Vector3(GameManager.LaneToX(lane), 0f, z);
        T item = PoolManager.Instance.Get(prefab, pos, Quaternion.identity);
        item.Init(lane);
        return item;
    }

    static int FindMovingLane()
    {
        foreach (Interactable i in Interactable.Active)
        {
            Obstacle o = i as Obstacle;
            if (o != null && o.IsMoving && o.transform.position.z > 0f) return o.Lane;
        }
        return -1;
    }

    static bool AnyActiveNear(float minZ)
    {
        foreach (Interactable i in Interactable.Active)
            if (i is Obstacle && i.transform.position.z > minZ) return true;
        return false;
    }
}
