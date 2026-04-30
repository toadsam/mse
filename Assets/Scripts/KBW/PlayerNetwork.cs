using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SimpleKCC))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerNetwork : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed = 6f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 2.5f;
    [SerializeField] private float dashCooldownSeconds = 1.0f;
    [SerializeField] private float dashSpeed = 28f;

    [Header("Jump / KCC")]
    [SerializeField] private float kccGravity = -25f;
    [SerializeField] private float jumpImpulseStrength = 8f;
    [Networked] public int AirState { get; set; }
    [Networked] public float VerticalSpeedForAnim { get; set; }

    [Networked] private TickTimer DashCooldown { get; set; }
    [Networked] private TickTimer DashActiveTimer { get; set; }
    [Networked] private float DashDirX { get; set; }
    [Networked] private float DashDirZ { get; set; }

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 3f;
    [SerializeField] private float lookDeadzone = 0.01f;
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimator = true;

    private SimpleKCC kcc;
    private Rigidbody rb;
    private PlayerView playerView;
    private PlayerVisuals playerVisuals;

    [Networked] public byte SlotIndex { get; set; }
    [Networked] public byte CharacterId { get; set; }

    [Networked] public float MoveSpeedBonus { get; set; }
    [Networked] public float MoveAmount { get; set; }

    [Networked] public float LookYaw { get; set; }
    [Networked] public float LookPitch { get; set; }

    [Networked] public float MoveX { get; set; }
    [Networked] public float MoveY { get; set; }

    [Networked] public bool IsGroundedNet { get; set; }
    [Networked] public bool IsDead { get; set; }
    [Networked] public int JumpAnimCount { get; set; }
    [Networked] public int MoveState { get; set; }

    [Networked] public int OfferedAugmentId0 { get; private set; }
    [Networked] public int OfferedAugmentId1 { get; private set; }
    [Networked] public int OfferedAugmentId2 { get; private set; }

    [Networked] public int SelectedAugmentId { get; private set; }
    [Networked] public NetworkBool HasSelectedAugmentNet { get; private set; }

    [Networked] public float DashDistanceBonus { get; private set; }
    [Networked] public float DashCooldownMultiplier { get; private set; }

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private int lastAppliedJumpAnimCount = -1;

    private void Awake()
    {
        kcc = GetComponent<SimpleKCC>();
        rb = GetComponent<Rigidbody>();
        playerView = GetComponent<PlayerView>();
        playerVisuals = GetComponent<PlayerVisuals>();

        if (rb != null)
            rb.isKinematic = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    public void ServerInitialize(byte slotIndex)
    {
        if (!HasStateAuthority)
            return;

        AirState = 0;
        VerticalSpeedForAnim = 0f;
        SlotIndex = slotIndex;
        CharacterId = slotIndex;

        MoveSpeedBonus = 0f;

        LookYaw = transform.eulerAngles.y;
        LookPitch = 0f;

        IsGroundedNet = false;
        IsDead = false;
        JumpAnimCount = 0;
        MoveState = 0;

        DashDistanceBonus = 0f;
        DashCooldownMultiplier = 1f;

        SelectedAugmentId = -1;
        HasSelectedAugmentNet = false;

        if (kcc != null)
            kcc.SetLookRotation(LookPitch, LookYaw);
    }

    public override void Spawned()
    {
        if (kcc == null)
            kcc = GetComponent<SimpleKCC>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
            rb.isKinematic = true;

        if (kcc != null)
        {
            kcc.SetGravity(kccGravity);
            kcc.SetLookRotation(LookPitch, LookYaw);
        }

        playerVisuals?.Refresh(CharacterId);

        lastAppliedJumpAnimCount = JumpAnimCount;

        if (!HasInputAuthority)
            return;

        GameManager.Instance?.RegisterLocalPlayer(this, playerView);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (!HasInputAuthority)
            return;

        GameManager.Instance?.UnregisterLocalPlayer(this);
    }

    public override void Render()
    {
        playerVisuals?.Refresh(CharacterId);
        RefreshAnimatorReference();
        UpdateAnimator();
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out GameplayInput input))
            return;

        MatchManager match = MatchManager.Instance;
        if (match != null && match.CurrentPhase == MatchPhase.ChoosingAugment)
        {
            MoveX = 0f;
            MoveY = 0f;
            MoveAmount = 0f;
            MoveState = 0;
            AirState = 0;
            VerticalSpeedForAnim = 0f;
            IsGroundedNet = kcc != null && kcc.IsGrounded;

            PreviousButtons = input.Buttons;
            return;
        }

        if (kcc == null)
            return;

        Vector2 look = input.Look;

        if (Mathf.Abs(look.x) < lookDeadzone) look.x = 0f;
        if (Mathf.Abs(look.y) < lookDeadzone) look.y = 0f;

        float yawDelta = look.x * lookSensitivity;
        float pitchDelta = -look.y * lookSensitivity;

        kcc.AddLookRotation(pitchDelta, yawDelta, minPitch, maxPitch);

        Vector2 lookRotation = kcc.GetLookRotation(true, true);
        LookPitch = lookRotation.x;
        LookYaw = lookRotation.y;

        Vector3 rawMove = new Vector3(input.Move.x, 0f, input.Move.y);

        MoveX = input.Move.x;
        MoveY = input.Move.y;
        MoveAmount = Mathf.Clamp01(rawMove.magnitude);
        MoveState = CalculateMoveState(input.Move);

        if (rawMove.sqrMagnitude > 1f)
            rawMove.Normalize();

        Vector3 moveDir = kcc.TransformRotation * rawMove;

        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Dash))
        {
            if (DashCooldown.ExpiredOrNotRunning(Runner))
            {
                Vector3 dashDir = moveDir.sqrMagnitude > 0.0001f
                    ? moveDir.normalized
                    : kcc.TransformRotation * Vector3.forward;

                StartDash(dashDir);

                float cooldown = dashCooldownSeconds * Mathf.Max(0.05f, DashCooldownMultiplier);
                DashCooldown = TickTimer.CreateFromSeconds(Runner, cooldown);
            }
        }

        Vector3 moveVelocity;

        bool isDashing = !DashActiveTimer.ExpiredOrNotRunning(Runner);
        if (isDashing)
        {
            Vector3 dashDir = new Vector3(DashDirX, 0f, DashDirZ);
            moveVelocity = dashDir * dashSpeed;
        }
        else
        {
            float finalMoveSpeed = baseMoveSpeed + MoveSpeedBonus;
            moveVelocity = moveDir * finalMoveSpeed;
        }

        float yBeforeMove = transform.position.y;

        float jumpImpulse = 0f;
        bool jumpPressedThisTick = input.Buttons.WasPressed(PreviousButtons, EInputButton.Jump);

        if (jumpPressedThisTick && kcc.IsGrounded)
        {
            jumpImpulse = jumpImpulseStrength;
        }

        kcc.Move(moveVelocity, jumpImpulse);

        float yAfterMove = transform.position.y;

        if (Runner.DeltaTime > 0f)
            VerticalSpeedForAnim = (yAfterMove - yBeforeMove) / Runner.DeltaTime;
        else
            VerticalSpeedForAnim = 0f;

        IsGroundedNet = kcc.IsGrounded;

        if (IsGroundedNet)
        {
            AirState = 0;
        }
        else if (jumpPressedThisTick || VerticalSpeedForAnim > 0.05f)
        {
            AirState = 1; // Jump Up
        }
        else
        {
            AirState = 2; // Jump Down
        }

        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Ability))
            UseAbility();

        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Reload))
            Reload();

        if (input.Buttons.IsSet(EInputButton.Fire))
            HoldFire();

        if (input.Buttons.IsSet(EInputButton.AltFire))
            HoldAltFire();

        PreviousButtons = input.Buttons;
    }

    private void UpdateAnimator()
    {
        if (!useAnimator || animator == null)
            return;

        animator.SetFloat("MoveAmount", MoveAmount);
        animator.SetInteger("MoveState", MoveState);
        animator.SetInteger("AirState", AirState);
        animator.SetFloat("VerticalSpeed", VerticalSpeedForAnim);
        animator.SetBool("IsGrounded", IsGroundedNet);
        animator.SetBool("IsDead", IsDead);
    }

    private void RefreshAnimatorReference()
    {
        if (!useAnimator)
            return;

        if (playerVisuals != null)
            animator = playerVisuals.GetActiveAnimator(CharacterId);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void StartDash(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.y = 0f;
        dir.Normalize();

        DashDirX = dir.x;
        DashDirZ = dir.z;

        float totalDashDistance = dashDistance + DashDistanceBonus;
        float effectiveDuration = totalDashDistance / dashSpeed;
        DashActiveTimer = TickTimer.CreateFromSeconds(Runner, effectiveDuration);
    }

    private int CalculateMoveState(Vector2 move)
    {
        const float deadZone = 0.1f;

        if (Mathf.Abs(move.x) < deadZone && Mathf.Abs(move.y) < deadZone)
            return 0;

        if (Mathf.Abs(move.y) >= Mathf.Abs(move.x))
        {
            if (move.y > deadZone) return 1;
            if (move.y < -deadZone) return 2;
        }
        else
        {
            if (move.x > deadZone) return 3;
            if (move.x < -deadZone) return 4;
        }

        return 0;
    }

    private void UseAbility()
    {
        // 나중에 캐릭터별 능력 연결
    }

    private void Reload()
    {
        // 나중에 재장전 연결
    }

    private void HoldFire()
    {
        // 나중에 좌클릭 발사 연결
    }

    private void HoldAltFire()
    {
        // 나중에 우클릭 조준/보조사격 연결
    }

    private void TriggerJumpAnimation()
    {
        JumpAnimCount++;
    }

    public void SetDead(bool dead)
    {
        IsDead = dead;
    }

    public void SetOfferedAugments(int a0, int a1, int a2)
    {
        if (!HasStateAuthority) return;

        OfferedAugmentId0 = a0;
        OfferedAugmentId1 = a1;
        OfferedAugmentId2 = a2;

        SelectedAugmentId = -1;
        HasSelectedAugmentNet = false;
    }

    public int GetOfferedAugmentId(int slotIndex)
    {
        return slotIndex switch
        {
            0 => OfferedAugmentId0,
            1 => OfferedAugmentId1,
            2 => OfferedAugmentId2,
            _ => -1
        };
    }

    public void ApplyAugment(AugmentDefinition def)
    {
        if (!HasStateAuthority || def == null) return;

        switch (def.augmentType)
        {
            case AugmentType.MoveSpeed:
                MoveSpeedBonus += def.value;
                break;

            case AugmentType.DashDistance:
                DashDistanceBonus += def.value;
                break;

            case AugmentType.DashCooldown:
                if (DashCooldownMultiplier <= 0f)
                    DashCooldownMultiplier = 1f;

                DashCooldownMultiplier *= def.value;
                break;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestCharacter(byte requestedCharacterId)
    {
        if (requestedCharacterId > 1)
            return;

        CharacterId = requestedCharacterId;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestSelectAugment(int slotIndex, RpcInfo info = default)
    {
        MatchManager match = MatchManager.Instance;
        if (match == null) return;

        if (match.CurrentPhase != MatchPhase.ChoosingAugment)
            return;

        if (HasSelectedAugmentNet)
            return;

        int augmentId = GetOfferedAugmentId(slotIndex);
        if (augmentId < 0)
            return;

        AugmentDefinition def = match.GetAugmentById(augmentId);
        if (def == null)
            return;

        ApplyAugment(def);

        SelectedAugmentId = augmentId;
        HasSelectedAugmentNet = true;

        match.NotifyPlayerSelectedAugment(this);
    }
}