using UnityEngine;

public enum AugmentRarity
{
    Common,
    Rare,
    Epic
}

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

[CreateAssetMenu(fileName = "AugmentDefinition", menuName = "Game/Augment Definition")]
public class AugmentDefinition : ScriptableObject
{
    [Header("Identity")]
    public int id;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;
    public AugmentRarity rarity = AugmentRarity.Common;
    public AugmentCategory category = AugmentCategory.Projectile;

    [Header("Projectile Shape")]
    public int extraProjectiles = 0;
    public float spreadAngle = 0f;
    public float projectileSizeMultiplier = 1f;
    public float projectileSpeedMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float fireIntervalMultiplier = 1f;

    [Header("Projectile Behavior")]
    public int bounceCountBonus = 0;
    public int pierceCountBonus = 0;
    public bool targetBounce = false;
    public bool growDamageByDistance = false;
    public float maxGrowDamageMultiplier = 1f;

    [Header("Status Effect")]
    public bool appliesPoison = false;
    public int poisonDamagePerTick = 0;
    public float poisonDuration = 0f;
    public bool appliesSlow = false;
    public float slowMultiplier = 1f;
    public float slowDuration = 0f;

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
}