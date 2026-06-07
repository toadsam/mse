using Fusion;
using UnityEngine;

public class NetworkTimedVfx : NetworkBehaviour
{
    private TickTimer lifeTimer;
    private bool initialized;

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