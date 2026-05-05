using UnityEngine;

public enum AugmentType
{
    MoveSpeed,
    DashDistance,
    DashCooldown
}

public enum AugmentRarity
{
    Common,
    Rare,
    Epic
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

    [Header("Effect")]
    public AugmentType augmentType;
    public float value = 1f;

    [Header("Stack")]
    public bool stackable = true;
    public int maxStacks = 99;
}