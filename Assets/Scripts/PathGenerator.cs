using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PathGenerator : MonoBehaviour
{
    public enum TileType
    {
        Start,
        StraightZ,     // Forward / Backward
        StraightX,     // Left / Right

        TurnLeftStart,
        TurnLeftEnd,
        TurnRightStart,
        TurnRightEnd,

        RejoinFromRightStart,
        RejoinFromRightEnd,
        RejoinFromLeftStart,
        RejoinFromLeftEnd,
        RejoinFromBackStart,
        RejoinFromBackEnd,

        End
    }

    public struct PathSegment
    {
        public Vector2Int Position;
        public TileType Type;

        public PathSegment(Vector2Int position, TileType type)
        {
            Position = position;
            Type = type;
        }
    }

    [Header("Path Settings")]
    public int initialLength = 20;
    [Range(0f, 1f)] public float turnChance = 0.35f;
    [Range(0f, 1f)] public float forwardBias = 0.7f;

    private List<PathSegment> path = new List<PathSegment>();
    private HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();

    // Directions: 0 = +Z, 1 = +X, 2 = -Z, 3 = -X
    private static readonly Vector2Int[] directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    private int currentDir = 0;
    private Vector2Int currentPos = Vector2Int.zero;
    private bool justTurned = false;

    //void Start() => GeneratePath(initialLength);
    public List<PathSegment> GetPath() => path;

    public void GenerateMorePath(int segments = 10)
    {
        if (path.Count > 0)
            path[path.Count - 1] = new PathSegment(path[path.Count - 1].Position, TileType.StraightZ);

        GeneratePath(segments);
    }

    private void GeneratePath(int count)
    {
        if (path.Count == 0)
        {
            path.Add(new PathSegment(currentPos, TileType.Start));
            occupiedPositions.Add(currentPos);
        }

        for (int i = 0; i < count; i++)
        {
            Vector2Int next = currentPos + directions[currentDir];

            if (occupiedPositions.Contains(next))
            {
                if (!TryTurnToAvoidCollision()) break;
                next = currentPos + directions[currentDir];
            }

            // TURN
            if (!justTurned && Random.value < turnChance)
            {
                DoTurn(next);
                continue;
            }

            // REJOIN TO +Z
            if (currentDir != 0 && Random.value < forwardBias)
            {
                DoRejoin();
                continue;
            }

            // STRAIGHT
            PlaceStraight(next);
        }

        // LAST TILE
        if (path.Count > 0)
        {
            var last = path[path.Count - 1];
            path[path.Count - 1] = new PathSegment(last.Position, TileType.End);
        }
    }

    // -------------------------------------------
    // TILE PLACEMENT LOGIC
    // -------------------------------------------

    private void PlaceStraight(Vector2Int next)
    {
        occupiedPositions.Add(next);

        TileType type = (currentDir == 0 || currentDir == 2)
            ? TileType.StraightZ
            : TileType.StraightX;

        path.Add(new PathSegment(next, type));
        currentPos = next;
        justTurned = false;
    }

    private void DoTurn(Vector2Int turnStartPos)
    {
        bool left = Random.value < 0.5f;

        // Step onto turn start tile
        occupiedPositions.Add(turnStartPos);
        path.Add(new PathSegment(turnStartPos, left ? TileType.TurnLeftStart : TileType.TurnRightStart));

        // Rotate
        Turn(left);

        // Move forward one tile
        Vector2Int turnEndPos = turnStartPos + directions[currentDir];
        if (occupiedPositions.Contains(turnEndPos)) return;

        occupiedPositions.Add(turnEndPos);
        path.Add(new PathSegment(turnEndPos, left ? TileType.TurnLeftEnd : TileType.TurnRightEnd));

        currentPos = turnEndPos;
        justTurned = true;
    }

    private void DoRejoin()
    {
        int originalDir = currentDir;

        // Rotate toward +Z
        if (currentDir == 1) Turn(true);
        else if (currentDir == 3) Turn(false);
        else if (currentDir == 2) Turn(Random.value < 0.5f);

        TileType startType, endType;
        switch (originalDir)
        {
            case 1: startType = TileType.RejoinFromRightStart; endType = TileType.RejoinFromRightEnd; break;
            case 3: startType = TileType.RejoinFromLeftStart; endType = TileType.RejoinFromLeftEnd; break;
            default: startType = TileType.RejoinFromBackStart; endType = TileType.RejoinFromBackEnd; break;
        }

        // Step 1: Turn start
        Vector2Int a = currentPos + directions[originalDir];
        if (occupiedPositions.Contains(a)) return;
        occupiedPositions.Add(a);
        path.Add(new PathSegment(a, startType));

        // Step 2: Turn end forward
        Vector2Int b = a + directions[currentDir];
        if (occupiedPositions.Contains(b)) return;
        occupiedPositions.Add(b);
        path.Add(new PathSegment(b, endType));

        currentPos = b;
        justTurned = true;
    }

    private void Turn(bool left)
    {
        currentDir = (currentDir + (left ? 3 : 1)) % 4;

        // Prevent backward direction (-Z)
        if (currentDir == 2) currentDir = 0;
    }


    private bool TryTurnToAvoidCollision()
    {
        int leftDir = (currentDir + 3) % 4;
        if (!occupiedPositions.Contains(currentPos + directions[leftDir])) { currentDir = leftDir; return true; }

        int rightDir = (currentDir + 1) % 4;
        if (!occupiedPositions.Contains(currentPos + directions[rightDir])) { currentDir = rightDir; return true; }

        return false;
    }

    public void GenerateImmediate()
    {
        GeneratePath(initialLength);
    }
}
