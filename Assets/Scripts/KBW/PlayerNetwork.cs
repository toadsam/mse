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
    [Networked] private TickTimer DashCooldown { get; set; }

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

    [Networked] public bool IsGroundedNet { get; set; }
    [Networked] public bool IsDead { get; set; }
    [Networked] public int JumpAnimCount { get; set; }

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private int lastAppliedJumpAnimCount = -1;

    /*private static readonly int MoveAmountHash = Animator.StringToHash("MoveAmount");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int JumpHash = Animator.StringToHash("Jump");*/

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

        LookYaw = transform.eulerAngles.y;
        LookPitch = 0f;

        IsDead = false;
        JumpAnimCount = 0;
    }

    public override void Spawned()
    {
        playerVisuals?.Refresh(CharacterId);

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
        UpdateAnimator();
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out GameplayInput input))
            return;

        Vector2 look = input.Look;

        if (Mathf.Abs(look.x) < lookDeadzone) look.x = 0f;
        if (Mathf.Abs(look.y) < lookDeadzone) look.y = 0f;

        LookYaw += look.x * lookSensitivity;
        LookPitch -= look.y * lookSensitivity;
        LookPitch = Mathf.Clamp(LookPitch, minPitch, maxPitch);

        // FPS/TPS 스타일: 몸체 회전은 마우스 yaw만 따라감
        transform.rotation = Quaternion.Euler(0f, LookYaw, 0f);

        Vector3 rawMove = new Vector3(input.Move.x, 0f, input.Move.y);
        MoveAmount = rawMove.magnitude;

        if (rawMove.sqrMagnitude > 1f)
            rawMove.Normalize();

        Vector3 moveDir = Quaternion.Euler(0f, LookYaw, 0f) * rawMove;
        float finalMoveSpeed = baseMoveSpeed + MoveSpeedBonus;
        cc.Move(moveDir * finalMoveSpeed * Runner.DeltaTime);

        IsGroundedNet = characterController != null && characterController.isGrounded;

        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Dash))
        {
            if (DashCooldown.ExpiredOrNotRunning(Runner))
            {
                Vector3 dashDir = moveDir.sqrMagnitude > 0.0001f
                    ? moveDir.normalized
                    : transform.forward;

                DoDash(dashDir);
                DashCooldown = TickTimer.CreateFromSeconds(Runner, dashCooldownSeconds);
            }
        }

        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Ability))
        {
            UseAbility();
        }

        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Reload))
        {
            Reload();
        }

        if (input.Buttons.IsSet(EInputButton.Fire))
        {
            HoldFire();
        }

        if (input.Buttons.IsSet(EInputButton.AltFire))
        {
            HoldAltFire();
        }

        PreviousButtons = input.Buttons;
    }

    private void UpdateAnimator()
    {
        if (!useAnimator || animator == null)
            return;

        animator.SetFloat("MoveAmount", MoveAmount);
        animator.SetBool("isGrounded", IsGroundedNet);
        animator.SetBool("isDead", IsDead);

        if (JumpAnimCount != lastAppliedJumpAnimCount)
        {
            lastAppliedJumpAnimCount = JumpAnimCount;
            animator.SetTrigger("Jump");
        }
    }

    private void DoDash(Vector3 dir)
    {
        cc.Move(dir * dashDistance);
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestCharacter(byte requestedCharacterId)
    {
        if (requestedCharacterId > 1)
            return;

        CharacterId = requestedCharacterId;
    }
}