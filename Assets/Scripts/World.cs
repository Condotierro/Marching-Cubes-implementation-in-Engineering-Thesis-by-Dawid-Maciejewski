using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int renderDistance = 5;
    public const int chunkSize = 16;
    public Material[] materials;

    public float scale = 0.1f;
    public Transform player;

    private Dictionary<Vector2Int, Chunk> chunks = new();
    private HashSet<Vector2Int> tunnelChunks = new();

    [Header("Tunnel Path")]
    public int tunnelLength = 100;
    public float turnChance = 0.2f;

    void Start()
    {
        GenerateTunnelPath();
        UpdateChunks();
    }

    void Update()
    {
        UpdateChunks();
    }

    void GenerateTunnelPath()
    {
        tunnelChunks.Clear();

        Vector2Int current = Vector2Int.zero;
        Vector2Int direction = Vector2Int.up; // +Z

        for (int i = 0; i < tunnelLength; i++)
        {
            tunnelChunks.Add(current);

            // Occasionally turn left/right (but still heading mostly +Z)
            if (Random.value < turnChance)
            {
                int turn = Random.value < 0.5f ? -1 : 1;
                direction = new Vector2Int(turn, 1); // diagonal step left/right-forward
            }
            else
            {
                direction = Vector2Int.up;
            }

            current += direction;
        }
    }

    void UpdateChunks()
    {
        int playerChunkX = Mathf.FloorToInt(player.position.x / Chunk.chunkSizeX);
        int playerChunkZ = Mathf.FloorToInt(player.position.z / Chunk.chunkSizeZ);

        HashSet<Vector2Int> needed = new();

        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = new Vector2Int(playerChunkX + dx, playerChunkZ + dz);
                needed.Add(coord);

                if (!chunks.ContainsKey(coord))
                    CreateChunk(coord);
            }
        }

        List<Vector2Int> toRemove = new();
        foreach (var kvp in chunks)
        {
            if (!needed.Contains(kvp.Key))
            {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var coord in toRemove)
            chunks.Remove(coord);
    }

    void CreateChunk(Vector2Int coord)
    {
        GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunkObj.transform.parent = this.transform;

        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.chunkX = coord.x;
        chunk.chunkZ = coord.y;
        chunk.scale = scale;
        chunk.materials = materials;
        chunk.isTunnel = tunnelChunks.Contains(coord);

        chunkObj.transform.position = new Vector3(coord.x * Chunk.chunkSizeX, 0, coord.y * Chunk.chunkSizeZ);
        chunks[coord] = chunk;
    }
}
