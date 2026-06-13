using Fusion;
using UnityEngine;

// Despawns a spawned network VFX object after a fixed lifetime.
public class NetworkTimedVfx : NetworkBehaviour
{
    // Network timer that controls when the VFX despawns.
    private TickTimer lifeTimer;
    private bool initialized;

    // Starts the lifetime timer on the state authority.
    public void Init(NetworkRunner runner, float lifetime)
    {
        if (!HasStateAuthority)
            return;

        lifeTimer = TickTimer.CreateFromSeconds(runner, Mathf.Max(0.05f, lifetime));
        initialized = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!initialized)
            return;

        if (lifeTimer.ExpiredOrNotRunning(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    private void OnDisable()
    {
        initialized = false;
        lifeTimer = default;
    }
}