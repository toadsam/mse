using UnityEngine;

// Rarity label used to group augment cards.
public enum AugmentRarity
{
    Common,
    Rare,
    Epic
}

// Gameplay category used to organize augment effects.
public enum AugmentCategory
{
    Projectile,
    Status,
    Explosion,
    Movement,
    Defensive,
    RiskReward,
    WeaponStyle
}
// Accessory visual/combat type added by an augment.
public enum AugmentAccessoryType
{
    None,
    OrbitShield,
    OrbitMelee,
    DropMelee
}

// Active item type that can replace the default medkit.
public enum ActiveItemType
{
    MedKit,
    Grenade,
    SmokeBomb,
    ThrowingAxe
}

[CreateAssetMenu(fileName = "AugmentDefinition", menuName = "Game/Augment Definition")]
// Data asset that describes one augment and its gameplay modifiers.
public class AugmentDefinition : ScriptableObject
{
    // Basic card identity shown in the selection UI.
    [Header("Identity")]
    public int id;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;
    public AugmentRarity rarity = AugmentRarity.Common;
    public AugmentCategory category = AugmentCategory.Projectile;

    // Projectile stat modifiers applied when the augment is selected.
    [Header("Projectile Shape")]
    public int extraProjectiles = 0;
    public float spreadAngle = 0f;
    public float projectileSizeMultiplier = 1f;
    public float projectileSpeedMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float fireIntervalMultiplier = 1f;

    // Extra projectile rules such as bounce, pierce, and distance scaling.
    [Header("Projectile Behavior")]
    public int bounceCountBonus = 0;
    public int pierceCountBonus = 0;
    public bool targetBounce = false;
    public bool growDamageByDistance = false;
    public float maxGrowDamageMultiplier = 1f;

    // Status effect values applied by projectiles.
    [Header("Status Effect")]
    public bool appliesPoison = false;
    public int poisonDamagePerTick = 0;
    public float poisonDuration = 0f;

    // Area damage and lingering cloud values.
    [Header("Explosion / Area")]
    public bool explodesOnImpact = false;
    public float explosionRadius = 0f;
    public float explosionDamageMultiplier = 0f;
    public bool createsToxicCloud = false;
    public float cloudDuration = 0f;
    public float cloudRadius = 0f;

    [Header("Special")]
    public float knockbackForce = 0f;
    public float selfDamageOnFire = 0f;
    public float cooldownRefundOnHit = 0f;

    [Header("Stack")]
    public bool stackable = false;
    public int maxStacks = 1;

    // Orbiting accessory values used by shield and melee augments.
    [Header("Accessory / Orbit")]
    public AugmentAccessoryType accessoryType = AugmentAccessoryType.None;
    public int accessoryCountBonus = 0;
    public float accessoryRadius = 1.4f;
    public float accessoryRotateSpeed = 180f;

    public int accessoryDamage = 0;

    public float accessoryHitInterval = 0.6f;

    public float shieldBlockAngle = 70f;

    // Active item replacement settings for item-type augments.
    [Header("Active Item")]
    public bool replacesActiveItem = false;
    public ActiveItemType activeItemType = ActiveItemType.MedKit;
    public int activeItemUsesPerRound = 1;
    public int medKitHealAmount = 10;
}