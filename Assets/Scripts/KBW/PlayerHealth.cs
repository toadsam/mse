using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerNetwork))]
// Networked health component that handles damage, death, healing, and poison ticks.
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    // Maximum HP restored at the start of each round.
    [SerializeField] private int maxHealth = 100;

    [Header("Poison")]
    [SerializeField] private float poisonTickInterval = 1.0f;

    // Networked health state replicated to HUD and match logic.
    [Networked] public int CurrentHealth { get; private set; }
    [Networked] public NetworkBool IsAlive { get; private set; }
    [Networked] public int DamageFeedbackCount { get; private set; }

    // Networked poison state controlled by the state authority.
    [Networked] private int PoisonDamagePerTick { get; set; }
    [Networked] private TickTimer PoisonEndTimer { get; set; }
    [Networked] private TickTimer PoisonTickTimer { get; set; }
    [Networked] private NetworkId PoisonAttackerId { get; set; }

    [Networked] public NetworkBool IsPoisonedNet { get; private set; }

    public int MaxHealth => maxHealth;
    public bool IsDead => !IsAlive;
    public bool IsPoisoned => !PoisonEndTimer.ExpiredOrNotRunning(Runner);

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

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        UpdatePoison();
    }

    // Restores HP and clears all status effects for a new round.
    public void ResetHealth()
    {
        if (!HasStateAuthority)
            return;

        CurrentHealth = maxHealth;
        IsAlive = true;
        DamageFeedbackCount = 0;

        PoisonDamagePerTick = 0;
        PoisonEndTimer = default;
        PoisonTickTimer = default;
        PoisonAttackerId = default;
        IsPoisonedNet = false;

        if (owner != null)
            owner.SetDead(false);
    }

    // Applies damage only during the Playing phase and reports death when HP reaches zero.
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

    // Restores HP up to the maximum value.
    public bool Heal(int amount)
    {
        if (!HasStateAuthority)
            return false;

        if (!IsAlive)
            return false;

        if (amount <= 0)
            return false;

        int before = CurrentHealth;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);

        return CurrentHealth > before;
    }

    // Starts or refreshes poison damage over time from an attacker.
    public void ApplyPoison(int damagePerTick, float duration, PlayerNetwork attacker = null)
    {
        if (!HasStateAuthority)
            return;

        if (!IsAlive)
            return;

        if (damagePerTick <= 0 || duration <= 0f)
            return;

        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase != MatchPhase.Playing)
            return;

        bool wasAlreadyPoisoned = !PoisonEndTimer.ExpiredOrNotRunning(Runner);

        PoisonDamagePerTick = Mathf.Max(PoisonDamagePerTick, damagePerTick);
        PoisonEndTimer = TickTimer.CreateFromSeconds(Runner, duration);

        // Do not keep pushing the tick timer while poison is already active.
        // This keeps toxic cloud damage ticking at the intended interval.
        if (!wasAlreadyPoisoned || PoisonTickTimer.ExpiredOrNotRunning(Runner))
            PoisonTickTimer = TickTimer.CreateFromSeconds(Runner, poisonTickInterval);

        PoisonAttackerId = attacker != null && attacker.Object != null
            ? attacker.Object.Id
            : default;

        IsPoisonedNet = true;
    }

    // Applies poison ticks and clears poison when its duration ends.
    private void UpdatePoison()
    {
        if (!IsAlive)
            return;

        if (PoisonEndTimer.ExpiredOrNotRunning(Runner))
        {
            PoisonDamagePerTick = 0;
            PoisonTickTimer = default;
            PoisonAttackerId = default;
            IsPoisonedNet = false;
            return;
        }

        if (!PoisonTickTimer.ExpiredOrNotRunning(Runner))
            return;

        PoisonTickTimer = TickTimer.CreateFromSeconds(Runner, poisonTickInterval);

        PlayerNetwork attacker = FindPoisonAttacker();
        bool applied = TakeDamage(PoisonDamagePerTick, attacker);

        if (applied && attacker != null)
            attacker.AddHitConfirm();
    }

    private PlayerNetwork FindPoisonAttacker()
    {
        if (PoisonAttackerId == default)
            return null;

        NetworkObject attackerObject = Runner.FindObject(PoisonAttackerId);
        if (attackerObject == null)
            return null;

        return attackerObject.GetComponent<PlayerNetwork>();
    }

    // Marks the player dead and notifies MatchManager about the defeat.
    private void Die(PlayerNetwork attacker)
    {
        if (!HasStateAuthority)
            return;

        if (!IsAlive)
            return;

        IsAlive = false;

        PoisonDamagePerTick = 0;
        PoisonEndTimer = default;
        PoisonTickTimer = default;
        PoisonAttackerId = default;
        IsPoisonedNet = false;

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