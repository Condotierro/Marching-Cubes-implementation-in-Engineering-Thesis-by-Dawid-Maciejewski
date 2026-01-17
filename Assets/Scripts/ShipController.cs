using System.Diagnostics;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [Header("Movement")]
    public float acceleration = 20f;
    public float maxSpeed = 25f;
    public float damping = 5f;
    public float verticalSmooth = 3f; 

    [Header("Rotation")]
    public float turnSpeed = 60f;
    public float tiltAngle = 15f;
    public float tiltSmooth = 5f;

    private Rigidbody rb;
    private float currentTilt = 0f;

    [Header("Weapons")]
    public GameObject projectilePrefab;
    public Transform firePoint; 
    public float projectileSpeed = 50f;
    public float fireCooldown = 0.2f;

    private float lastFireTime;

    public AudioClip rammingSound; 
    private AudioSource audioSource;

    public MapLayer currentMapLayer;

    [SerializeField] GameObject debrisPrefab;
    [SerializeField] float debrisSpawnChance = 1f; // 15% of destroyed blocks spawn debris
    [SerializeField] float debrisForce = 8f;

    [SerializeField] TextMeshProUGUI scoreText;

    public enum MapLayer
    {
        Top,
        Medium,
        Bottom
    }

    public const float TopHeight = 55;
    public const float MediumHeight = 33;
    public const float BottomHeight = 11;

    public PlayerHealth health;

    void Start()
    {
        currentMapLayer = MapLayer.Medium;
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0.5f;
        rb.angularDrag = 2f;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.playOnAwake = false;
    }

    void LowerLayer()
    {
        if (hasSpaceUnderneath() == false) { return; }
        //implement raycast check if free space?
        health.TakeDamage(-25f);
        switch (currentMapLayer)
        {
            case MapLayer.Top:
                currentMapLayer = MapLayer.Medium;
                break;
            case MapLayer.Medium:
                currentMapLayer = MapLayer.Bottom;
                break;
        }
    }

    void HigherLayer()
    {
        if(isNearGeyser() == false) { return; }

        switch (currentMapLayer)
        {
            case MapLayer.Bottom:
                currentMapLayer = MapLayer.Medium;
                break;
            case MapLayer.Medium:
                currentMapLayer = MapLayer.Top;
                break;
        }
    }


    private bool hasSpaceUnderneath()
    {
        RaycastHit hit;

        if (Physics.Raycast(
            transform.position - new Vector3(0,5,0),
            Vector3.down,
            out hit,
            10f
        ))
        {
            if(hit.collider.TryGetComponent<Chunk>(out var target))
    {
                return false;
            }
        }
        return true;
    }

    [SerializeField] Collider[] results = new Collider[10];
    [SerializeField] float checkRadius = 10f;
    [SerializeField] LayerMask proceduralLayer;

    private bool isNearGeyser()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            checkRadius,
            results,
            proceduralLayer
        );
        return count > 0;
    }

    private void Update()
    {
        HandleShooting();
        UpdateScore();

        if (Input.GetKeyDown(KeyCode.P)) 
        {
            HigherLayer();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            LowerLayer();
        }

        if(health.currentHealth <= 0)
        {
            rb.useGravity = true;
            this.gameObject.GetComponent<ShipController>().enabled = false;
        }
    }

    public int brokenBlocks = 0;
    int crashes = 0;
    private void UpdateScore()
    {
        float score = Mathf.Max(0,(this.gameObject.transform.position.z * 1f) + (brokenBlocks * 5) - (crashes * 20));
        scoreText.text = "Score : " + score.ToString("0.00");
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 moveDir = (transform.forward * v) + (transform.right * h);

        rb.AddForce(moveDir.normalized * acceleration, ForceMode.Acceleration);

        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;

        if (moveDir.magnitude < 0.1f)
        {
            rb.velocity = Vector3.Lerp(rb.velocity, new Vector3(0, rb.velocity.y, 0), damping * Time.fixedDeltaTime);
        }

        float targetY = GetLayerHeight(currentMapLayer);
        float newY = Mathf.Lerp(rb.position.y, targetY, verticalSmooth * Time.fixedDeltaTime);

        rb.MovePosition(new Vector3(rb.position.x, newY, rb.position.z));
    }

    float GetLayerHeight(MapLayer layer)
    {
        return layer switch
        {
            MapLayer.Top => TopHeight,
            MapLayer.Medium => MediumHeight,
            MapLayer.Bottom => BottomHeight,
            _ => MediumHeight
        };
    }

    void HandleRotation()
    {
        if (Input.GetKey(KeyCode.A))
        {
            transform.Rotate(Vector3.up * -turnSpeed * Time.fixedDeltaTime, Space.Self);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Rotate(Vector3.up * turnSpeed * Time.fixedDeltaTime, Space.Self);
        }

        float targetTilt = -Input.GetAxis("Horizontal") * tiltAngle;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.fixedDeltaTime * tiltSmooth);
        transform.localRotation = Quaternion.Euler(0, transform.localEulerAngles.y, currentTilt);
    }

    void OnCollisionEnter(Collision collision)
    {
        Chunk chunk = collision.collider.GetComponentInParent<Chunk>();
        if (chunk != null)
        {
            HandleVoxelCollision(chunk, collision.contacts[0].point);
        }
    }
    void HandleVoxelCollision(Chunk chunk, Vector3 hitPoint)
    {
        Vector3 localPos = hitPoint - chunk.transform.position;

        int radius = 2;
        int cx = Mathf.FloorToInt(localPos.x);
        int cy = Mathf.FloorToInt(localPos.y);
        int cz = Mathf.FloorToInt(localPos.z);
        crashes++;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    if (x * x + y * y + z * z <= radius * radius)
                    {
                        int bx = cx + x;
                        int by = cy + y;
                        int bz = cz + z;

                        if (bx < 0 || by < 0 || bz < 0 ||
                            bx >= Chunk.chunkSizeX || by >= Chunk.chunkSizeY || bz >= Chunk.chunkSizeZ)
                            continue;

                        if (chunk.blocks[bx, by, bz] != BlockType.Air)
                        {
                            // Spawn debris with some randomness
                            if (Random.value < debrisSpawnChance)
                            {
                                SpawnVoxelDebris(chunk, bx, by, bz);
                            }
                        }

                        chunk.blocks[bx, by, bz] = BlockType.Air;
                    }
                }
            }
        }

        if (!audioSource.isPlaying)
        {
            audioSource.clip = rammingSound;
            audioSource.Play();
        }

        health.TakeDamage(5f);
        sw.Restart();
        chunk.GenerateMesh();
        sw.Stop();
        RuntimeMetrics.Record("Chunk.GenerateMesh.ms", sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        chunk.UpdateCollider();
        sw.Stop();
        RuntimeMetrics.Record("Chunk.UpdateCollider.ms", sw.Elapsed.TotalMilliseconds);

    }
    static Stopwatch sw = new Stopwatch();

    void SpawnVoxelDebris(Chunk chunk, int bx, int by, int bz)
    {
        Vector3 worldPos = chunk.transform.position + new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);

        GameObject debris = Instantiate(debrisPrefab, worldPos, Random.rotation);

        Rigidbody rb = debris.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 randomDir = Random.onUnitSphere;
            rb.AddForce(randomDir * debrisForce, ForceMode.Impulse);
        }

        Destroy(debris, 4f); // auto cleanup
    }


    void HandleShooting()
    {
        if (Input.GetMouseButton(0) && Time.time > lastFireTime + fireCooldown)
        {
            lastFireTime = Time.time;

            if (projectilePrefab && firePoint)
            {
                GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
                Rigidbody prb = proj.GetComponent<Rigidbody>();

                if (prb)
                {
                    prb.velocity = firePoint.forward * projectileSpeed;
                }

                health.TakeDamage(1f);
            }
        }
    }

}
