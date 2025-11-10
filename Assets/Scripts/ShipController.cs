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

    [Header("Camera")]
    public Transform shipCamera;
    public Vector3 cameraOffset = new Vector3(0, 3, -8);
    public float cameraFollowSpeed = 5f;

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
    public enum MapLayer
    {
        Top,
        Medium,
        Bottom
    }

    public const float TopHeight = 50;
    public const float MediumHeight = 35;
    public const float BottomHeight = 20;

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

    void SwitchLayer()
    {
        currentMapLayer = currentMapLayer switch
        {
            MapLayer.Bottom => MapLayer.Medium,
            MapLayer.Medium => MapLayer.Top,
            MapLayer.Top => MapLayer.Bottom,
            _ => currentMapLayer
        };
    }

    void LowerLayer()
    {
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

    private void Update()
    {
        HandleShooting();

        if (Input.GetKeyDown(KeyCode.O)) 
        {
            HigherLayer();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            LowerLayer();
        }

        if(health.currentHealth <= 0)
        {
            rb.useGravity = true;
            this.gameObject.GetComponent<ShipController>().enabled = false;
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
        UpdateCamera();
    }

    void HandleMovement()
    {
        // Horizontal/forward movement
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 moveDir = (transform.forward * v) + (transform.right * h);

        rb.AddForce(moveDir.normalized * acceleration, ForceMode.Acceleration);

        // Cap speed
        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;

        // Damping when no input
        if (moveDir.magnitude < 0.1f)
        {
            rb.velocity = Vector3.Lerp(rb.velocity, new Vector3(0, rb.velocity.y, 0), damping * Time.fixedDeltaTime);
        }

        // -------- Vertical Layer Handling --------
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

    void UpdateCamera()
    {
        if (!shipCamera) return;

        Vector3 desiredPos = transform.TransformPoint(cameraOffset);
        shipCamera.position = Vector3.Lerp(shipCamera.position, desiredPos, Time.deltaTime * cameraFollowSpeed);

        Quaternion desiredRot = Quaternion.LookRotation(transform.position + transform.forward * 10f - shipCamera.position, Vector3.up);
        shipCamera.rotation = Quaternion.Lerp(shipCamera.rotation, desiredRot, Time.deltaTime * cameraFollowSpeed);
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

        chunk.GenerateMesh();
        chunk.UpdateCollider();
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
