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

    [Header("Projectile Rifle")]
    [SerializeField] private NetworkPrefabRef rifleProjectilePrefab;
    [SerializeField] private float projectileSpeed = 35f;
    [SerializeField] private float projectileSpawnForwardOffset = 0.4f;

    [Header("Augment Runtime Stats")]
    [Networked] public int ProjectileExtraProjectiles { get; private set; }
    [Networked] public float ProjectileSpreadAngle { get; private set; }
    [Networked] public float ProjectileSizeMultiplier { get; private set; }
    [Networked] public float ProjectileSpeedMultiplier { get; private set; }
    [Networked] public float ProjectileDamageMultiplier { get; private set; }
    [Networked] public float FireIntervalMultiplier { get; private set; }

    [Networked] public NetworkString<_32> PlayerName { get; private set; }
    [Networked] public NetworkBool HasAppliedProfile { get; private set; }

    // 백엔드(MySQL) 매치 저장을 위해, 각 클라이언트가 자기 로그인 userId를 호스트(StateAuthority)로 동기화한다.
    [Networked] public long BackendUserId { get; private set; }

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

    // 캐릭터 표시 이름(프로필 RPC로 호스트에 동기화). 결과 화면/백엔드 저장에 사용.
    [Networked] public NetworkString<_32> CharacterDisplayName { get; private set; }

    // 매치 동안 선택한 augment를 누적 보존(SelectedAugmentId는 매 라운드 초기화되어 직전 1개만 남기 때문).
    // 결과 화면 표시 및 백엔드(MySQL) 저장에 사용한다. (best-of-3 기준 최대 라운드 수 여유 있게 8)
    public const int MaxAugmentHistory = 8;
    [Networked, Capacity(MaxAugmentHistory)] public NetworkArray<int> AugmentHistoryIds { get; }
    [Networked, Capacity(MaxAugmentHistory)] public NetworkArray<int> AugmentHistoryRounds { get; }
    [Networked] public int AugmentHistoryCount { get; private set; }

    [Networked] public float DashDistanceBonus { get; private set; }
    [Networked] public float DashCooldownMultiplier { get; private set; }

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [Networked] public int HitConfirmCount { get; private set; }

    [Networked] public NetworkBool CanSelectAugmentNet { get; private set; }

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

        if (fireOrigin == null)
        {
            Debug.LogWarning($"[PlayerNetwork] fireOrigin is not assigned on {name}. Projectile will use fallback muzzle position.");
        }
    }

    public void ServerInitialize(byte slotIndex)
    {
        if (!HasStateAuthority)
            return;

        AirState = 0;
        VerticalSpeedForAnim = 0f;
        SlotIndex = slotIndex;

        if (!HasAppliedProfile)
        {
            CharacterId = 0;
            PlayerName = $"Player {slotIndex + 1}";
            CharacterDisplayName = $"Character {slotIndex + 1}";
        }
        HasAppliedProfile = false;

        ResetAugmentHistory();

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

        HitConfirmCount = 0;

        ProjectileExtraProjectiles = 0;
        ProjectileSpreadAngle = 0f;
        ProjectileSizeMultiplier = 1f;
        ProjectileSpeedMultiplier = 1f;
        ProjectileDamageMultiplier = 1f;
        FireIntervalMultiplier = 1f;
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

        Debug.Log($"[PlayerNetwork] Send profile RPC. Name={LocalPlayerProfile.PlayerName}, CharacterId={LocalPlayerProfile.CharacterId}");

        RPC_RequestApplyProfile(
            LocalPlayerProfile.CharacterId,
            LocalPlayerProfile.PlayerName,
            LocalPlayerProfile.CharacterName
        );

        // 로그인 상태면 내 backend userId를 호스트로 전달(게스트/비로그인은 0 → 호스트가 저장을 스킵).
        long localBackendUserId = BackendSession.IsLoggedIn ? BackendSession.UserId : 0;
        RPC_SetBackendIdentity(localBackendUserId);
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

    [SerializeField] private Vector3 fallbackMuzzleLocalOffset = new Vector3(0.25f, 1.35f, 0.65f);

    private Vector3 GetFireOriginPosition()
    {
        // 1����: ���� Ȱ�� ĳ������ Muzzle
        if (playerVisuals != null)
        {
            Transform activeMuzzle = playerVisuals.GetActiveMuzzle(CharacterId);
            if (activeMuzzle != null)
                return activeMuzzle.position;
        }

        // 2����: ���� fireOrigin
        if (fireOrigin != null)
            return fireOrigin.position;

        // 3����: ī�޶� ��Ŀ�� �ƴ϶� �÷��̾� ��Ʈ ���� fallback
        Quaternion yawRotation = Quaternion.Euler(0f, LookYaw, 0f);
        return transform.position + yawRotation * fallbackMuzzleLocalOffset;
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

        float finalFireInterval = rifleFireInterval * Mathf.Max(0.05f, FireIntervalMultiplier);
        FireCooldown = TickTimer.CreateFromSeconds(Runner, finalFireInterval);
        FireAnimCount++;

        GetFireRay(inputAimOrigin, inputAimDirection, out Vector3 aimOrigin, out Vector3 aimDirection);

        Vector3 targetPoint = aimOrigin + aimDirection * rifleRange;

        RaycastHit[] aimHits = Physics.RaycastAll(
            aimOrigin,
            aimDirection,
            rifleRange,
            rifleHitMask,
            QueryTriggerInteraction.Ignore
        );

        if (aimHits != null && aimHits.Length > 0)
        {
            System.Array.Sort(aimHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in aimHits)
            {
                PlayerNetwork hitPlayer = hit.collider.GetComponentInParent<PlayerNetwork>();

                if (hitPlayer == this)
                    continue;

                targetPoint = hit.point;
                break;
            }
        }

        Vector3 spawnPos = GetFireOriginPosition();
        Vector3 projectileDir = (targetPoint - spawnPos).normalized;

        if (projectileDir.sqrMagnitude < 0.0001f)
            projectileDir = GetAimDirection();

        spawnPos += projectileDir * projectileSpawnForwardOffset;

        Debug.DrawLine(transform.position, spawnPos, Color.cyan, 1.0f);
        Debug.DrawRay(spawnPos, projectileDir * 5f, Color.yellow, 1.0f);

        Transform activeMuzzle = playerVisuals != null ? playerVisuals.GetActiveMuzzle(CharacterId) : null;

        int projectileCount = Mathf.Max(1, 1 + ProjectileExtraProjectiles);
        float spreadAngle = Mathf.Max(0f, ProjectileSpreadAngle);

        int finalDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(rifleDamage * Mathf.Max(0.05f, ProjectileDamageMultiplier))
        );

        float finalSpeed = projectileSpeed * Mathf.Max(0.05f, ProjectileSpeedMultiplier);
        float finalSize = Mathf.Max(0.1f, ProjectileSizeMultiplier);

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 shotDir = GetSpreadProjectileDirection(projectileDir, i, projectileCount, spreadAngle);
            Vector3 shotSpawnPos = GetFireOriginPosition() + shotDir * projectileSpawnForwardOffset;

            Debug.DrawRay(shotSpawnPos, shotDir * 5f, Color.yellow, 1.0f);

            Runner.Spawn(
                rifleProjectilePrefab,
                shotSpawnPos,
                Quaternion.LookRotation(shotDir),
                Object.InputAuthority,
                (runner, obj) =>
                {
                    RifleProjectile projectile = obj.GetComponent<RifleProjectile>();
                    if (projectile != null)
                        projectile.Init(runner, this, shotDir, finalSpeed, finalDamage, finalSize);
                }
            );
        }
    }

    public void AddHitConfirm()
    {
        if (!HasStateAuthority)
            return;

        HitConfirmCount++;
    }

    private void UseAbility()
    {
        // ���߿� ĳ���ͺ� �ɷ� ����
    }

    private void Reload()
    {
        // ���߿� ������ ����
    }

    private void HoldAltFire()
    {
        // ���߿� ��Ŭ�� ����/������� ����
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

    public void SetOfferedAugments(int a0, int a1, int a2, bool canSelect)
    {
        if (!HasStateAuthority) return;

        OfferedAugmentId0 = a0;
        OfferedAugmentId1 = a1;
        OfferedAugmentId2 = a2;

        SelectedAugmentId = -1;
        CanSelectAugmentNet = canSelect;
        HasSelectedAugmentNet = !canSelect;
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
        if (!HasStateAuthority || def == null)
            return;

        ProjectileExtraProjectiles += Mathf.Max(0, def.extraProjectiles);

        if (def.spreadAngle > 0f)
            ProjectileSpreadAngle = Mathf.Max(ProjectileSpreadAngle, def.spreadAngle);

        ProjectileSizeMultiplier *= Mathf.Max(0.05f, def.projectileSizeMultiplier);
        ProjectileSpeedMultiplier *= Mathf.Max(0.05f, def.projectileSpeedMultiplier);
        ProjectileDamageMultiplier *= Mathf.Max(0.05f, def.damageMultiplier);
        FireIntervalMultiplier *= Mathf.Max(0.05f, def.fireIntervalMultiplier);

        Debug.Log($"[Augment] Slot {SlotIndex} applied {def.displayName}");
    }

    private Vector3 GetSpreadProjectileDirection(Vector3 centerDirection, int index, int count, float spreadAngle)
    {
        if (centerDirection.sqrMagnitude < 0.0001f)
            centerDirection = transform.forward;

        centerDirection.Normalize();

        if (spreadAngle <= 0f)
            return centerDirection;

        float yawOffset;

        if (count <= 1)
        {
            // Rapid Barreló�� �ܹ��ε� ������ �ִ� ���
            yawOffset = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
        }
        else
        {
            // Multi Shotó�� ���� ���̸� �յ� �л�
            float t = count == 1 ? 0.5f : index / (float)(count - 1);
            yawOffset = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t);
        }

        Quaternion yawRotation = Quaternion.AngleAxis(yawOffset, Vector3.up);
        return (yawRotation * centerDirection).normalized;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestApplyProfile(byte requestedCharacterId, string requestedPlayerName, string requestedCharacterName)
    {
        if (playerVisuals != null && !playerVisuals.IsValidCharacterId(requestedCharacterId))
            requestedCharacterId = 0;

        Debug.Log($"[PlayerNetwork] Apply profile requested. RequestedCharacterId={requestedCharacterId}, Name={requestedPlayerName}, Character={requestedCharacterName}");

        if (playerVisuals != null && !playerVisuals.IsValidCharacterId(requestedCharacterId))
        {
            Debug.LogWarning($"[PlayerNetwork] Invalid CharacterId {requestedCharacterId}. Fallback to CharacterA. Check PlayerVisuals Characters array.");
            requestedCharacterId = 0;
        }

        string safeName = (requestedPlayerName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(safeName))
            safeName = $"Player {SlotIndex + 1}";

        if (safeName.Length > LocalPlayerProfile.MaxNameLength)
            safeName = safeName.Substring(0, LocalPlayerProfile.MaxNameLength);

        string safeCharacterName = (requestedCharacterName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(safeCharacterName))
            safeCharacterName = $"Character {requestedCharacterId + 1}";
        if (safeCharacterName.Length > 31)
            safeCharacterName = safeCharacterName.Substring(0, 31);

        CharacterId = requestedCharacterId;
        PlayerName = safeName;
        CharacterDisplayName = safeCharacterName;
        HasAppliedProfile = true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetBackendIdentity(long backendUserId)
    {
        BackendUserId = backendUserId < 0 ? 0 : backendUserId;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestCharacter(byte requestedCharacterId)
    {
        if (playerVisuals != null && !playerVisuals.IsValidCharacterId(requestedCharacterId))
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

        RecordSelectedAugment(augmentId, match.RoundIndex);

        match.NotifyPlayerSelectedAugment(this);
    }

    // 호스트에서만 호출. 이번 매치에서 선택한 augment를 누적 기록한다.
    private void RecordSelectedAugment(int augmentId, int roundIndex)
    {
        if (!HasStateAuthority)
            return;

        if (AugmentHistoryCount >= MaxAugmentHistory)
            return;

        AugmentHistoryIds.Set(AugmentHistoryCount, augmentId);
        AugmentHistoryRounds.Set(AugmentHistoryCount, roundIndex);
        AugmentHistoryCount++;
    }

    // 매치 시작 시 호스트가 누적 기록을 초기화한다.
    public void ResetAugmentHistory()
    {
        if (!HasStateAuthority)
            return;

        for (int i = 0; i < MaxAugmentHistory; i++)
        {
            AugmentHistoryIds.Set(i, -1);
            AugmentHistoryRounds.Set(i, -1);
        }

        AugmentHistoryCount = 0;
    }

    public int GetSelectedAugmentId(int index)
    {
        if (index < 0 || index >= AugmentHistoryCount)
            return -1;

        return AugmentHistoryIds.Get(index);
    }

    public int GetSelectedAugmentRound(int index)
    {
        if (index < 0 || index >= AugmentHistoryCount)
            return -1;

        return AugmentHistoryRounds.Get(index);
    }
}