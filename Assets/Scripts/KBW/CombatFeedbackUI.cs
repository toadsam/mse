using UnityEngine;

// Shows local combat feedback such as hit markers, damage flash, and SFX.
public class CombatFeedbackUI : MonoBehaviour
{
    // Hit marker UI shown when the local player confirms damage.
    [Header("Hit Marker")]
    [SerializeField] private GameObject hitMarkerRoot;
    [SerializeField] private CanvasGroup hitMarkerGroup;
    [SerializeField] private float hitMarkerDuration = 0.12f;

    // Damage flash UI shown when the local player takes damage.
    [Header("Damage Flash")]
    [SerializeField] private GameObject damageFlashRoot;
    [SerializeField] private CanvasGroup damageFlashGroup;
    [SerializeField] private float damageFlashDuration = 0.25f;
    [SerializeField] private float damageFlashMaxAlpha = 0.45f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitMarkerSound;
    [SerializeField] private AudioClip damagedSound;

    // Current local player and health object being observed.
    private PlayerNetwork observedPlayer;
    private PlayerHealth observedHealth;

    // Previous network counters used to detect new feedback events.
    private int lastHitConfirmCount;
    private int lastDamageFeedbackCount;

    private float hitMarkerTimer;
    private float damageFlashTimer;

    private void Awake()
    {
        SetHitMarkerAlpha(0f);
        SetDamageFlashAlpha(0f);

        if (hitMarkerRoot != null)
            hitMarkerRoot.SetActive(false);

        if (damageFlashRoot != null)
            damageFlashRoot.SetActive(false);
    }

    // Tracks the local player and updates feedback timers every frame.
    private void Update()
    {
        PlayerNetwork localPlayer = GameManager.Instance != null
            ? GameManager.Instance.LocalPlayer
            : null;

        if (localPlayer == null)
        {
            observedPlayer = null;
            observedHealth = null;

            hitMarkerTimer = 0f;
            damageFlashTimer = 0f;

            SetHitMarkerAlpha(0f);
            SetDamageFlashAlpha(0f);
            return;
        }

        if (observedPlayer != localPlayer)
        {
            observedPlayer = localPlayer;
            observedHealth = localPlayer.Health;

            lastHitConfirmCount = observedPlayer.HitConfirmCount;
            lastDamageFeedbackCount = observedHealth != null ? observedHealth.DamageFeedbackCount : 0;

            hitMarkerTimer = 0f;
            damageFlashTimer = 0f;

            SetHitMarkerAlpha(0f);
            SetDamageFlashAlpha(0f);
        }

        CheckHitMarkerEvent();
        CheckDamageFlashEvent();

        UpdateHitMarker(Time.deltaTime);
        UpdateDamageFlash(Time.deltaTime);
    }

    // Starts the hit marker when the hit confirm counter increases.
    private void CheckHitMarkerEvent()
    {
        if (observedPlayer == null)
            return;

        int current = observedPlayer.HitConfirmCount;

        // Counter can reset to 0 when a new round starts.
        if (current < lastHitConfirmCount)
        {
            lastHitConfirmCount = current;
            return;
        }

        if (current == lastHitConfirmCount)
            return;

        TriggerHitMarker();
        lastHitConfirmCount = current;
    }

    // Starts the damage flash when the damage feedback counter increases.
    private void CheckDamageFlashEvent()
    {
        if (observedHealth == null)
            return;

        int current = observedHealth.DamageFeedbackCount;

        // Counter can reset to 0 when a new round starts.
        if (current < lastDamageFeedbackCount)
        {
            lastDamageFeedbackCount = current;
            return;
        }

        if (current == lastDamageFeedbackCount)
            return;

        TriggerDamageFlash();
        lastDamageFeedbackCount = current;
    }

    private void TriggerHitMarker()
    {
        hitMarkerTimer = hitMarkerDuration;

        if (hitMarkerRoot != null)
            hitMarkerRoot.SetActive(true);

        SetHitMarkerAlpha(1f);

        AudioSettingsUI.PlaySfx(audioSource, hitMarkerSound, GameAudioClipId.HitMarker);
    }

    private void TriggerDamageFlash()
    {
        damageFlashTimer = damageFlashDuration;

        if (damageFlashRoot != null)
            damageFlashRoot.SetActive(true);

        SetDamageFlashAlpha(damageFlashMaxAlpha);

        AudioSettingsUI.PlaySfx(audioSource, damagedSound, GameAudioClipId.Damage);
    }

    private void UpdateHitMarker(float deltaTime)
    {
        if (hitMarkerTimer <= 0f)
        {
            SetHitMarkerAlpha(0f);

            if (hitMarkerRoot != null && hitMarkerRoot.activeSelf)
                hitMarkerRoot.SetActive(false);

            return;
        }

        hitMarkerTimer -= deltaTime;

        float t = hitMarkerDuration > 0f
            ? Mathf.Clamp01(hitMarkerTimer / hitMarkerDuration)
            : 0f;

        SetHitMarkerAlpha(t);
    }

    private void UpdateDamageFlash(float deltaTime)
    {
        if (damageFlashTimer <= 0f)
        {
            SetDamageFlashAlpha(0f);

            if (damageFlashRoot != null && damageFlashRoot.activeSelf)
                damageFlashRoot.SetActive(false);

            return;
        }

        damageFlashTimer -= deltaTime;

        float t = damageFlashDuration > 0f
            ? Mathf.Clamp01(damageFlashTimer / damageFlashDuration)
            : 0f;

        SetDamageFlashAlpha(t * damageFlashMaxAlpha);
    }

    private void SetHitMarkerAlpha(float alpha)
    {
        if (hitMarkerGroup != null)
            hitMarkerGroup.alpha = alpha;
    }

    private void SetDamageFlashAlpha(float alpha)
    {
        if (damageFlashGroup != null)
            damageFlashGroup.alpha = alpha;
    }
}