using System.Collections;
using System.Collections.Generic;
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

                float distSq = TunnelPath.Instance.DistanceSqToPath(worldPos);

                bool carve = distSq < (tunnelRadius * tunnelRadius * 3);

                for (int y = 0; y < chunkSizeY; y++)
                {
                    if (carve)
                        blocks[x, y, z] = BlockType.Air;
                    else
                        blocks[x, y, z] = BlockType.Stone;
                }
            }
    }



    public void GenerateMesh()
    {
        Mesh mesh = new Mesh();

        // shared across all block faces
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();

        // one triangle list per material
        var submeshTris = new List<List<int>>();
        for (int i = 0; i < materials.Length; i++)
            submeshTris.Add(new List<int>());

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
                    AddCube(vertices, uvs, submeshTris, pos, x, y, z);
                }
            }
        }

        // build the final mesh
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.subMeshCount = materials.Length;

        // assign triangles per material index
        for (int i = 0; i < materials.Length; i++)
            mesh.SetTriangles(submeshTris[i], i);

        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }


    void AddCube(List<Vector3> verts,
             List<Vector2> uvs,
             List<List<int>> submeshTris,
             Vector3 pos, int x, int y, int z)
    {

        BlockType block = blocks[x, y, z];
        int matIndex = GetMaterialIndex(block);
        List<int> targetTris = submeshTris[matIndex];

        // TOP
        if (y + 1 >= chunkSizeY || blocks[x, y + 1, z] == BlockType.Air)
        {
            Vector3[] faceVerts = new Vector3[] {
            pos + new Vector3(0,1,0),
            pos + new Vector3(1,1,0),
            pos + new Vector3(1,1,1),
            pos + new Vector3(0,1,1)
        };
            int startIndex = verts.Count;
            verts.AddRange(faceVerts);

            targetTris.AddRange(new int[] {
            startIndex, startIndex + 2, startIndex + 1,
            startIndex, startIndex + 3, startIndex + 2
        });

            uvs.AddRange(new Vector2[] {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1)
        });
        }

        // BOTTOM
        if (y - 1 < 0 || blocks[x, y - 1, z] == BlockType.Air)
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,0),
                pos + new Vector3(1,0,0),
                pos + new Vector3(1,0,1),
                pos + new Vector3(0,0,1)
            };
            int startIndex = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { startIndex, startIndex + 1, startIndex + 2, startIndex, startIndex + 2, startIndex + 3 });

            uvs.AddRange(new Vector2[] {
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1),
                new Vector2(0,1)
            });
        }

        //RIGHT
        if (x + 1 >= chunkSizeX || blocks[x+1, y, z] == BlockType.Air)
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(1,0,0),
                pos + new Vector3(1,1,0),
                pos + new Vector3(1,1,1),
                pos + new Vector3(1,0,1)
            };
            int startIndex = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { startIndex, startIndex + 1, startIndex + 2, startIndex, startIndex + 2, startIndex + 3 });

            uvs.AddRange(new Vector2[] {
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1),
                new Vector2(0,1)
            });
        }

        //LEFT
        if (x - 1 < 0 || blocks[x - 1, y, z] == BlockType.Air)
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,0),
                pos + new Vector3(0,1,0),
                pos + new Vector3(0,1,1),
                pos + new Vector3(0,0,1)
            };
            int startIndex = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { startIndex, startIndex + 2, startIndex + 1, startIndex, startIndex + 3, startIndex + 2 });

            uvs.AddRange(new Vector2[] {
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1),
                new Vector2(0,1)
            });
        }

        //FRONT
        if (z + 1 >= chunkSizeZ || blocks[x, y, z + 1] == BlockType.Air)
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,1),
                pos + new Vector3(0,1,1),
                pos + new Vector3(1,1,1),
                pos + new Vector3(1,0,1)
            };
            int startIndex = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { startIndex, startIndex + 2, startIndex + 1, startIndex, startIndex + 3, startIndex + 2 });

            uvs.AddRange(new Vector2[] {
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1),
                new Vector2(0,1)
            });
        }

        //BACK
        if (z - 1 < 0 || blocks[x, y, z - 1] == BlockType.Air)
        {
            Vector3[] faceVerts = new Vector3[] {
                pos + new Vector3(0,0,0),
                pos + new Vector3(0,1,0),
                pos + new Vector3(1,1,0),
                pos + new Vector3(1,0,0)
            };
            int startIndex = verts.Count;
            verts.AddRange(faceVerts);
            targetTris.AddRange(new int[] { startIndex, startIndex + 1, startIndex + 2, startIndex, startIndex + 2, startIndex + 3 });

            uvs.AddRange(new Vector2[] {
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1),
                new Vector2(0,1)
            });
        }
    }

    int GetMaterialIndex(BlockType type)
    {
        switch (type)
        {
            case BlockType.Dirt: return 0;
            case BlockType.Grass: return 1;
            case BlockType.Stone: return 2;
            default: return 0; // fallback
        }
    }
}
