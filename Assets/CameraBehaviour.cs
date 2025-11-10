using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraBehaviour : MonoBehaviour
{
    public Transform ship;
    public float speed;
    public float heightOfCamera;
    public float cameraZoffset;

    void LateUpdate()
    {
        Vector3 position = Vector3.Lerp(transform.position, ship.position + new Vector3(0, heightOfCamera, cameraZoffset), speed * Time.deltaTime);
        transform.position = position;
    }
}
