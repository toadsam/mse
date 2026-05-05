using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerNetwork))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Networked] public int CurrentHealth { get; private set; }
    [Networked] public NetworkBool IsAlive { get; private set; }
    [Networked] public int DamageFeedbackCount { get; private set; }

    public int MaxHealth => maxHealth;
    public bool IsDead => !IsAlive;

    private PlayerNetwork owner;

    private void Awake()
    {
        owner = GetComponent<PlayerNetwork>();
    }

    public override void Spawned()
    {
        if (owner == null)
            owner = GetComponent<PlayerNetwork>();

        if (HasStateAuthority)
            ResetHealth();
    }

    public void ResetHealth()
    {
        if (!HasStateAuthority)
            return;

        CurrentHealth = maxHealth;
        IsAlive = true;
        DamageFeedbackCount = 0;

        if (owner != null)
            owner.SetDead(false);
    }

    public bool TakeDamage(int damage, PlayerNetwork attacker = null)
    {
        if (!HasStateAuthority)
            return false;

        if (!IsAlive)
            return false;

        if (damage <= 0)
            return false;

        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase != MatchPhase.Playing)
            return false;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        DamageFeedbackCount++;

        if (CurrentHealth <= 0)
            Die(attacker);

        return true;
    }

    private void Die(PlayerNetwork attacker)
    {
        if (!HasStateAuthority)
            return;

        if (!IsAlive)
            return;

        IsAlive = false;

        if (owner != null)
            owner.SetDead(true);

        MatchManager.Instance?.ReportPlayerDefeated(owner);
    }

    [ContextMenu("Debug/Take 25 Damage")]
    private void DebugTake25Damage()
    {
        if (HasStateAuthority)
            TakeDamage(25, null);
    }

    [ContextMenu("Debug/Kill Player")]
    private void DebugKillPlayer()
    {
        if (HasStateAuthority)
            TakeDamage(maxHealth, null);
    }
}