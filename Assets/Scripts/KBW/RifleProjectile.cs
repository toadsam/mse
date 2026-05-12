using Fusion;
using UnityEngine;

public class RifleProjectile : NetworkBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float radius = 0.12f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float lifeSeconds = 3f;

    [Networked] private Vector3 Direction { get; set; }
    [Networked] private float Speed { get; set; }
    [Networked] private int Damage { get; set; }
    [Networked] private NetworkId OwnerId { get; set; }
    [Networked] private TickTimer LifeTimer { get; set; }

    private Vector3 previousPosition;

    public void Init(PlayerNetwork owner, Vector3 direction, float speed, int damage)
    {
        if (!HasStateAuthority)
            return;

        Direction = direction.normalized;
        Speed = speed;
        Damage = damage;
        OwnerId = owner != null && owner.Object != null ? owner.Object.Id : default;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);

        previousPosition = transform.position;
    }

    public override void Spawned()
    {
        previousPosition = transform.position;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (LifeTimer.ExpiredOrNotRunning(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = currentPosition + Direction * Speed * Runner.DeltaTime;

        CheckHit(currentPosition, nextPosition);

        transform.position = nextPosition;

        if (Direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(Direction);
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
        if (hitPlayer != null && hitPlayer.Object != null && hitPlayer.Object.Id == OwnerId)
            return;

        bool hitSomethingDamageable = false;

        PlayerHealth playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            PlayerNetwork ownerPlayer = FindOwnerPlayer();
            bool applied = playerHealth.TakeDamage(Damage, ownerPlayer);

            if (applied && ownerPlayer != null)
                ownerPlayer.AddHitConfirm();

            hitSomethingDamageable = true;
        }
        else
        {
            DummyTargetHealth dummy = hit.collider.GetComponentInParent<DummyTargetHealth>();
            if (dummy != null)
            {
                dummy.TakeDamage(Damage);
                hitSomethingDamageable = true;
            }
        }

        // 플레이어, 더미, 벽, 바닥 등 어떤 물체든 맞으면 총알 제거
        Runner.Despawn(Object);
    }

    private PlayerNetwork FindOwnerPlayer()
    {
        if (OwnerId == default)
            return null;

        NetworkObject ownerObject = Runner.FindObject(OwnerId);
        if (ownerObject == null)
            return null;

        return ownerObject.GetComponent<PlayerNetwork>();
    }
}