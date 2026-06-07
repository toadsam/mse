using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class RifleProjectile : NetworkBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float radius = 0.12f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float lifeSeconds = 3f;

    [Header("Grow Shot")]
    [SerializeField] private float growFullDistance = 25f;

    [Header("Explosion")]
    [SerializeField] private NetworkPrefabRef explosionFxPrefab;

    [Header("Visual")]
    [SerializeField] private Renderer[] projectileRenderers;
    [SerializeField] private TrailRenderer[] trailRenderers;
    [SerializeField] private ParticleSystem[] projectileParticles;

    [SerializeField] private Color normalProjectileColor = Color.white;
    [SerializeField] private Color poisonProjectileColor = new Color(0.15f, 1f, 0.15f, 1f);

    [Header("Area FX")]
    [SerializeField] private NetworkPrefabRef toxicCloudPrefab;

    [Networked] private NetworkBool PoisonVisualNet { get; set; }

    private MaterialPropertyBlock visualPropertyBlock;
    private bool lastAppliedPoisonVisual;

    private Vector3 direction;
    private float speed;
    private int damage;
    private NetworkId ownerId;
    private TickTimer lifeTimer;
    private bool hasInitialized;

    private float baseRadius;
    private Vector3 baseScale;
    private Vector3 spawnPosition;

    private int remainingBounces;
    private int remainingPierces;
    private bool targetBounce;

    private bool growDamageByDistance;
    private float maxGrowDamageMultiplier = 1f;

    private bool explodesOnImpact;
    private float explosionRadius;
    private float explosionDamageMultiplier;

    private bool appliesPoison;
    private int poisonDamagePerTick;
    private float poisonDuration;

    private bool createsToxicCloud;
    private float cloudRadius;
    private float cloudDuration;

    [Networked] private float SizeMultiplierNet { get; set; }
    private float lastAppliedVisualSize = -1f;

    private void Awake()
    {
        baseRadius = radius;
        baseScale = transform.localScale;

        if (projectileRenderers == null || projectileRenderers.Length == 0)
            projectileRenderers = GetComponentsInChildren<Renderer>(true);

        if (trailRenderers == null || trailRenderers.Length == 0)
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);

        if (projectileParticles == null || projectileParticles.Length == 0)
            projectileParticles = GetComponentsInChildren<ParticleSystem>(true);

        visualPropertyBlock = new MaterialPropertyBlock();
    }

    public void Init(
        NetworkRunner runner,
        PlayerNetwork owner,
        Vector3 projectileDirection,
        float projectileSpeed,
        int projectileDamage,
        float projectileSizeMultiplier = 1f,
        int bounceCount = 0,
        int pierceCount = 0,
        bool targetBounce = false,
        bool growDamageByDistance = false,
        float maxGrowDamageMultiplier = 1f,
        bool explodesOnImpact = false,
        float explosionRadius = 0f,
        float explosionDamageMultiplier = 0f,
        bool appliesPoison = false,
        int poisonDamagePerTick = 0,
        float poisonDuration = 0f,
        bool createsToxicCloud = false,
        float cloudRadius = 0f,
        float cloudDuration = 0f
    )
    {
        direction = projectileDirection.sqrMagnitude > 0.0001f
            ? projectileDirection.normalized
            : transform.forward;

        speed = projectileSpeed;
        damage = projectileDamage;

        ownerId = owner != null && owner.Object != null
            ? owner.Object.Id
            : default;

        lifeTimer = TickTimer.CreateFromSeconds(runner, lifeSeconds);
        spawnPosition = transform.position;

        remainingBounces = Mathf.Max(0, bounceCount);
        remainingPierces = Mathf.Max(0, pierceCount);
        this.targetBounce = targetBounce;

        this.growDamageByDistance = growDamageByDistance;
        this.maxGrowDamageMultiplier = Mathf.Max(1f, maxGrowDamageMultiplier);

        this.explodesOnImpact = explodesOnImpact;
        this.explosionRadius = Mathf.Max(0f, explosionRadius);
        this.explosionDamageMultiplier = Mathf.Max(0f, explosionDamageMultiplier);

        this.appliesPoison = appliesPoison;
        this.poisonDamagePerTick = Mathf.Max(0, poisonDamagePerTick);
        this.poisonDuration = Mathf.Max(0f, poisonDuration);

        this.createsToxicCloud = createsToxicCloud;
        this.cloudRadius = Mathf.Max(0f, cloudRadius);
        this.cloudDuration = Mathf.Max(0f, cloudDuration);

        float safeSize = Mathf.Max(0.1f, projectileSizeMultiplier);
        SizeMultiplierNet = safeSize;
        ApplyProjectileSize(safeSize);

        PoisonVisualNet = appliesPoison || createsToxicCloud;
        ApplyProjectileVisual(PoisonVisualNet);
        hasInitialized = true;
    }

    public override void Spawned()
    {
        if (SizeMultiplierNet <= 0f)
            SizeMultiplierNet = 1f;

        ApplyProjectileSize(SizeMultiplierNet);
        ApplyProjectileVisual(PoisonVisualNet);
    }

    public override void Render()
    {
        float size = SizeMultiplierNet <= 0f ? 1f : SizeMultiplierNet;

        if (!Mathf.Approximately(lastAppliedVisualSize, size))
            ApplyProjectileSize(size);

        if (lastAppliedPoisonVisual != PoisonVisualNet)
            ApplyProjectileVisual(PoisonVisualNet);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!hasInitialized)
            return;

        if (lifeTimer.ExpiredOrNotRunning(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = currentPosition + direction * speed * Runner.DeltaTime;

        bool hitSomething = CheckHit(currentPosition, nextPosition);

        if (hitSomething)
            return;

        transform.position = nextPosition;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private bool CheckHit(Vector3 from, Vector3 to)
    {
        Vector3 move = to - from;
        float distance = move.magnitude;

        if (distance <= 0.0001f)
            return false;

        Vector3 dir = move / distance;

        if (!Physics.SphereCast(
                from,
                radius,
                dir,
                out RaycastHit hit,
                distance,
                hitMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

        if (hitPlayer != null &&
            hitPlayer.Object != null &&
            hitPlayer.Object.Id == ownerId)
        {
            return false;
        }

        if (hitPlayer != null && hitPlayer.TryBlockProjectile(hit.point, direction))
        {
            Runner.Despawn(Object);
            return true;
        }

        bool damagedTarget = TryApplyDirectHit(hit);

        if (explodesOnImpact)
            ApplyExplosionDamage(hit.point);

        if (createsToxicCloud)
            SpawnToxicCloudPlaceholder(hit.point);

        if (damagedTarget && remainingPierces > 0)
        {
            remainingPierces--;

            transform.position = hit.point + direction * (radius + 0.05f);
            return true;
        }

        if (!damagedTarget && remainingBounces > 0)
        {
            remainingBounces--;

            direction = Vector3.Reflect(direction, hit.normal).normalized;

            if (targetBounce)
                direction = AdjustDirectionTowardEnemy(direction);

            transform.position = hit.point + direction * (radius + 0.05f);
            transform.rotation = Quaternion.LookRotation(direction);
            return true;
        }

        Runner.Despawn(Object);
        return true;
    }

    private bool TryApplyDirectHit(RaycastHit hit)
    {
        int finalDamage = GetFinalDamage();
        PlayerNetwork ownerPlayer = FindOwnerPlayer();

        PlayerHealth playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            bool applied = playerHealth.TakeDamage(finalDamage, ownerPlayer);

            if (applied)
            {
                if (appliesPoison)
                    playerHealth.ApplyPoison(poisonDamagePerTick, poisonDuration, ownerPlayer);

                if (ownerPlayer != null)
                    ownerPlayer.AddHitConfirm();
            }

            return true;
        }

        DummyTargetHealth dummy = hit.collider.GetComponentInParent<DummyTargetHealth>();
        if (dummy != null)
        {
            dummy.TakeDamage(finalDamage);
            return true;
        }

        return false;
    }

    private int GetFinalDamage()
    {
        if (!growDamageByDistance)
            return damage;

        float traveled = Vector3.Distance(spawnPosition, transform.position);
        float t = Mathf.Clamp01(traveled / Mathf.Max(0.01f, growFullDistance));
        float multiplier = Mathf.Lerp(1f, maxGrowDamageMultiplier, t);

        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    private void ApplyExplosionDamage(Vector3 center)
    {
        if (explosionRadius <= 0f || explosionDamageMultiplier <= 0f)
            return;

        Collider[] hits = Physics.OverlapSphere(
            center,
            explosionRadius,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        PlayerNetwork ownerPlayer = FindOwnerPlayer();
        HashSet<NetworkId> damagedPlayers = new HashSet<NetworkId>();
        HashSet<DummyTargetHealth> damagedDummies = new HashSet<DummyTargetHealth>();

        int explosionDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(damage * explosionDamageMultiplier)
        );

        foreach (Collider col in hits)
        {
            PlayerNetwork targetPlayer = col.GetComponentInParent<PlayerNetwork>();

            if (targetPlayer != null && targetPlayer.Object != null)
            {
                NetworkId targetId = targetPlayer.Object.Id;

                if (targetId == ownerId)
                    continue;

                if (damagedPlayers.Contains(targetId))
                    continue;

                damagedPlayers.Add(targetId);

                PlayerHealth health = targetPlayer.Health;
                if (health != null)
                {
                    bool applied = health.TakeDamage(explosionDamage, ownerPlayer);

                    if (applied && ownerPlayer != null)
                        ownerPlayer.AddHitConfirm();
                }

                continue;
            }

            DummyTargetHealth dummy = col.GetComponentInParent<DummyTargetHealth>();
            if (dummy != null && !damagedDummies.Contains(dummy))
            {
                damagedDummies.Add(dummy);
                dummy.TakeDamage(explosionDamage);
            }
        }

        SpawnExplosionVfx(center);
    }

    private void SpawnExplosionVfx(Vector3 position)
    {
        if (!explosionFxPrefab.IsValid)
            return;

        Runner.Spawn(
            explosionFxPrefab,
            position,
            Quaternion.identity,
            Object.InputAuthority,
            (runner, obj) =>
            {
                NetworkTimedVfx vfx = obj.GetComponent<NetworkTimedVfx>();
                if (vfx != null)
                    vfx.Init(runner, 1.5f);
            }
        );
    }

    private void SpawnToxicCloudPlaceholder(Vector3 position)
    {
        if (createsToxicCloud)
            SpawnToxicCloud(position);
    }

    private Vector3 AdjustDirectionTowardEnemy(Vector3 reflectedDirection)
    {
        PlayerNetwork ownerPlayer = FindOwnerPlayer();
        if (ownerPlayer == null)
            return reflectedDirection;

        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);

        PlayerNetwork nearestEnemy = null;
        float nearestSqr = float.MaxValue;

        foreach (PlayerNetwork player in players)
        {
            if (player == null || player == ownerPlayer)
                continue;

            if (player.Object == null)
                continue;

            Vector3 toPlayer = player.transform.position - transform.position;
            float sqr = toPlayer.sqrMagnitude;

            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearestEnemy = player;
            }
        }

        if (nearestEnemy == null)
            return reflectedDirection;

        Vector3 enemyDir = nearestEnemy.transform.position - transform.position;
        enemyDir.y = 0f;

        if (enemyDir.sqrMagnitude < 0.0001f)
            return reflectedDirection;

        enemyDir.Normalize();

        // 완전 유도탄이 아니라 살짝만 보정합니다.
        Vector3 adjusted = Vector3.Slerp(reflectedDirection, enemyDir, 0.35f);
        return adjusted.normalized;
    }

    private void ApplyProjectileSize(float sizeMultiplier)
    {
        float safeSize = Mathf.Max(0.1f, sizeMultiplier);

        radius = baseRadius * safeSize;
        transform.localScale = baseScale * safeSize;

        lastAppliedVisualSize = safeSize;
    }

    private PlayerNetwork FindOwnerPlayer()
    {
        if (ownerId == default)
            return null;

        NetworkObject ownerObject = Runner.FindObject(ownerId);
        if (ownerObject == null)
            return null;

        return ownerObject.GetComponent<PlayerNetwork>();
    }

    private void ApplyProjectileVisual(bool isPoison)
    {
        Color color = isPoison ? poisonProjectileColor : normalProjectileColor;

        if (projectileRenderers != null)
        {
            foreach (Renderer r in projectileRenderers)
            {
                if (r == null)
                    continue;

                r.GetPropertyBlock(visualPropertyBlock);

                // URP Lit 계열
                visualPropertyBlock.SetColor("_BaseColor", color);

                // Standard 또는 일부 커스텀 셰이더
                visualPropertyBlock.SetColor("_Color", color);

                // Emission을 쓰는 총알이면 더 독 탄환처럼 보임
                visualPropertyBlock.SetColor("_EmissionColor", color * (isPoison ? 1.5f : 0f));

                r.SetPropertyBlock(visualPropertyBlock);
            }
        }

        if (trailRenderers != null)
        {
            foreach (TrailRenderer tr in trailRenderers)
            {
                if (tr == null)
                    continue;

                tr.startColor = color;
                tr.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        if (projectileParticles != null)
        {
            foreach (ParticleSystem ps in projectileParticles)
            {
                if (ps == null)
                    continue;

                ParticleSystem.MainModule main = ps.main;
                main.startColor = color;
            }
        }

        lastAppliedPoisonVisual = isPoison;
    }

    private void OnDisable()
    {
        hasInitialized = false;

        direction = Vector3.zero;
        speed = 0f;
        damage = 0;
        ownerId = default;
        lifeTimer = default;
        hasInitialized = false;

        spawnPosition = Vector3.zero;

        remainingBounces = 0;
        remainingPierces = 0;
        targetBounce = false;

        growDamageByDistance = false;
        maxGrowDamageMultiplier = 1f;

        explodesOnImpact = false;
        explosionRadius = 0f;
        explosionDamageMultiplier = 0f;

        appliesPoison = false;
        poisonDamagePerTick = 0;
        poisonDuration = 0f;

        createsToxicCloud = false;
        cloudRadius = 0f;
        cloudDuration = 0f;

        radius = baseRadius;
        transform.localScale = baseScale;
        lastAppliedVisualSize = -1f;

        lastAppliedPoisonVisual = false;
        ClearProjectileVisual();
    }

    private void SpawnToxicCloud(Vector3 position)
    {
        if (!toxicCloudPrefab.IsValid)
            return;

        Vector3 spawnPosition = position + Vector3.up * 0.05f;

        int cloudPoisonDamage = Mathf.Max(1, poisonDamagePerTick);
        float cloudPoisonDuration = poisonDuration > 0f ? poisonDuration : 1.25f;

        Runner.Spawn(
            toxicCloudPrefab,
            spawnPosition,
            Quaternion.identity,
            Object.InputAuthority,
            (runner, obj) =>
            {
                SmokeZone zone = obj.GetComponent<SmokeZone>();
                if (zone != null)
                {
                    zone.Init(
                        runner,
                        cloudRadius,
                        cloudDuration,
                        true,
                        ownerId,
                        cloudPoisonDamage,
                        cloudPoisonDuration
                    );
                }
            }
        );
    }

    private void ClearProjectileVisual()
    {
        if (projectileRenderers != null)
        {
            foreach (Renderer r in projectileRenderers)
            {
                if (r == null)
                    continue;

                r.SetPropertyBlock(null);
            }
        }

        if (trailRenderers != null)
        {
            foreach (TrailRenderer tr in trailRenderers)
            {
                if (tr == null)
                    continue;

                tr.startColor = normalProjectileColor;
                tr.endColor = new Color(
                    normalProjectileColor.r,
                    normalProjectileColor.g,
                    normalProjectileColor.b,
                    0f
                );
            }
        }

        if (projectileParticles != null)
        {
            foreach (ParticleSystem ps in projectileParticles)
            {
                if (ps == null)
                    continue;

                ParticleSystem.MainModule main = ps.main;
                main.startColor = normalProjectileColor;
            }
        }
    }
}