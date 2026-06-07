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
public enum AugmentAccessoryType
{
    None,
    OrbitShield,
    OrbitMelee,
    DropMelee
}

public enum ActiveItemType
{
    MedKit,
    Grenade,
    SmokeBomb,
    ThrowingAxe
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

    [Header("Accessory / Orbit")]
    public AugmentAccessoryType accessoryType = AugmentAccessoryType.None;
    public int accessoryCountBonus = 0;
    public float accessoryRadius = 1.4f;
    public float accessoryRotateSpeed = 180f;

    [Tooltip("OrbitMelee 또는 DropMelee 피해량")]
    public int accessoryDamage = 0;

    [Tooltip("OrbitMelee가 같은 대상에게 다시 피해를 줄 수 있는 간격")]
    public float accessoryHitInterval = 0.6f;

    [Tooltip("Shield가 탄을 막는 각도입니다. 70이면 방패 중심 기준 좌우 35도 정도입니다.")]
    public float shieldBlockAngle = 70f;

    [Header("Active Item")]
    public bool replacesActiveItem = false;
    public ActiveItemType activeItemType = ActiveItemType.MedKit;
    public int activeItemUsesPerRound = 1;
    public int medKitHealAmount = 10;
}