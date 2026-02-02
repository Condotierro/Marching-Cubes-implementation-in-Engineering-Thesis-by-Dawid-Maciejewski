using UnityEngine;

public class Collectible : MonoBehaviour
{
    [SerializeField] private int value = 1;
    [SerializeField] private int type = 0;

    public ShipController shipController;

    public void initialize(ShipController sc)
    {
        shipController = sc;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Layer check is faster than tag
        if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        Collect();
    }

    private void Update()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
    }

    private void Collect()
    {
        if(type == 0)
        {
            shipController.CollectedItemsScore += value;
        }
        else
        {
            shipController.health.TakeDamage(-value);
        }

        Destroy(gameObject);
    }
}
