using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 50f;
    public float lifetime = 5f;
    public float destroyRadius = 3f;
    public float hitForce = 5f;

    [Header("Explosion FX")]
    public GameObject explosionPrefab; 
    public AudioClip explosionSound;   
    public float explosionVolume = 0.8f;

    private Rigidbody rb;
    private AudioSource audioSource;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.velocity = transform.forward * speed;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.playOnAwake = false;

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contacts[0].point;
        Quaternion hitRotation = Quaternion.LookRotation(collision.contacts[0].normal);

        if (collision.rigidbody != null)
            collision.rigidbody.AddForce(-collision.contacts[0].normal * hitForce, ForceMode.Impulse);

        Chunk chunk = collision.collider.GetComponentInParent<Chunk>();
        if (chunk != null)
            DestroyVoxel(chunk, hitPoint);

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, hitPoint, hitRotation);
            Destroy(explosion, 1f);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, hitPoint, explosionVolume);
        }

        Destroy(gameObject);
    }

    void DestroyVoxel(Chunk chunk, Vector3 hitPoint)
    {
        Vector3 localPos = hitPoint - chunk.transform.position;
        int cx = Mathf.FloorToInt(localPos.x);
        int cy = Mathf.FloorToInt(localPos.y);
        int cz = Mathf.FloorToInt(localPos.z);

        int radius = Mathf.CeilToInt(destroyRadius);

        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                for (int z = -radius; z <= radius; z++)
                {
                    if (x * x + y * y + z * z <= destroyRadius * destroyRadius)
                    {
                        int bx = cx + x;
                        int by = cy + y;
                        int bz = cz + z;

                        if (bx < 0 || by < 0 || bz < 0 ||
                            bx >= Chunk.chunkSizeX || by >= Chunk.chunkSizeY || bz >= Chunk.chunkSizeZ)
                            continue;

                        chunk.blocks[bx, by, bz] = BlockType.Air;
                    }
                }

        chunk.GenerateMesh();
        chunk.UpdateCollider();
    }
}
