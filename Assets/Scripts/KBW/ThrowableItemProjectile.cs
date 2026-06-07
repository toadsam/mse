using System.Collections.Generic;
using Fusion;
using UnityEngine;

public enum ThrowableItemKind : byte
{
    Grenade = 0,
    SmokeBomb = 1
}

public class ThrowableItemProjectile : NetworkBehaviour
{
    [Header("Collision")]
    [SerializeField] private float radius = 0.18f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private LayerMask damageMask = ~0;

    [Header("Movement")]
    [SerializeField] private float horizontalSpeed = 13f;
    [SerializeField] private float upwardSpeed = 6f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float fuseSeconds = 1.5f;

    [Header("Grenade")]
    [SerializeField] private int grenadeDamage = 45;
    [SerializeField] private float grenadeRadius = 3.0f;

    [Header("Smoke")]
    [SerializeField] private float smokeRadius = 3.5f;
    [SerializeField] private float smokeDuration = 5.0f;

    [Header("Visual")]
    [SerializeField] private GameObject grenadeVisualRoot;
    [SerializeField] private GameObject smokeVisualRoot;

    [Networked] private ThrowableItemKind KindNet { get; set; }

    private Vector3 velocity;
    private NetworkId ownerId;
    private TickTimer fuseTimer;
    private bool hasInitialized;

    private NetworkPrefabRef explosionFxPrefab;
    private NetworkPrefabRef smokeZonePrefab;

    public void Init(
        NetworkRunner runner,
        PlayerNetwork owner,
        ThrowableItemKind kind,
        Vector3 horizontalDirection,
        NetworkPrefabRef explosionFxPrefab,
        NetworkPrefabRef smokeZonePrefab
    )
    {
        if (!HasStateAuthority)
            return;

        KindNet = kind;

        ownerId = owner != null && owner.Object != null
            ? owner.Object.Id
            : default;

        Vector3 dir = horizontalDirection;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        velocity = dir * horizontalSpeed;
        velocity.y = upwardSpeed;

        fuseTimer = TickTimer.CreateFromSeconds(runner, fuseSeconds);

        this.explosionFxPrefab = explosionFxPrefab;
        this.smokeZonePrefab = smokeZonePrefab;

        hasInitialized = true;

        ApplyVisual();
    }

    public override void Spawned()
    {
        ApplyVisual();
    }

    public override void Render()
    {
        ApplyVisual();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!hasInitialized)
            return;

        if (fuseTimer.ExpiredOrNotRunning(Runner))
        {
            ExecuteImpact(transform.position);
            return;
        }

        Vector3 currentPosition = transform.position;

        velocity.y += gravity * Runner.DeltaTime;
        Vector3 nextPosition = currentPosition + velocity * Runner.DeltaTime;

        if (CheckCollision(currentPosition, nextPosition, out Vector3 hitPoint))
        {
            ExecuteImpact(hitPoint);
            return;
        }

        transform.position = nextPosition;

        if (velocity.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
    }

    private bool CheckCollision(Vector3 from, Vector3 to, out Vector3 hitPoint)
    {
        hitPoint = to;

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
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

        // 던진 직후 자신의 콜라이더를 치는 경우 방지
        if (hitPlayer != null &&
            hitPlayer.Object != null &&
            hitPlayer.Object.Id == ownerId)
        {
            return false;
        }

        hitPoint = hit.point;
        return true;
    }

    private void ExecuteImpact(Vector3 center)
    {
        switch (KindNet)
        {
            case ThrowableItemKind.Grenade:
                ApplyExplosionDamage(center);
                SpawnNetworkVfx(explosionFxPrefab, center, 1.5f);
                break;

            case ThrowableItemKind.SmokeBomb:
                SpawnSmokeZone(center);
                break;
        }

        Runner.Despawn(Object);
    }

    private void ApplyExplosionDamage(Vector3 center)
    {
        Collider[] hits = Physics.OverlapSphere(
            center,
            grenadeRadius,
            damageMask,
            QueryTriggerInteraction.Ignore
        );

        PlayerNetwork ownerPlayer = FindOwnerPlayer();

        HashSet<NetworkId> damagedPlayers = new HashSet<NetworkId>();
        HashSet<DummyTargetHealth> damagedDummies = new HashSet<DummyTargetHealth>();

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
                    bool applied = health.TakeDamage(grenadeDamage, ownerPlayer);

                    if (applied && ownerPlayer != null)
                        ownerPlayer.AddHitConfirm();
                }

                continue;
            }

            DummyTargetHealth dummy = col.GetComponentInParent<DummyTargetHealth>();
            if (dummy != null && !damagedDummies.Contains(dummy))
            {
                damagedDummies.Add(dummy);
                dummy.TakeDamage(grenadeDamage);
            }
        }
    }

    private void SpawnNetworkVfx(NetworkPrefabRef prefab, Vector3 position, float lifetime)
    {
        if (!prefab.IsValid)
            return;

        Runner.Spawn(
            prefab,
            position,
            Quaternion.identity,
            Object.InputAuthority,
            (runner, obj) =>
            {
                NetworkTimedVfx vfx = obj.GetComponent<NetworkTimedVfx>();
                if (vfx != null)
                    vfx.Init(runner, lifetime);
            }
        );
    }

    private void SpawnSmokeZone(Vector3 position)
    {
        if (!smokeZonePrefab.IsValid)
            return;

        Vector3 spawnPosition = position + Vector3.up * 0.05f;

        Runner.Spawn(
            smokeZonePrefab,
            spawnPosition,
            Quaternion.identity,
            Object.InputAuthority,
            (runner, obj) =>
            {
                SmokeZone zone = obj.GetComponent<SmokeZone>();
                if (zone != null)
                    zone.Init(runner, smokeRadius, smokeDuration);
            }
        );
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

    private void ApplyVisual()
    {
        bool isGrenade = KindNet == ThrowableItemKind.Grenade;
        bool isSmoke = KindNet == ThrowableItemKind.SmokeBomb;

        if (grenadeVisualRoot != null)
            grenadeVisualRoot.SetActive(isGrenade);

        if (smokeVisualRoot != null)
            smokeVisualRoot.SetActive(isSmoke);
    }

    private void OnDisable()
    {
        velocity = Vector3.zero;
        ownerId = default;
        fuseTimer = default;
        hasInitialized = false;

        explosionFxPrefab = default;
        smokeZonePrefab = default;
    }
}