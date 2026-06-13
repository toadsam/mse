using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Networked smoke or toxic cloud zone with scalable visuals and optional poison application.
public class SmokeZone : NetworkBehaviour
{
    [Header("Smoke")]
    // Visual root and trigger collider scaled by the networked radius.
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SphereCollider triggerCollider;

    [SerializeField] private float baseVisualRadius = 2.5f;

    [SerializeField] private float defaultRadius = 3.0f;
    [SerializeField] private float defaultDuration = 5.0f;

    [Header("Color")]
    // Colors used to distinguish normal smoke from toxic smoke.
    [SerializeField] private Color normalSmokeColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    [SerializeField] private Color toxicSmokeColor = new Color(0.15f, 1f, 0.15f, 0.85f);

    [Header("Toxic Cloud Damage")]
    [SerializeField] private LayerMask damageMask = ~0;

    [Header("Toxic Cloud Growth")]
    [SerializeField] private float toxicInitialDelay = 0.15f;
    [SerializeField] private float toxicDamageGrowthSeconds = 1.0f;

    [Range(0f, 1f)]
    [SerializeField] private float toxicStartRadiusMultiplier = 0.1f;
    [Range(0.1f, 1.2f)]
    [SerializeField] private float toxicMaxDamageRadiusMultiplier = 0.85f;

    [Networked] private float SpawnSimulationTimeNet { get; set; }

    [SerializeField] private float toxicApplyInterval = 0.35f;

    [SerializeField] private float defaultPoisonRefreshDuration = 1.25f;

    // Networked smoke state replicated to all clients.
    [Networked] private NetworkBool IsToxicNet { get; set; }
    [Networked] private float RadiusNet { get; set; }
    [Networked] private TickTimer LifeTimer { get; set; }

    [Networked] private NetworkId OwnerIdNet { get; set; }
    [Networked] private int PoisonDamagePerTickNet { get; set; }
    [Networked] private float PoisonRefreshDurationNet { get; set; }
    [Networked] private TickTimer ToxicApplyTimer { get; set; }

    private bool lastAppliedToxic;
    private Renderer[] smokeRenderers;
    private MaterialPropertyBlock smokePropertyBlock;

