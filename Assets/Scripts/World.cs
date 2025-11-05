using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int renderDistance = 4;
    public const int chunkSize = 16;
    public Material[] materials;

    public float scale;
    public Transform player;

    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private PathGenerator pathGen;


    void Start()
    {
        // Attach a path generator to this world
        pathGen = gameObject.AddComponent<PathGenerator>();
        pathGen.initialLength = 100; // Generate an initial long path
        pathGen.turnChance = 0.15f;
        pathGen.forwardBias = 0.7f;

        // Force the path to be generated immediately
        pathGen.GenerateImmediate();

        // Now it's safe to build chunks right away
        UpdateChunks();
    }


    void Update()
    {
        UpdateChunks();
    }

    void UpdateChunks()
    {
        int playerChunkX = Mathf.FloorToInt(player.position.x / Chunk.chunkSizeX);
        int playerChunkZ = Mathf.FloorToInt(player.position.z / Chunk.chunkSizeZ);

        HashSet<Vector2Int> needed = new HashSet<Vector2Int>();

        // Load chunks within render distance
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = new Vector2Int(playerChunkX + dx, playerChunkZ + dz);
                needed.Add(coord);

                if (!chunks.ContainsKey(coord))
                {
                    CreateChunk(coord.x, coord.y);
                }
            }
        }

        // Unload chunks that are no longer needed
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var kvp in chunks)
        {
            if (!needed.Contains(kvp.Key))
            {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var coord in toRemove)
        {
            chunks.Remove(coord);
        }
    }

    void CreateChunk(int cx, int cz)
    {
        // Create the chunk GameObject
        GameObject chunkObj = new GameObject($"Chunk_{cx}_{cz}");
        chunkObj.transform.parent = this.transform;

        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.chunkX = cx;
        chunk.chunkZ = cz;
        chunk.scale = scale;
        chunk.materials = materials;

        chunkObj.transform.position = new Vector3(cx * Chunk.chunkSizeX, 0, cz * Chunk.chunkSizeZ);

        // Determine chunk type based on path
        ChunkType type = GetChunkTypeForPosition(cx, cz);
        chunk.SetChunkType(type);

        if(type == ChunkType.Start && onlyPlacePlayerOnce)
        {
            onlyPlacePlayerOnce = false;
            player.position = new Vector3(cx * 32 + 16, player.position.y, cz * 32 + 16);
        }

        chunks[new Vector2Int(cx, cz)] = chunk;
    }

    bool onlyPlacePlayerOnce = true;

    ChunkType GetChunkTypeForPosition(int cx, int cz)
    {
        // Find matching path segment (rounded to chunk coordinates)
        Vector2Int pos = new Vector2Int(cx, cz);
        var path = pathGen.GetPath();

        foreach (var seg in path)
        {
            if (seg.Position == pos)
            {
                switch (seg.Type)
                {
                    case PathGenerator.TileType.Start: return ChunkType.Start;
                    case PathGenerator.TileType.End: return ChunkType.End;

                    case PathGenerator.TileType.StraightX: return ChunkType.StraightX;
                    case PathGenerator.TileType.StraightZ: return ChunkType.StraightZ;

                    case PathGenerator.TileType.TurnLeftStart: return ChunkType.TurnLeftStart;
                    case PathGenerator.TileType.TurnLeftEnd: return ChunkType.TurnLeftEnd;
                    case PathGenerator.TileType.TurnRightStart: return ChunkType.TurnRightStart;
                    case PathGenerator.TileType.TurnRightEnd: return ChunkType.TurnRightEnd;

                    case PathGenerator.TileType.RejoinFromRightStart: return ChunkType.RejoinFromRightStart;
                    case PathGenerator.TileType.RejoinFromRightEnd: return ChunkType.RejoinFromRightEnd;

                    case PathGenerator.TileType.RejoinFromLeftStart: return ChunkType.RejoinFromLeftStart;
                    case PathGenerator.TileType.RejoinFromLeftEnd: return ChunkType.RejoinFromLeftEnd;

                    case PathGenerator.TileType.RejoinFromBackStart: return ChunkType.RejoinFromBackStart;
                    case PathGenerator.TileType.RejoinFromBackEnd: return ChunkType.RejoinFromBackEnd;
                }
            }
        }

        // Default if no path tile exists here
        return ChunkType.Plain;
    }
}
