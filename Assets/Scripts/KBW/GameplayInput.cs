using Fusion;
using UnityEngine;

public enum EInputButton
{
    Fire = 0,
    AltFire = 1,
    Dash = 2,
    Ability = 3,
    Reload = 4,
    ConfirmAugment1 = 5,
    ConfirmAugment2 = 6,
    ConfirmAugment3 = 7,
}

public struct GameplayInput : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public NetworkButtons Buttons;
}