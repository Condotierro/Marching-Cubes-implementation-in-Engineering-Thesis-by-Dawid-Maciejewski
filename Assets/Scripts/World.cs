using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public class World : MonoBehaviour
{
    public int renderDistance = 4;
    public const int chunkSize = 16;
    public Material[] materials;

    public float scale;
    public Transform player;

    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    public TunnelPath layer1;
    public TunnelPath layer2;
    public TunnelPath layer3;
    public GameObject connectionObject;
    public GameObject[] collectibles;


    private void Awake()
    {
        foreach (GameObject g in collectibles)
        {
            Collectible c = g.GetComponent<Collectible>();
            c.initialize(player.gameObject.GetComponent<ShipController>());
        }

        UnityEngine.Random.seed = 4;
    }

    void Update()
    {
        long managedBefore = GC.GetTotalMemory(false);

        UpdateChunks();

        long managedAfter = GC.GetTotalMemory(false);
        long managedDelta = managedAfter - managedBefore;
        
        //engine allocators 
        long unityAllocated = Profiler.GetTotalAllocatedMemoryLong();
        long unityReserved = Profiler.GetTotalReservedMemoryLong();
        
        //OS-level physical RAM (process working set) 
        long processRam = Process.GetCurrentProcess().WorkingSet64;
        
        //Record metrics (BYTES) 
        RuntimeMetrics.Record("Memory.Managed.Delta.Bytes", managedDelta);
        RuntimeMetrics.Record("Memory.Managed.Total.Bytes", managedAfter);
        
        RuntimeMetrics.Record("Memory.Unity.Allocated.Bytes", unityAllocated);
        RuntimeMetrics.Record("Memory.Unity.Reserved.Bytes", unityReserved);
        
        RuntimeMetrics.Record("Memory.Process.WorkingSet.Bytes", processRam);

        RuntimeMetrics.Record("FrameTime.ms", Time.deltaTime * 1000f);
        RuntimeMetrics.Record("Render.Batches", UnityStats.batches);
    }

    void UpdateChunks()
    {
        int playerChunkX = Mathf.FloorToInt(player.position.x / Chunk.chunkSizeX);
        int playerChunkZ = Mathf.FloorToInt(player.position.z / Chunk.chunkSizeZ);

        float chunkMaxZ = (playerChunkZ + 1) * Chunk.chunkSizeZ;
        layer1.EnsureLengthUpTo(chunkMaxZ + 250);
        layer2.EnsureLengthUpTo(chunkMaxZ + 250);
        layer3.EnsureLengthUpTo(chunkMaxZ + 250);


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
                List<GameObject> toClean = kvp.Value.gameObject.GetComponent<Chunk>().registered;
                foreach (var f in toClean)
                {
                    Destroy(f);
                }
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
        GameObject chunkObj = new GameObject($"Chunk_{cx}_{cz}");
        chunkObj.transform.parent = this.transform;

        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.chunkX = cx;
        chunk.chunkZ = cz;
        chunk.scale = scale;
        chunk.materials = materials;
        chunk.layer1 = layer1;
        chunk.layer2 = layer2;
        chunk.layer3 = layer3;
        chunkObj.transform.position = new Vector3(cx * Chunk.chunkSizeX, 0, cz * Chunk.chunkSizeZ);
        chunk.connectionPrefab = connectionObject;
        chunk.collectibles = collectibles;
        chunks[new Vector2Int(cx, cz)] = chunk;
    }
}
