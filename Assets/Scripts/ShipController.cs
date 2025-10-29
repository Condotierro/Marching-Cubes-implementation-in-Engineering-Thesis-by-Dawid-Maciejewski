using System.Threading;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [Header("Movement")]
    public float acceleration = 20f;
    public float maxSpeed = 25f;
    public float damping = 5f;

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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0.5f;
        rb.angularDrag = 2f;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        HandleShooting();
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
        UpdateCamera();
    }

    void HandleMovement()
    {
        // Input directions
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float ascend = 0f;

        if (Input.GetKey(KeyCode.Space))
            ascend = 1f;
        else if (Input.GetKey(KeyCode.LeftControl))
            ascend = -1f;

        // Calculate movement direction relative to ship orientation
        Vector3 moveDir = (transform.forward * v) + (transform.right * h) + (transform.up * ascend);

        // Smooth acceleration
        rb.AddForce(moveDir.normalized * acceleration, ForceMode.Acceleration);

        // Cap the speed
        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;

        // Apply a bit of manual damping for smoother deceleration
        if (moveDir.magnitude < 0.1f)
            rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, damping * Time.fixedDeltaTime);

        if (Mathf.Approximately(ascend, 0f))
        {
            Vector3 vel = rb.velocity;
            vel.y = 0f;
            rb.velocity = vel;
        }
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
        // Convert world hit position to local chunk space
        Vector3 localPos = hitPoint - chunk.transform.position;

        int radius = 2; // Radius of destruction (adjust for effect)
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
            }
        }
    }

}
