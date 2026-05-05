using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerNetwork : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed = 6f;
    [SerializeField] private float dashDistance = 2.5f;
    [SerializeField] private float dashCooldownSeconds = 1.0f;
    [SerializeField] private float dashSpeed = 28f;
    [SerializeField] private float dashDuration = 0.10f;
    [Networked] private TickTimer DashCooldown { get; set; }
    [Networked] private TickTimer DashActiveTimer { get; set; }
    [Networked] private float DashDirX { get; set; }
    [Networked] private float DashDirZ { get; set; }
    [Header("Jump")]
    [SerializeField] private float groundedSnapVelocity = -1f;
    [SerializeField] private float jumpHeight = 2.0f;
    [SerializeField] private float gravity = 15f;

    [Networked] private float VerticalVelocity { get; set; }

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 3f;
    [SerializeField] private float lookDeadzone = 0.01f;
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimator = true;

    private NetworkCharacterController cc;
    private CharacterController characterController;
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
    [Networked] public float DashCooldownMultiplier { get; private set; } // 기본 1.0

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private int lastAppliedJumpAnimCount = -1;

    private void Awake()
    {
        cc = GetComponent<NetworkCharacterController>();
        characterController = GetComponent<CharacterController>();
        playerView = GetComponent<PlayerView>();
        playerVisuals = GetComponent<PlayerVisuals>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void ServerInitialize(byte slotIndex)
    {
        if (!HasStateAuthority)
            return;

        SlotIndex = slotIndex;
        CharacterId = slotIndex;
        MoveSpeedBonus = 0f;
        VerticalVelocity = groundedSnapVelocity;

        LookYaw = transform.eulerAngles.y;
        LookPitch = 0f;

        IsDead = false;
        JumpAnimCount = 0;

        DashDistanceBonus = 0f;
        DashCooldownMultiplier = 1f;
        SelectedAugmentId = -1;
        HasSelectedAugmentNet = false;
    }

    public override void Spawned()
    {
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

            VerticalVelocity = 0f;
            IsGroundedNet = characterController != null && characterController.isGrounded;

            PreviousButtons = input.Buttons;
            return;
        }


        Vector2 look = input.Look;

        if (Mathf.Abs(look.x) < lookDeadzone) look.x = 0f;
        if (Mathf.Abs(look.y) < lookDeadzone) look.y = 0f;

        LookYaw += look.x * lookSensitivity;
        LookPitch -= look.y * lookSensitivity;
        LookPitch = Mathf.Clamp(LookPitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, LookYaw, 0f);

        Vector3 rawMove = new Vector3(input.Move.x, 0f, input.Move.y);

        MoveX = input.Move.x;
        MoveY = input.Move.y;
        MoveAmount = Mathf.Clamp01(rawMove.magnitude);
        MoveState = CalculateMoveState(input.Move);

        if (rawMove.sqrMagnitude > 1f)
            rawMove.Normalize();

        Vector3 moveDir = Quaternion.Euler(0f, LookYaw, 0f) * rawMove;

        bool wasGrounded = characterController != null && characterController.isGrounded;

        if (wasGrounded)
        {
            if (VerticalVelocity < groundedSnapVelocity)
                VerticalVelocity = groundedSnapVelocity;

            if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Jump))
            {
                VerticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                // TriggerJumpAnimation(); // 일단 잠깐 비활성화
            }
        }
        else
        {
            VerticalVelocity -= gravity * Runner.DeltaTime;
        }

        // 대시 입력 처리
        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Dash))
        {
            if (DashCooldown.ExpiredOrNotRunning(Runner))
            {
                Vector3 dashDir = moveDir.sqrMagnitude > 0.0001f
                    ? moveDir.normalized
                    : transform.forward;

                StartDash(dashDir);

                float cooldown = dashCooldownSeconds * Mathf.Max(0.05f, DashCooldownMultiplier);
                DashCooldown = TickTimer.CreateFromSeconds(Runner, cooldown);
            }
        }



        bool isDashing = !DashActiveTimer.ExpiredOrNotRunning(Runner);

        Vector3 finalMove;
        if (isDashing)
        {
            Vector3 dashDir = new Vector3(DashDirX, 0f, DashDirZ);
            finalMove = dashDir * dashSpeed;
        }
        else
        {
            float finalMoveSpeed = baseMoveSpeed + MoveSpeedBonus;
            finalMove = moveDir * finalMoveSpeed;
        }

        finalMove.y = VerticalVelocity;

        cc.Move(finalMove * Runner.DeltaTime);

        IsGroundedNet = characterController != null && characterController.isGrounded;

        if (IsGroundedNet && VerticalVelocity < groundedSnapVelocity)
            VerticalVelocity = groundedSnapVelocity;

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
        animator.SetBool("IsGrounded", IsGroundedNet);
        animator.SetBool("IsDead", IsDead);

        /*if (JumpAnimCount != lastAppliedJumpAnimCount)
        {
            lastAppliedJumpAnimCount = JumpAnimCount;
            animator.SetTrigger("Jump");
        }*/
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
            return 0; // Idle

        if (Mathf.Abs(move.y) >= Mathf.Abs(move.x))
        {
            if (move.y > deadZone) return 1;   // Forward
            if (move.y < -deadZone) return 2;  // Backward
        }
        else
        {
            if (move.x > deadZone) return 3;   // Right
            if (move.x < -deadZone) return 4;  // Left
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

    // 점프 입력을 나중에 추가하면 이 메서드를 호출
    private void TriggerJumpAnimation()
    {
        JumpAnimCount++;
    }

    // 나중에 체력 시스템에서 사망 시 호출
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
                // 예: 0.8f 면 20% 감소
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