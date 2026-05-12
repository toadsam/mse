using UnityEngine;

public class DummyTargetHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool resetWhenDead = true;

    private int currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log($"[Dummy] Hit! HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Debug.Log("[Dummy] Dead");

            if (resetWhenDead)
                ResetHealth();
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        Debug.Log($"[Dummy] Reset HP: {currentHealth}/{maxHealth}");
    }
}