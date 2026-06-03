using UnityEngine;

public class DummyTargetHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 10000;
    [SerializeField] private bool resetWhenDead = true;

    [Header("Hit Reaction")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.12f;

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

    private void ApplyColor(Color color)
    {
        if (targetRenderer == null || propertyBlock == null)
            return;

        targetRenderer.GetPropertyBlock(propertyBlock);

        // URP Lit 계열은 보통 _BaseColor를 사용합니다.
        propertyBlock.SetColor(BaseColorId, color);

        // Standard/일부 커스텀 셰이더 호환용입니다.
        propertyBlock.SetColor(ColorId, color);

        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}