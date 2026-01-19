using UnityEngine;
using UnityEngine.UI; 

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100;
    public float currentHealth;

    public Slider healthSlider;

    void Start()
    {
        currentHealth = maxHealth;
        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        healthSlider.value = currentHealth;
    }

    void Update()
    {
        TakeDamage(1 * Time.deltaTime);
    }

    public bool IsAlive()
    {
        if(currentHealth > 0) { return true; }else return false;
    }
}
