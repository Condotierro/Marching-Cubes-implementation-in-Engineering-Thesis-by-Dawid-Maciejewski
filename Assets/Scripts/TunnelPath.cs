using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton managing a continuous, forward-growing tunnel path.
/// Supports turning and eventual branching.
/// </summary>
public class TunnelPath : MonoBehaviour
{
    public static TunnelPath Instance;

    public List<Vector2> nodes = new List<Vector2>();

    public float stepLength = 6f;
    public float turnChance = 0.15f;
    public float turnAngle = 35f * Mathf.Deg2Rad;
    public int maxSamplesPerChunk = 64;

    private Vector2 direction = Vector2.up; // initial forward direction
    private Vector2 head = Vector2.zero;

    // max angle from global forward (Z axis)
    private float maxDeviation = 45f * Mathf.Deg2Rad;

    void Awake()
    {
        Instance = this;
        nodes.Add(head);
    }

    /// <summary>
    /// Grows the tunnel path ahead of the player, clamping turns to ±45° from Z-axis.
    /// </summary>
    public void EnsureLengthUpTo(float targetWorldZ)
    {
        while (head.y < targetWorldZ + 64f)
        {
            if (Random.value < turnChance)
            {
                float sign = Random.value < 0.5f ? -1 : 1;
                float angle = turnAngle * sign;

                Vector2 newDir = new Vector2(
                    direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
                    direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle)
                ).normalized;

                float globalAngle = Mathf.Atan2(newDir.x, newDir.y);
                globalAngle = Mathf.Clamp(globalAngle, -maxDeviation, maxDeviation);

                direction = new Vector2(Mathf.Sin(globalAngle), Mathf.Cos(globalAngle));
            }

            head += direction * stepLength;
            nodes.Add(head);
        }
    }

    /// <summary>
    /// Returns squared distance from a world point to the tunnel polyline.
    /// </summary>
    public float DistanceSqToPath(Vector2 point)
    {
        float best = float.MaxValue;

        for (int i = 1; i < nodes.Count; i++)
        {
            Vector2 a = nodes[i - 1];
            Vector2 b = nodes[i];

            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            Vector2 closest = a + t * ab;

            float d = (point - closest).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>
    /// Returns a list of evenly spaced samples along the path that intersect the given chunk bounds.
    /// Ensures chunks can carve tunnels without gaps.
    /// </summary>
    public List<Vector2> GetSamplesForChunk(int chunkX, int chunkZ, int chunkSizeX, int chunkSizeZ)
    {
        Vector2 chunkMin = new Vector2(chunkX * chunkSizeX, chunkZ * chunkSizeZ);
        Vector2 chunkMax = chunkMin + new Vector2(chunkSizeX, chunkSizeZ);

        List<Vector2> samples = new List<Vector2>();

        float chunkDiag = new Vector2(chunkSizeX, chunkSizeZ).magnitude;
        float step = chunkDiag / maxSamplesPerChunk;

        for (int i = 1; i < nodes.Count; i++)
        {
            Vector2 a = nodes[i - 1];
            Vector2 b = nodes[i];
            Vector2 segment = b - a;
            float segmentLength = segment.magnitude;
            int numSteps = Mathf.CeilToInt(segmentLength / step);

            for (int s = 0; s <= numSteps; s++)
            {
                Vector2 p = a + segment * (s / (float)numSteps);
                if (p.x >= chunkMin.x && p.x < chunkMax.x &&
                    p.y >= chunkMin.y && p.y < chunkMax.y)
                {
                    samples.Add(p);
                }
            }
        }

        return samples;
    }

    /// <summary>
    /// Estimated number of samples for a chunk (used in Chunk.GenerateBlocks).
    /// </summary>
    public int EstimatedSampleCount(int chunkX, int chunkZ, int chunkSizeX, int chunkSizeZ)
    {
        return GetSamplesForChunk(chunkX, chunkZ, chunkSizeX, chunkSizeZ).Count;
    }

    /// <summary>
    /// Returns the i-th sample for a chunk (0 <= i < EstimatedSampleCount).
    /// </summary>
    public Vector2 SampleAtChunk(int chunkX, int chunkZ, int chunkSizeX, int chunkSizeZ, int i)
    {
        var samples = GetSamplesForChunk(chunkX, chunkZ, chunkSizeX, chunkSizeZ);
        if (samples.Count == 0) return new Vector2(chunkX * chunkSizeX + chunkSizeX/2f, chunkZ * chunkSizeZ + chunkSizeZ/2f);
        return samples[Mathf.Clamp(i, 0, samples.Count - 1)];
    }
}
