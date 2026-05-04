using Fusion;
using UnityEngine;

public enum EInputButton
{
    Fire = 0,
    AltFire = 1,
    Dash = 2,
    Jump = 3,
    Ability = 4,
    Reload = 5,
    ConfirmAugment1 = 6,
    ConfirmAugment2 = 7,
    ConfirmAugment3 = 8,
}

public struct GameplayInput : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public NetworkButtons Buttons;
}