using UnityEngine;

// Simple test target used to verify projectile and damage behavior.
public class DummyTargetHealth : MonoBehaviour
{
    [Header("Health")]
    // Test target health values configured in the Inspector.
    [SerializeField] private int maxHealth = 10000;
    [SerializeField] private bool resetWhenDead = true;

    // Renderer and colors used for a short hit flash.
    [Header("Hit Reaction")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.12f;

    // Current dummy HP used only for local testing.
    private int currentHealth;

    private MaterialPropertyBlock propertyBlock;
    private float hitFlashTimer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        currentHealth = maxHealth;

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        propertyBlock = new MaterialPropertyBlock();

        ApplyColor(normalColor);
    }

    private void Update()
    {
        if (hitFlashTimer <= 0f)
            return;

        hitFlashTimer -= Time.deltaTime;

        if (hitFlashTimer <= 0f)
        {
            ApplyColor(normalColor);
        }
    }

    // Applies test damage and optionally resets the dummy when it dies.
    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log($"[Dummy] Hit! HP: {currentHealth}/{maxHealth}");

        PlayHitReaction();

        if (currentHealth <= 0)
        {
            Debug.Log("[Dummy] Dead");

            if (resetWhenDead)
                ResetHealth();
        }
    }

    // Restores HP and visual state for repeated tests.
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        Debug.Log($"[Dummy] Reset HP: {currentHealth}/{maxHealth}");

        ApplyColor(normalColor);
        hitFlashTimer = 0f;
    }

    private void PlayHitReaction()
    {
        hitFlashTimer = hitFlashDuration;
        ApplyColor(hitColor);
    }

    // Applies color through a property block without duplicating materials.
    private void ApplyColor(Color color)
    {
        if (targetRenderer == null || propertyBlock == null)
            return;

        targetRenderer.GetPropertyBlock(propertyBlock);

        // URP Lit shaders usually use _BaseColor.
        propertyBlock.SetColor(BaseColorId, color);

        // Also support Standard or custom shaders that use _Color.
        propertyBlock.SetColor(ColorId, color);

        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}