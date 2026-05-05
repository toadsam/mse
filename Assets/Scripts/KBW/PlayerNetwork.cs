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

    [Header("Rifle")]
    [SerializeField] private int rifleDamage = 20;
    [SerializeField] private float rifleRange = 80f;
    [SerializeField] private float rifleFireInterval = 0.18f;
    [SerializeField] private LayerMask rifleHitMask = ~0;
    [SerializeField] private Transform fireOrigin;
    [SerializeField] private bool drawFireDebugRay = true;
    [Networked] public NetworkBool IsFiringNet { get; set; }

    [Networked] private TickTimer FireCooldown { get; set; }
    [Networked] public int FireAnimCount { get; set; }

    private int lastAppliedFireAnimCount = -1;

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
    private PlayerHealth playerHealth;
    public PlayerHealth Health => playerHealth;

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

    [Networked] public int HitConfirmCount { get; private set; }

    private int lastAppliedJumpAnimCount = -1;

    private void Awake()
    {
        kcc = GetComponent<SimpleKCC>();
        rb = GetComponent<Rigidbody>();
        playerView = GetComponent<PlayerView>();
        playerVisuals = GetComponent<PlayerVisuals>();
        playerHealth = GetComponent<PlayerHealth>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (fireOrigin == null && playerView != null)
        {
            if (playerView.FirstPersonAnchor != null)
                fireOrigin = playerView.FirstPersonAnchor;
            else
                fireOrigin = transform;
        }
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

        FireCooldown = default;
        FireAnimCount = 0;

        DashDistanceBonus = 0f;
        DashCooldownMultiplier = 1f;

        SelectedAugmentId = -1;
        HasSelectedAugmentNet = false;

        if (kcc != null)
            kcc.SetLookRotation(LookPitch, LookYaw);

        FireCooldown = default;
        FireAnimCount = 0;
        HitConfirmCount = 0;
    }

    public override void Spawned()
    {
        if (kcc == null)
            kcc = GetComponent<SimpleKCC>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (rb != null)
            rb.isKinematic = true;

        if (kcc != null)
        {
            kcc.SetGravity(kccGravity);
            kcc.SetLookRotation(LookPitch, LookYaw);
        }

        playerVisuals?.Refresh(CharacterId);

        lastAppliedJumpAnimCount = JumpAnimCount;
        lastAppliedFireAnimCount = FireAnimCount;

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
            TriggerJumpAnimation();
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

        bool canFireState = MatchManager.Instance != null && MatchManager.Instance.CurrentPhase == MatchPhase.Playing && !IsDead;

        IsFiringNet = canFireState && input.Buttons.IsSet(EInputButton.Fire);

        if (IsFiringNet)
            HoldFire(input.AimOrigin, input.AimDirection);

        if (input.Buttons.IsSet(EInputButton.AltFire))
            HoldAltFire();

        PreviousButtons = input.Buttons;
    }

    private void GetFireRay(Vector3 inputAimOrigin, Vector3 inputAimDirection, out Vector3 origin, out Vector3 direction)
    {
        if (inputAimDirection.sqrMagnitude > 0.0001f)
        {
            origin = inputAimOrigin;
            direction = inputAimDirection.normalized;
            return;
        }

        // fallback
        origin = GetFireOriginPosition();
        direction = GetAimDirection();
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

        animator.SetBool("IsMoving", MoveState != 0);
        animator.SetBool("IsFiring", IsFiringNet);

        if (JumpAnimCount != lastAppliedJumpAnimCount)
        {
            lastAppliedJumpAnimCount = JumpAnimCount;
            animator.SetTrigger("Jump");
        }
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
    private Vector3 GetFireOriginPosition()
    {
        if (fireOrigin != null)
            return fireOrigin.position;

        if (playerView != null && playerView.FirstPersonAnchor != null)
            return playerView.FirstPersonAnchor.position;

        return transform.position + Vector3.up * 1.6f;
    }
    private Vector3 GetAimDirection()
    {
        return Quaternion.Euler(LookPitch, LookYaw, 0f) * Vector3.forward;
    }

    private void HoldFire(Vector3 inputAimOrigin, Vector3 inputAimDirection)
    {
        if (!HasStateAuthority)
            return;

        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase != MatchPhase.Playing)
            return;

        if (IsDead)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (!FireCooldown.ExpiredOrNotRunning(Runner))
            return;

        FireCooldown = TickTimer.CreateFromSeconds(Runner, rifleFireInterval);

        FireAnimCount++;

        GetFireRay(inputAimOrigin, inputAimDirection, out Vector3 origin, out Vector3 direction);

        if (drawFireDebugRay)
            Debug.DrawRay(origin, direction * rifleRange, Color.red, 0.2f);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            rifleRange,
            rifleHitMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

            // 자기 자신의 KCCCollider는 무시
            if (hitPlayer == this)
                continue;

            PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();

            if (hitHealth != null)
            {
                bool damageApplied = hitHealth.TakeDamage(rifleDamage, this);

                if (damageApplied)
                    HitConfirmCount++;

                break;
            }

            // 플레이어가 아닌 첫 번째 물체를 맞으면 총알은 거기서 막힘
            break;
        }
    }

    private void UseAbility()
    {
        // 나중에 캐릭터별 능력 연결
    }

    private void Reload()
    {
        // 나중에 재장전 연결
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

    public void ResetForRound(Vector3 spawnPosition, float yaw)
    {
        if (!HasStateAuthority)
            return;

        if (kcc != null)
        {
            kcc.SetPosition(spawnPosition);
            kcc.SetLookRotation(0f, yaw);
        }
        else
        {
            transform.position = spawnPosition;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        LookPitch = 0f;
        LookYaw = yaw;

        MoveX = 0f;
        MoveY = 0f;
        MoveAmount = 0f;
        MoveState = 0;

        AirState = 0;
        VerticalSpeedForAnim = 0f;
        IsGroundedNet = false;

        IsDead = false;
        JumpAnimCount = 0;

        if (playerHealth != null)
            playerHealth.ResetHealth();

        DashCooldown = default;
        DashActiveTimer = default;
        DashDirX = 0f;
        DashDirZ = 0f;
        FireCooldown = default;
        FireAnimCount = 0;

        PreviousButtons = default;

        FireCooldown = default;
        FireAnimCount = 0;
        HitConfirmCount = 0;
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