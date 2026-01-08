using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public const int chunkSizeX = 16;
    public const int chunkSizeY = 64;
    public const int chunkSizeZ = 16;
    public int maxHeight = 32;
    public BlockType[,,] blocks;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    public float scale;
    public int chunkX;
    public int chunkZ;

    public Material[] materials;
    public TunnelPath layer1;
    public TunnelPath layer2;
    public TunnelPath layer3;
    public GameObject connectionPrefab;

    [SerializeField] int atlasSizeInTiles = 4; // 4x4, 8x8, etc.
    [SerializeField] float uvPadding = 0.001f; // prevents bleeding

    Vector2[][] cachedUVs;

    void Awake()
    {
        cachedUVs = new Vector2[System.Enum.GetValues(typeof(BlockType)).Length][];

        for (int i = 0; i < cachedUVs.Length; i++)
            cachedUVs[i] = BuildFaceUVs((BlockType)i);
    }

    void Start()
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.materials = materials;
        StartCoroutine(GenerateChunkCoroutine());
        
    }

    IEnumerator GenerateChunkCoroutine()
    {
        yield return null;
        GenerateBlocks();
        yield return null;
        GenerateMesh();
        yield return null;
        AddCollider();
    }

    void AddCollider()
    {
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.mesh;
    }
    public void UpdateCollider()
    {
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        meshCollider.sharedMesh = null; 
        meshCollider.sharedMesh = meshFilter.mesh; 
    }

    public void ModifyBlock(int x, int y, int z, BlockType newType)
    {
        blocks[x, y, z] = newType;
        GenerateMesh();   
        UpdateCollider(); 
    }

    public List<Vector3> validPoses = new List<Vector3>();

    void GenerateBlocks()
    {
        blocks = new BlockType[chunkSizeX, chunkSizeY, chunkSizeZ];

        float tunnelRadius = 3.5f;

        for (int x = 0; x < chunkSizeX; x++)
            for (int z = 0; z < chunkSizeZ; z++)
            {
                float wx = x + chunkX * chunkSizeX;
                float wz = z + chunkZ * chunkSizeZ;

                Vector2 worldPos = new Vector2(wx, wz);

                float distSq1 = layer1.DistanceSqToPath(worldPos);
                float distSq2 = layer2.DistanceSqToPath(worldPos);
                float distSq3 = layer3.DistanceSqToPath(worldPos);
                bool carve1 = distSq1 < (tunnelRadius * tunnelRadius * 3);
                bool carve2 = distSq2 < (tunnelRadius * tunnelRadius * 3);
                bool carve3 = distSq3 < (tunnelRadius * tunnelRadius * 3);

                for (int y = 0; y < chunkSizeY; y++)
                {
                    bool upperOverlap = carve1 && carve2;
                    bool lowerOverlap = carve2 && carve3;

                    if (upperOverlap)
                    {
                        validPoses.Add(new Vector3(wx + 0.5f, 5, wz + 0.5f));
                    }
                    else if (lowerOverlap)
                    {
                        validPoses.Add(new Vector3(wx + 0.5f, 23, wz + 0.5f));
                    }

                    if (y == 0)
                    {
                        blocks[x, y, z] = BlockType.Sand;
                        continue;
                    }

                    if (y < 22)
                    {
                        if (carve1)
                            blocks[x, y, z] = BlockType.Air;
                        else
                            blocks[x, y, z] = BlockType.DeepStone;
                        continue;
                    }
                    if (y < 44)
                    {
                        if (carve2)
                            blocks[x, y, z] = BlockType.Air;
                        else
                            blocks[x, y, z] = BlockType.Stone;
                        continue;
                    }
                    if (y >= 44)
                    {
                        if (carve3)
                            blocks[x, y, z] = BlockType.Air;
                        else
                            blocks[x, y, z] = BlockType.Dirt;
                        continue;
                    }
                }
            }

        if (validPoses.Count > 0)
        {
            // Compute average center point
            Vector3 avg = Vector3.zero;
            foreach (var p in validPoses)
                avg += p;
            avg /= validPoses.Count;

            // Spawn up to 3 objects
            int spawnCount = Mathf.Min(1, validPoses.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                //// Add small randomness (±1 block)
                //Vector3 jitter = new Vector3(
                //    Random.Range(-0.8f, 0.8f),
                //    0f,
                //    Random.Range(-0.8f, 0.8f)
                //);

                SpawnTunnelConnectionObject(avg);
            }
        }
    }


    static Stopwatch sw = new Stopwatch();

    public void GenerateMesh()
    {
        sw.Restart();   
        Mesh mesh = new Mesh();

        // shared across all block faces
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();

        // one triangle list per material
        List<int> triangles = new List<int>();

        // loop through all blocks in the chunk
        for (int x = 0; x < chunkSizeX; x++)
        {
            for (int y = 0; y < chunkSizeY; y++)
            {
                for (int z = 0; z < chunkSizeZ; z++)
                {
                    if (blocks[x, y, z] == BlockType.Air) continue;

                    Vector3 pos = new Vector3(x, y, z);

                    // generate only visible faces
                    AddCube(vertices, uvs, triangles, pos, x, y, z);
                }
            }
        }

        // build the final mesh
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.subMeshCount = materials.Length;

        // assign triangles per material index
        
        mesh.SetTriangles(triangles, 0);

        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;

        sw.Stop();
        RuntimeMetrics.Record("Chunk.GenerateMesh.ms", sw.Elapsed.TotalMilliseconds);
    }


    void AddCube(
    List<Vector3> verts,
    List<Vector2> uvs,
    List<int> tris,
    Vector3 pos,
    int x, int y, int z)
    {
        BlockType block = blocks[x, y, z];
        Vector2[] faceUVs = GetFaceUVs(block);

        void AddFace(
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            bool ccw)
        {
            int start = verts.Count;

            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            var t = ccw ? trisCCW : trisCW;
            tris.Add(start + t[0]);
            tris.Add(start + t[1]);
            tris.Add(start + t[2]);
            tris.Add(start + t[3]);
            tris.Add(start + t[4]);
            tris.Add(start + t[5]);

            uvs.Add(faceUVs[0]);
            uvs.Add(faceUVs[1]);
            uvs.Add(faceUVs[2]);
            uvs.Add(faceUVs[3]);
        }

        // TOP (CCW)
        if (y + 1 >= chunkSizeY || blocks[x, y + 1, z] == BlockType.Air)
            AddFace(
                pos + new Vector3(0, 1, 0),
                pos + new Vector3(1, 1, 0),
                pos + new Vector3(1, 1, 1),
                pos + new Vector3(0, 1, 1),
                true
            );

        // BOTTOM (CW)
        if (y - 1 < 0 || blocks[x, y - 1, z] == BlockType.Air)
            AddFace(
                pos + new Vector3(0, 0, 0),
                pos + new Vector3(1, 0, 0),
                pos + new Vector3(1, 0, 1),
                pos + new Vector3(0, 0, 1),
                false
            );

        // RIGHT (CW)
        if (x + 1 >= chunkSizeX || blocks[x + 1, y, z] == BlockType.Air)
            AddFace(
                pos + new Vector3(1, 0, 0),
                pos + new Vector3(1, 1, 0),
                pos + new Vector3(1, 1, 1),
                pos + new Vector3(1, 0, 1),
                false
            );

        // LEFT (CCW)
        if (x - 1 < 0 || blocks[x - 1, y, z] == BlockType.Air)
            AddFace(
                pos + new Vector3(0, 0, 0),
                pos + new Vector3(0, 1, 0),
                pos + new Vector3(0, 1, 1),
                pos + new Vector3(0, 0, 1),
                true
            );

        // FRONT (CCW)
        if (z + 1 >= chunkSizeZ || blocks[x, y, z + 1] == BlockType.Air)
            AddFace(
                pos + new Vector3(0, 0, 1),
                pos + new Vector3(0, 1, 1),
                pos + new Vector3(1, 1, 1),
                pos + new Vector3(1, 0, 1),
                true
            );

        // BACK (CW)
        if (z - 1 < 0 || blocks[x, y, z - 1] == BlockType.Air)
            AddFace(
                pos + new Vector3(0, 0, 0),
                pos + new Vector3(0, 1, 0),
                pos + new Vector3(1, 1, 0),
                pos + new Vector3(1, 0, 0),
                false
            );
    }




    void SpawnTunnelConnectionObject(Vector3 worldPos)
    {
        if (connectionPrefab == null) return;

        // Prevent duplicates: check a hashset if needed
        GameObject geyser = Instantiate(connectionPrefab, worldPos, Quaternion.identity);
        geyser.transform.rotation = Quaternion.Euler(new Vector3(-90,0,0));
    }

    Vector2[] GetFaceUVs(BlockType type)
    {
        int index = (int)type;

        int x = index % atlasSizeInTiles;
        int y = index / atlasSizeInTiles;

        // flip Y because Unity UVs start bottom-left
        y = atlasSizeInTiles - 1 - y;

        float tileSize = 1f / atlasSizeInTiles;

        float uMin = x * tileSize + uvPadding;
        float vMin = y * tileSize + uvPadding;
        float uMax = (x + 1) * tileSize - uvPadding;
        float vMax = (y + 1) * tileSize - uvPadding;

        return new Vector2[]
        {
        new Vector2(uMin, vMin),
        new Vector2(uMax, vMin),
        new Vector2(uMax, vMax),
        new Vector2(uMin, vMax)
        };
    }

    // Quad triangle indices (shared by all faces)
    static readonly int[] quadTris = { 0, 1, 2, 0, 2, 3 };

    // Face vertex offsets
    static readonly Vector3[] faceUp =
{
    new Vector3(0,1,1),
    new Vector3(1,1,1),
    new Vector3(1,1,0),
    new Vector3(0,1,0)
};

