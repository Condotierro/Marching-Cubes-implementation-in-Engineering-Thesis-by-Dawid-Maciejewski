using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public const int chunkSizeX = 16;
    public const int chunkSizeY = 128;
    public const int chunkSizeZ = 16;

    public int maxHeight = 40;
    public BlockType[,,] blocks;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    public float scale = 0.1f;
    public int chunkX;
    public int chunkZ;

    public Material[] materials;
    public bool isTunnel = false;
    public ChunkType chunkType = ChunkType.Default;

    void Start()
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (materials != null && materials.Length > 0)
            meshRenderer.materials = materials;

        GenerateBlocks();

        // carve tunnel if flagged (separate from base generation)
        if (isTunnel)
            CarveTunnel();

        GenerateMesh();
        AddCollider();
    }

    // -----------------------------
    // Basic block array creation
    // -----------------------------
    void GenerateBlocks()
    {
        blocks = new BlockType[chunkSizeX, chunkSizeY, chunkSizeZ];

        // Super-flat world baseline
        for (int x = 0; x < chunkSizeX; x++)
        {
            for (int z = 0; z < chunkSizeZ; z++)
            {
                int height = maxHeight;

                for (int y = 0; y < chunkSizeY; y++)
                {
                    if (y < height - 5)
                        blocks[x, y, z] = BlockType.Stone;
                    else if (y < height)
                        blocks[x, y, z] = BlockType.Dirt;
                    else if (y == height)
                        blocks[x, y, z] = BlockType.Grass;
                    else
                        blocks[x, y, z] = BlockType.Air;
                }
            }
        }
    }

    // -----------------------------
    // Carving logic (independent)
    // -----------------------------
    // This carves a U-shaped tunnel through the chunk. It uses chunkType if available to
    // vary the orientation. The carving clears entire vertical slices so tunnels are contiguous.
    void CarveTunnel()
    {
        // Parameters - tweak to taste
        float tunnelHalfWidth = 5.0f;    // half width of U in world units (x direction)
        float tunnelHalfHeight = 4.0f;   // half vertical clearance
        float tunnelDepthBias = 8.0f;    // how much lower the U valley is from top
        float noiseScale = scale * 0.2f;
        float noiseMultiplier = 1.8f;

        // world offset for Perlin -> helps continuity across chunks
        float wxBase = chunkX * chunkSizeX;
        float wzBase = chunkZ * chunkSizeZ;

        // Carve by evaluating a smooth 2D distance-shaped mask in local chunk coords.
        // For forward (main +Z) tunnels we make the U orient across X (so corridor runs Z).
        for (int lx = 0; lx < chunkSizeX; lx++)
        {
            for (int lz = 0; lz < chunkSizeZ; lz++)
            {
                // local coordinates centered
                float cx = lx - (chunkSizeX - 1) / 2f;
                float cz = lz - (chunkSizeZ - 1) / 2f;

                // compute a "carve value" in [0..1], where 1 = deep carve (air), 0 = keep solid
                float carve = 0f;

                switch (chunkType)
                {
                    case ChunkType.TunnelForward:
                    case ChunkType.Default:
                        // U-shape across X: low in center X, extends along Z
                        // use squared parabola for smooth U
                        {
                            float xNorm = cx / tunnelHalfWidth;           // -1 .. 1 across tunnel width
                            float curve = Mathf.Clamp01(1f - xNorm * xNorm); // 1 at center, 0 at edges
                            // widen towards center along Z slightly (so mid chunk is more open)
                            float zFade = 1f - Mathf.Abs(cz) / (chunkSizeZ * 0.5f);
                            zFade = Mathf.Clamp01(zFade);
                            carve = curve * zFade;
                        }
                        break;

                    case ChunkType.TunnelBeginLeft:
                        // begin turning left: open more toward -X in front half of chunk (positive cz)
                        {
                            float frontT = Mathf.Clamp01((cz + chunkSizeZ / 2f) / chunkSizeZ); // 0 at back, 1 at front
                            float leftBias = Mathf.Clamp01(1f - (cx / (tunnelHalfWidth * 1.4f))); // 1 when cx negative (left)
                            float xCurve = Mathf.Clamp01(1f - (cx * cx) / (tunnelHalfWidth * tunnelHalfWidth));
                            carve = Mathf.Lerp(xCurve * 0.6f, leftBias * 1.0f, frontT);
                        }
                        break;

                    case ChunkType.TunnelBeginRight:
                        // begin turning right: open more toward +X in front half
                        {
                            float frontT = Mathf.Clamp01((cz + chunkSizeZ / 2f) / chunkSizeZ);
                            float rightBias = Mathf.Clamp01(1f - ((-cx) / (tunnelHalfWidth * 1.4f))); // 1 when cx positive (right)
                            float xCurve = Mathf.Clamp01(1f - (cx * cx) / (tunnelHalfWidth * tunnelHalfWidth));
                            carve = Mathf.Lerp(xCurve * 0.6f, rightBias * 1.0f, frontT);
                        }
                        break;

                    case ChunkType.TunnelDiagonalLeft:
                        // diagonal left: carve along the diagonal direction (X negative as Z increases)
                        {
                            // distance from the diagonal center line (dx + dz)
                            float d = (cx + cz) * 0.70710678f; // 1/sqrt(2) normalize
                            float dnorm = d / (tunnelHalfWidth * 1.1f);
                            carve = Mathf.Clamp01(1f - dnorm * dnorm);
                        }
                        break;

                    case ChunkType.TunnelDiagonalRight:
                        {
                            float d = (cx - cz) * 0.70710678f;
                            float dnorm = d / (tunnelHalfWidth * 1.1f);
                            carve = Mathf.Clamp01(1f - dnorm * dnorm);
                        }
                        break;

                    case ChunkType.TunnelDiagonalAssistLeftBottom:
                    case ChunkType.TunnelDiagonalAssistLeftTop:
                    case ChunkType.TunnelDiagonalAssistRightBottom:
                    case ChunkType.TunnelDiagonalAssistRightTop:
                        // simple assist: blend forward and diagonal using cz
                        {
                            float blend = Mathf.Clamp01((cz + chunkSizeZ / 2f) / (float)chunkSizeZ);
                            float forwardCurve = Mathf.Clamp01(1f - (cx * cx) / (tunnelHalfWidth * tunnelHalfWidth));
                            float diagCurve = Mathf.Clamp01(1f - ((cx + (chunkType.ToString().Contains("Left") ? cz : -cz)) * (cx + (chunkType.ToString().Contains("Left") ? cz : -cz)) / (tunnelHalfWidth * tunnelHalfWidth)));
                            carve = Mathf.Lerp(forwardCurve, diagCurve, blend);
                        }
                        break;

                    default:
                        // fallback: small forward carve
                        {
                            float xNorm = cx / tunnelHalfWidth;
                            carve = Mathf.Clamp01(1f - xNorm * xNorm) * 0.6f;
                        }
                        break;
                }

                // add a touch of Perlin noise so edges aren't perfect
                float n = Mathf.PerlinNoise((wxBase + lx) * noiseScale, (wzBase + lz) * noiseScale) * noiseMultiplier;
                carve = Mathf.Clamp01(carve + (n * 0.15f)); // noise is subtle

                // decide carved vertical extent based on carve amount
                // carve=1 -> big vertical clearance, carve=0 -> keep
                float centerY = maxHeight - tunnelDepthBias; // central carve baseline
                float verticalRadius = Mathf.Lerp(2f, tunnelHalfHeight, carve); // how much vertical open
                int floorY = Mathf.Clamp(Mathf.FloorToInt(centerY - verticalRadius), 0, chunkSizeY - 1);
                int ceilY = Mathf.Clamp(Mathf.CeilToInt(centerY + verticalRadius), 0, chunkSizeY - 1);

                // CLEAR (set to Air) full column between floorY and ceilY
                for (int yy = floorY; yy <= ceilY; yy++)
                {
                    blocks[lx, yy, lz] = BlockType.Air;
                }
            }
        }
    }

    // -----------------------------
    // Mesh + collider
    // -----------------------------
    void AddCollider()
    {
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.mesh;
    }

    public void UpdateCollider()
    {
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        // must clear first to force update
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = meshFilter.mesh;
    }

    public void ModifyBlock(int x, int y, int z, BlockType newType)
    {
        if (x < 0 || y < 0 || z < 0 || x >= chunkSizeX || y >= chunkSizeY || z >= chunkSizeZ) return;
        blocks[x, y, z] = newType;
        GenerateMesh();
        UpdateCollider();
    }

    public void GenerateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // safe for many verts

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var submeshTris = new List<List<int>>();

        int matCount = (materials != null && materials.Length > 0) ? materials.Length : 1;
        for (int i = 0; i < matCount; i++)
            submeshTris.Add(new List<int>());

        // iterate blocks and produce only visible faces
        for (int x = 0; x < chunkSizeX; x++)
        {
            for (int y = 0; y < chunkSizeY; y++)
            {
                for (int z = 0; z < chunkSizeZ; z++)
                {
                    if (blocks[x, y, z] == BlockType.Air) continue;
                    Vector3 pos = new Vector3(x, y, z);
                    AddCube(vertices, uvs, submeshTris, pos, x, y, z);
                }
            }
        }

        mesh.vertices = vertices.ToArray();

        // combine all uv lists (we used same uvs list)
        mesh.uv = uvs.ToArray();

        mesh.subMeshCount = submeshTris.Count;
        for (int i = 0; i < submeshTris.Count; i++)
            mesh.SetTriangles(submeshTris[i].ToArray(), i);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }

    // AddCube: adds visible faces for a single block at (x,y,z). Uses submesh lists by material index.
    void AddCube(List<Vector3> verts, List<Vector2> uvs, List<List<int>> submeshTris, Vector3 pos, int x, int y, int z)
    {
        BlockType block = blocks[x, y, z];
        int matIndex = GetMaterialIndex(block);
        List<int> targetTris = submeshTris[Mathf.Clamp(matIndex, 0, submeshTris.Count - 1)];

        // local helper to check neighbor (treat outside as air)
        bool IsAir(int nx, int ny, int nz)
        {
            if (nx < 0 || ny < 0 || nz < 0 || nx >= chunkSizeX || ny >= chunkSizeY || nz >= chunkSizeZ) return true;
            return blocks[nx, ny, nz] == BlockType.Air;
        }

        // For each face: if neighbor is air -> add face (4 verts, 2 tris), add uvs
        int start;

        // TOP
        if (IsAir(x, y + 1, z))
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,1,0),
                pos + new Vector3(1,1,0),
                pos + new Vector3(1,1,1),
                pos + new Vector3(0,1,1)
            };
            start = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            uvs.AddRange(new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        }

        // BOTTOM
        if (IsAir(x, y - 1, z))
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,0),
                pos + new Vector3(1,0,0),
                pos + new Vector3(1,0,1),
                pos + new Vector3(0,0,1)
            };
            start = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            uvs.AddRange(new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        }

        // RIGHT (+X)
        if (IsAir(x + 1, y, z))
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(1,0,0),
                pos + new Vector3(1,1,0),
                pos + new Vector3(1,1,1),
                pos + new Vector3(1,0,1)
            };
            start = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            uvs.AddRange(new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        }

        // LEFT (-X)
        if (IsAir(x - 1, y, z))
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,0),
                pos + new Vector3(0,1,0),
                pos + new Vector3(0,1,1),
                pos + new Vector3(0,0,1)
            };
            start = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            uvs.AddRange(new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        }

        // FRONT (+Z)
        if (IsAir(x, y, z + 1))
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,1),
                pos + new Vector3(0,1,1),
                pos + new Vector3(1,1,1),
                pos + new Vector3(1,0,1)
            };
            start = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            uvs.AddRange(new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        }

        // BACK (-Z)
        if (IsAir(x, y, z - 1))
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,0),
                pos + new Vector3(0,1,0),
                pos + new Vector3(1,1,0),
                pos + new Vector3(1,0,0)
            };
            start = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            uvs.AddRange(new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        }
    }

    // map BlockType -> material index (adjust to match materials array)
    int GetMaterialIndex(BlockType type)
    {
        switch (type)
        {
            case BlockType.Grass: return 0;
            case BlockType.Dirt: return 1;
            case BlockType.Stone: return 2;
            default: return 0;
        }
    }
}
