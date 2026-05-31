using Fusion;
using UnityEngine;

public class RifleProjectile : NetworkBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float radius = 0.12f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float lifeSeconds = 3f;

    private Vector3 direction;
    private float speed;
    private int damage;
    private NetworkId ownerId;
    private TickTimer lifeTimer;

    private bool hasInitialized;

    public void Init(
    NetworkRunner runner,
    PlayerNetwork owner,
    Vector3 projectileDirection,
    float projectileSpeed,
    int projectileDamage,
    float projectileSizeMultiplier = 1f
)
    {
        direction = projectileDirection.sqrMagnitude > 0.0001f
            ? projectileDirection.normalized
            : transform.forward;

        speed = projectileSpeed;
        damage = projectileDamage;
        ownerId = owner != null && owner.Object != null ? owner.Object.Id : default;
        lifeTimer = TickTimer.CreateFromSeconds(runner, lifeSeconds);

        float safeSize = Mathf.Max(0.1f, projectileSizeMultiplier);
        radius *= safeSize;
        transform.localScale *= safeSize;

        hasInitialized = true;
    }

    public override void Spawned()
    {
        // OnBeforeSpawned 콜백에서 Init이 이미 호출되는 구조이므로
        // 여기서는 따로 Networked 값을 읽거나 쓰지 않습니다.
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

        CheckHit(currentPosition, nextPosition);

        transform.position = nextPosition;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void CheckHit(Vector3 from, Vector3 to)
    {
        Vector3 move = to - from;
        float distance = move.magnitude;

        if (distance <= 0.0001f)
            return;

        Vector3 dir = move / distance;

        if (!Physics.SphereCast(from, radius, dir, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
            return;

        PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

        // 발사자 자신은 무시
        if (hitPlayer != null && hitPlayer.Object != null && hitPlayer.Object.Id == ownerId)
            return;

        PlayerHealth playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            PlayerNetwork ownerPlayer = FindOwnerPlayer();
            bool applied = playerHealth.TakeDamage(damage, ownerPlayer);

            if (applied && ownerPlayer != null)
                ownerPlayer.AddHitConfirm();
        }
        else
        {
            DummyTargetHealth dummy = hit.collider.GetComponentInParent<DummyTargetHealth>();
            if (dummy != null)
                dummy.TakeDamage(damage);
        }

        Runner.Despawn(Object);
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
}