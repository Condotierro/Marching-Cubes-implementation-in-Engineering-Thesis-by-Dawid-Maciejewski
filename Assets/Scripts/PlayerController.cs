using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Look")]
    public float lookSpeed = 2f;
    public Camera playerCamera;

    [Header("Block Interaction")]
    public float reachDistance = 5f;

    [Header("Ground Check")]
    public Collider groundCheckCollider; 

    private Rigidbody rb;
    private float pitch = 0f;
    private bool grounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Look();

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            Vector3 vel = rb.velocity;
            vel.y = 0f; 
            rb.velocity = vel;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }

        if (Input.GetMouseButtonDown(0)) MineBlock();
        if (Input.GetMouseButtonDown(1)) PlaceBlock();
    }

    void FixedUpdate()
    {
        Move();
    }

    void Move()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = (transform.right * h + transform.forward * v) * walkSpeed * Time.fixedDeltaTime;
        Vector3 targetPosition = rb.position + move;

        rb.MovePosition(targetPosition + Vector3.up * rb.velocity.y * Time.fixedDeltaTime);
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other != groundCheckCollider && other != this.GetComponent<Collider>())
            grounded = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other != groundCheckCollider && other != this.GetComponent<Collider>())
            grounded = false;
    }

    bool IsGrounded()
    {
        return grounded;
    }

    void MineBlock()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reachDistance))
        {
            Chunk chunk = hit.collider.GetComponentInParent<Chunk>();
            if (chunk != null)
            {
                Vector3 localPos = hit.point - hit.normal * 0.01f - chunk.transform.position;

                int x = Mathf.FloorToInt(localPos.x);
                int y = Mathf.FloorToInt(localPos.y);
                int z = Mathf.FloorToInt(localPos.z);

                x = Mathf.Clamp(x, 0, Chunk.chunkSizeX - 1);
                y = Mathf.Clamp(y, 0, Chunk.chunkSizeY - 1);
                z = Mathf.Clamp(z, 0, Chunk.chunkSizeZ - 1);

                chunk.blocks[x, y, z] = BlockType.Air;
                chunk.GenerateMesh();
                chunk.UpdateCollider();
            }
        }
    }

    void PlaceBlock()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reachDistance))
        {
            Chunk chunk = hit.collider.GetComponentInParent<Chunk>();
            if (chunk != null)
            {
                Vector3 placePos = hit.point + hit.normal * 0.01f;
                Vector3 localPos = placePos - chunk.transform.position;

                int x = Mathf.FloorToInt(localPos.x);
                int y = Mathf.FloorToInt(localPos.y);
                int z = Mathf.FloorToInt(localPos.z);

                x = Mathf.Clamp(x, 0, Chunk.chunkSizeX - 1);
                y = Mathf.Clamp(y, 0, Chunk.chunkSizeY - 1);
                z = Mathf.Clamp(z, 0, Chunk.chunkSizeZ - 1);

                chunk.blocks[x, y, z] = BlockType.Dirt;
                chunk.GenerateMesh();
                chunk.UpdateCollider();
            }
        }
    }

}