    private float lastAppliedRadius = -1f;
    private ParticleSystem[] particles;

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        particles = GetComponentsInChildren<ParticleSystem>(true);
        smokeRenderers = GetComponentsInChildren<Renderer>(true);
        smokePropertyBlock = new MaterialPropertyBlock();
    }

    // Initializes cloud radius, lifetime, poison settings, and visuals.
    public void Init(
        NetworkRunner runner,
        float radius,
        float duration,
        bool isToxic = false,
        NetworkId ownerId = default,
        int poisonDamagePerTick = 0,
        float poisonRefreshDuration = 0f
    )
    {
        if (!HasStateAuthority)
            return;

        RadiusNet = Mathf.Max(0.1f, radius);
        IsToxicNet = isToxic;

        OwnerIdNet = ownerId;
        PoisonDamagePerTickNet = Mathf.Max(0, poisonDamagePerTick);
        PoisonRefreshDurationNet = poisonRefreshDuration > 0f
            ? poisonRefreshDuration
            : defaultPoisonRefreshDuration;

        LifeTimer = TickTimer.CreateFromSeconds(runner, Mathf.Max(0.1f, duration));
        SpawnSimulationTimeNet = (float)runner.SimulationTime;

        if (isToxic && PoisonDamagePerTickNet > 0)
            ToxicApplyTimer = TickTimer.CreateFromSeconds(runner, 0.05f);
        else
            ToxicApplyTimer = default;

        ApplyRadius(RadiusNet);
        ApplySmokeColor(IsToxicNet);
        PlayParticles();
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            if (RadiusNet <= 0f)
                RadiusNet = defaultRadius;

            if (LifeTimer.ExpiredOrNotRunning(Runner))
                LifeTimer = TickTimer.CreateFromSeconds(Runner, defaultDuration);
        }

        float radius = RadiusNet <= 0f ? defaultRadius : RadiusNet;

        ApplyRadius(radius);
        ApplySmokeColor(IsToxicNet);
        PlayParticles();
    }

    public override void Render()
    {
        float radius = RadiusNet <= 0f ? defaultRadius : RadiusNet;

        if (!Mathf.Approximately(lastAppliedRadius, radius))
            ApplyRadius(radius);

        if (lastAppliedToxic != IsToxicNet)
            ApplySmokeColor(IsToxicNet);

        UpdateToxicColliderRadius();
    }

    // Despawns the cloud when the match ends or its lifetime expires.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase != MatchPhase.Playing)
        {
            Runner.Despawn(Object);
            return;
        }

        if (LifeTimer.ExpiredOrNotRunning(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        UpdateToxicColliderRadius();
        UpdateToxicCloud();
    }

    // Periodically applies poison to targets inside a toxic cloud.
    private void UpdateToxicCloud()
    {
        if (!IsToxicNet)
            return;

        if (PoisonDamagePerTickNet <= 0)
            return;

        if (!ToxicApplyTimer.ExpiredOrNotRunning(Runner))
            return;

        ToxicApplyTimer = TickTimer.CreateFromSeconds(
            Runner,
            Mathf.Max(0.05f, toxicApplyInterval)
        );

        ApplyPoisonToTargetsInRange();
    }

    // Calculates the growing toxic damage radius over time.
    private float GetCurrentToxicDamageRadius()
    {
        float finalRadius = Mathf.Max(0.1f, RadiusNet);

        if (!IsToxicNet)
            return finalRadius;

        if (Runner == null)
            return finalRadius * toxicMaxDamageRadiusMultiplier;

        float elapsed = Mathf.Max(0f, (float)(Runner.SimulationTime - SpawnSimulationTimeNet));
        elapsed -= Mathf.Max(0f, toxicInitialDelay);

        if (elapsed <= 0f)
            return finalRadius * toxicStartRadiusMultiplier;

        float growthTime = Mathf.Max(0.01f, toxicDamageGrowthSeconds);
        float t = Mathf.Clamp01(elapsed / growthTime);

        // SmoothStep makes the toxic radius grow more smoothly.
        t = t * t * (3f - 2f * t);

        float startRadius = finalRadius * toxicStartRadiusMultiplier;
        float endRadius = finalRadius * toxicMaxDamageRadiusMultiplier;

        return Mathf.Lerp(startRadius, endRadius, t);
    }

    // Finds targets in range and applies poison or dummy damage once per interval.
    private void ApplyPoisonToTargetsInRange()
    {
        float currentDamageRadius = GetCurrentToxicDamageRadius();

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            currentDamageRadius,
            damageMask,
            QueryTriggerInteraction.Ignore
        );

        PlayerNetwork ownerPlayer = FindOwnerPlayer();

        HashSet<NetworkId> poisonedPlayers = new HashSet<NetworkId>();
        HashSet<DummyTargetHealth> damagedDummies = new HashSet<DummyTargetHealth>();

        foreach (Collider col in hits)
        {
            PlayerNetwork targetPlayer = col.GetComponentInParent<PlayerNetwork>();

            if (targetPlayer != null && targetPlayer.Object != null)
            {
                NetworkId targetId = targetPlayer.Object.Id;

                // Remove this check if the owner should also be affected by their toxic cloud.
                if (targetId == OwnerIdNet)
                    continue;

                if (poisonedPlayers.Contains(targetId))
                    continue;

                poisonedPlayers.Add(targetId);

                PlayerHealth health = targetPlayer.Health;
                if (health != null)
                    health.ApplyPoison(PoisonDamagePerTickNet, PoisonRefreshDurationNet, ownerPlayer);

                continue;
            }

            // Dummy targets do not have poison state, so apply direct damage each cloud tick.
            DummyTargetHealth dummy = col.GetComponentInParent<DummyTargetHealth>();
            if (dummy != null && !damagedDummies.Contains(dummy))
            {
                damagedDummies.Add(dummy);
                dummy.TakeDamage(PoisonDamagePerTickNet);
            }
        }
    }

    private PlayerNetwork FindOwnerPlayer()
    {
        if (OwnerIdNet == default)
            return null;

        NetworkObject ownerObject = Runner.FindObject(OwnerIdNet);
        if (ownerObject == null)
            return null;

        return ownerObject.GetComponent<PlayerNetwork>();
    }

    // Updates collider radius and visual scale from a safe radius value.
    private void ApplyRadius(float radius)
    {
        float safeRadius = Mathf.Max(0.1f, radius);

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.radius = safeRadius;
        }

        if (visualRoot != null)
        {
            float scale = safeRadius / Mathf.Max(0.01f, baseVisualRadius);
            visualRoot.localScale = Vector3.one * scale;
        }

        lastAppliedRadius = safeRadius;
    }

    private void PlayParticles()
    {
        if (particles == null)
            return;

        foreach (ParticleSystem ps in particles)
        {
            if (ps == null)
                continue;

            ps.Clear(true);
            ps.Play(true);
        }
    }

    // Applies smoke color to particles and renderers.
    private void ApplySmokeColor(bool isToxic)
    {
        Color color = isToxic ? toxicSmokeColor : normalSmokeColor;

        if (particles != null)
        {
            foreach (ParticleSystem ps in particles)
            {
                if (ps == null)
                    continue;

                ParticleSystem.MainModule main = ps.main;
                main.startColor = color;
            }
        }

        if (smokeRenderers != null)
        {
            foreach (Renderer r in smokeRenderers)
            {
                if (r == null)
                    continue;

                r.GetPropertyBlock(smokePropertyBlock);
                smokePropertyBlock.SetColor("_BaseColor", color);
                smokePropertyBlock.SetColor("_Color", color);
                smokePropertyBlock.SetColor("_EmissionColor", color * (isToxic ? 0.6f : 0f));
                r.SetPropertyBlock(smokePropertyBlock);
            }
        }

        lastAppliedToxic = isToxic;
    }

    private void UpdateToxicColliderRadius()
    {
        if (triggerCollider == null)
            return;

        if (IsToxicNet)
            triggerCollider.radius = GetCurrentToxicDamageRadius();
        else
            triggerCollider.radius = RadiusNet;
    }

    private void OnDisable()
    {

        lastAppliedRadius = -1f;
        lastAppliedToxic = false;

        if (smokeRenderers != null)
        {
            foreach (Renderer r in smokeRenderers)
            {
                if (r != null)
                    r.SetPropertyBlock(null);
            }
        }
    }
}