static readonly Vector3[] faceDown =
{
    new Vector3(0,0,0),
    new Vector3(1,0,0),
    new Vector3(1,0,1),
    new Vector3(0,0,1)
};

static readonly Vector3[] faceRight =
{
    new Vector3(1,0,1),
    new Vector3(1,1,1),
    new Vector3(1,1,0),
    new Vector3(1,0,0)
};

static readonly Vector3[] faceLeft =
{
    new Vector3(0,0,0),
    new Vector3(0,1,0),
    new Vector3(0,1,1),
    new Vector3(0,0,1)
};

static readonly Vector3[] faceFront =
{
    new Vector3(0,0,1),
    new Vector3(1,0,1),
    new Vector3(1,1,1),
    new Vector3(0,1,1)
};

static readonly Vector3[] faceBack =
{
    new Vector3(1,0,0),
    new Vector3(0,0,0),
    new Vector3(0,1,0),
    new Vector3(1,1,0)
};

    Vector2[] BuildFaceUVs(BlockType type)
    {
        int index = (int)type;

        int x = index % atlasSizeInTiles;
        int y = atlasSizeInTiles - 1 - (index / atlasSizeInTiles);

        float tileSize = 1f / atlasSizeInTiles;

        float uMin = x * tileSize + uvPadding;
        float vMin = y * tileSize + uvPadding;
        float uMax = (x + 1) * tileSize - uvPadding;
        float vMax = (y + 1) * tileSize - uvPadding;

        return new Vector2[]
        {
        new Vector2(uMin, vMin),
        new Vector2(uMax, vMin),
        new Vector2(uMax, vMax),
        new Vector2(uMin, vMax)
        };
    }

    static readonly int[] trisCW = { 0, 1, 2, 0, 2, 3 };
    static readonly int[] trisCCW = { 0, 2, 1, 0, 3, 2 };
}
