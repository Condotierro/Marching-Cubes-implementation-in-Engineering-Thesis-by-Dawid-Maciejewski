using UnityEngine;

public class CameraBehaviour : MonoBehaviour
{
    [SerializeField] Transform ship;
    [SerializeField] float speed;
    [SerializeField] float heightOfCamera;
    [SerializeField] float cameraZoffset;
    public bool isAlive = true;

    void LateUpdate()
    {
        if (isAlive)
        {
            Vector3 position = Vector3.Lerp(transform.position,
                ship.position + 
                new Vector3(0, heightOfCamera, cameraZoffset), 
                speed * Time.deltaTime);
            transform.position = position;
        }
    }
}
