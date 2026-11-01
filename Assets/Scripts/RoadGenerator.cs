using System.Collections.Generic;
using UnityEngine;

// Keeps road segments ahead of the player. Segments that pass behind the car go back to the pool.
public class RoadGenerator : MonoBehaviour
{
    public RoadSegment segmentPrefab;
    public float aheadDistance = 260f;
    public float behindDistance = 48f;

    readonly List<RoadSegment> segments = new List<RoadSegment>();

    public void ResetRoad()
    {
        PoolManager.Instance.Prewarm(segmentPrefab.gameObject, Mathf.CeilToInt((aheadDistance + behindDistance) / RoadSegment.Length) + 2);
        foreach (RoadSegment s in segments) PoolManager.Instance.Release(s.gameObject);
        segments.Clear();

        for (float z = -behindDistance; z < aheadDistance; z += RoadSegment.Length)
            Spawn(z);
    }

    void Spawn(float z)
    {
        RoadSegment s = PoolManager.Instance.Get(segmentPrefab, new Vector3(0f, 0f, z), Quaternion.identity);
        segments.Add(s);
    }

    void Update()
    {
        if (segments.Count == 0) return;

        Vector3 step = Vector3.back * (GameManager.Instance.WorldSpeed * Time.deltaTime);
        foreach (RoadSegment s in segments) s.transform.position += step;

        while (segments.Count > 0 && segments[0].transform.position.z + RoadSegment.Length < -behindDistance)
        {
            PoolManager.Instance.Release(segments[0].gameObject);
            segments.RemoveAt(0);
        }

        while (true)
        {
            RoadSegment last = segments[segments.Count - 1];
            float nextZ = last.transform.position.z + RoadSegment.Length;
            if (nextZ > aheadDistance) break;
            Spawn(nextZ);
        }
    }
}
