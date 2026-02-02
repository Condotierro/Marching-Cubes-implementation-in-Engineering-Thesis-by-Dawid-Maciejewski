using UnityEngine;
using System.Collections.Generic;

public class TunnelPath : MonoBehaviour
{
    public static TunnelPath Instance;

    public List<Vector2> mainNodes = new List<Vector2>();
    private List<Vector2> branchNodes = null;

    public float stepLength = 6f;
    public float turnChance = 0.15f;
    public float turnAngle = 35f * Mathf.Deg2Rad;
    public int maxSamplesPerChunk = 64;

    private Vector2 direction = Vector2.up;
    private Vector2 head = Vector2.zero;

    // branching control
    public float branchChance = 0.05f;
    private bool branchActive = false;
    private Vector2 branchDir;
    private int rejoinIndexOffset = 40;
    private int rejoinTargetIndex = -1;

    private float maxDeviation = 45f * Mathf.Deg2Rad;

    public float minBranchLength = 50f;
    public float minSideDistance = 25f;
    private Vector2 branchStart;

    private int lastRejoinNodeCount = 0;
    public int branchCooldownNodes = 80;

    private float branchAccumulator = 0f;
    public float branchProbabilityIncrease = 0.003f;

    // spatial binning
    private Dictionary<Vector2Int, List<Vector2>> nodeBuckets = new Dictionary<Vector2Int, List<Vector2>>();
    public int bucketSize = 64;

    void Awake()
    {
        Instance = this;
        mainNodes.Add(head);
        AddNodeToBucket(head);
    }

    public void EnsureLengthUpTo(float targetWorldZ)
    {
        while (head.y < targetWorldZ + 64f)
        {
            // accumulate branch probability
            branchAccumulator += branchProbabilityIncrease;

            if (!branchActive
                && branchAccumulator >= Random.value
                && mainNodes.Count > rejoinIndexOffset * 2
                && mainNodes.Count - lastRejoinNodeCount > branchCooldownNodes)
            {
                branchAccumulator = 0f;

                branchActive = true;
                branchNodes = new List<Vector2>();
                branchNodes.Add(mainNodes[mainNodes.Count - 1]);
                branchStart = mainNodes[mainNodes.Count - 1];
                branchDir = direction;
                rejoinTargetIndex = Mathf.Clamp(mainNodes.Count - 1 + rejoinIndexOffset, 0, mainNodes.Count - 1);
            }

            // grow main path
            direction = ApplyRandomTurnWithClamping(direction);
            head += direction * stepLength;
            mainNodes.Add(head);
            AddNodeToBucket(head); // ADD to spatial bin

            // grow branch path if active
            if (branchActive)
            {
                branchDir = ApplyBranchSteering(branchDir);
                Vector2 branchHead = branchNodes[branchNodes.Count - 1] + branchDir * stepLength;
                branchNodes.Add(branchHead);
                AddNodeToBucket(branchHead); // ADD branch to spatial bin too

                // target always moves ahead along the main path
                rejoinTargetIndex = mainNodes.Count - 1 + rejoinIndexOffset;
                rejoinTargetIndex = Mathf.Clamp(rejoinTargetIndex, 0, mainNodes.Count - 1);

                Vector2 rejoinTarget = mainNodes[rejoinTargetIndex];

                if ((branchHead - rejoinTarget).sqrMagnitude < 6f)
                {
                    mainNodes.AddRange(branchNodes);
                    foreach (var n in branchNodes) AddNodeToBucket(n); // ensure merged branch nodes are also binned

                    branchNodes = null;
                    branchActive = false;

                    lastRejoinNodeCount = mainNodes.Count;
                }
            }
        }
    }

    private Vector2 ApplyRandomTurnWithClamping(Vector2 dir)
    {
        if (Random.value < turnChance)
        {
            float sign = Random.value < 0.5f ? -1 : 1;
            float angle = turnAngle * sign;

            Vector2 newDir = new Vector2(
                dir.x * Mathf.Cos(angle) - dir.y * Mathf.Sin(angle),
                dir.x * Mathf.Sin(angle) + dir.y * Mathf.Cos(angle)
            ).normalized;

            float globalAngle = Mathf.Atan2(newDir.x, newDir.y);
            globalAngle = Mathf.Clamp(globalAngle, -maxDeviation, maxDeviation);

            return new Vector2(Mathf.Sin(globalAngle), Mathf.Cos(globalAngle));
        }
        return dir;
    }

    private Vector2 ApplyBranchSteering(Vector2 dir)
    {
        Vector2 current = branchNodes[branchNodes.Count - 1];

        float branchTravel = (current - branchStart).magnitude;
        Vector2 rejoinPoint = mainNodes[Mathf.Clamp(rejoinTargetIndex, 0, mainNodes.Count - 1)];
        float sideDist = (current - rejoinPoint).magnitude;

        bool inDivergencePhase = sideDist < minSideDistance;

        if (inDivergencePhase)
        {
            // push away from main path
            Vector2 away = (current - rejoinPoint).normalized;
            dir = (dir * 0.7f + away * 0.3f).normalized;
        }
        else if (branchTravel > minBranchLength)
        {
            // curve back toward main
            Vector2 forward = Vector2.up;
            Vector2 toTarget = (rejoinPoint - current).normalized;
            dir = (forward * 0.65f + toTarget * 0.35f).normalized;
        }
        else
        {
            if (Random.value < 0.05f)
                dir = ApplyRandomTurnWithClamping(dir);
        }

        return dir.normalized;
    }

    public float DistanceSqToPath(Vector2 point)
    {
        Vector2Int baseKey = new Vector2Int(
            Mathf.FloorToInt(point.x / bucketSize),
            Mathf.FloorToInt(point.y / bucketSize)
        );

        float best = float.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int key = new Vector2Int(baseKey.x + dx, baseKey.y + dy);
                if (!nodeBuckets.TryGetValue(key, out var list)) continue;

                for (int i = 1; i < list.Count; i++)
                {
                    Vector2 a = list[i - 1];
                    Vector2 b = list[i];
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
                    Vector2 closest = a + t * ab;
                    float d = (point - closest).sqrMagnitude;
                    if (d < best) best = d;
                }
            }
        }
        return best;
    }

    private void AddNodeToBucket(Vector2 pos)
    {
        Vector2Int key = new Vector2Int(
            Mathf.FloorToInt(pos.x / bucketSize),
            Mathf.FloorToInt(pos.y / bucketSize)
        );

        if (!nodeBuckets.TryGetValue(key, out var list))
        {
            list = new List<Vector2>();
            nodeBuckets[key] = list;
        }
        list.Add(pos);
    }
}
