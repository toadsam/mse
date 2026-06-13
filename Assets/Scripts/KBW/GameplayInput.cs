using Fusion;
using UnityEngine;

// Network button indices used by Fusion input.
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

// Input payload sent from the local client to the state authority.
public struct GameplayInput : INetworkInput
{
    // Movement, look, aim ray, and button state for one network tick.
    public Vector2 Move;
    public Vector2 Look;
    public Vector3 AimOrigin;
    public Vector3 AimDirection;
    public NetworkButtons Buttons;
}