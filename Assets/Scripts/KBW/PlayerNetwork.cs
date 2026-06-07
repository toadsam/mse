using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;
using System.Collections.Generic;

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

    [Header("Debug Augment Test")]
    [SerializeField] private AugmentDatabase debugAugmentDatabase;
    [SerializeField] private int debugAugmentId = 1;
    [Networked] public NetworkBool IsFiringNet { get; set; }

    [Networked] private TickTimer FireCooldown { get; set; }
    [Networked] public int FireAnimCount { get; set; }

    private int lastAppliedFireAnimCount = -1;

    [Header("Accessory Combat")]
    [SerializeField] private LayerMask accessoryHitMask = ~0;
    [SerializeField] private float orbitMeleeHitRadius = 0.55f;
    [SerializeField] private float accessoryVisualHeight = 1.1f;

    [Networked] private TickTimer OrbitMeleeDamageTimer { get; set; }

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

    [Header("Audio")]
    [SerializeField] private float shootSfxVolume = 1f;
    [SerializeField] private float footstepSfxVolume = 0.7f;
    [SerializeField] private float footstepInterval = 0.42f;
    [SerializeField] private float footstepMoveThreshold = 0.15f;
    [SerializeField] private float jumpSfxVolume = 0.8f;
    [SerializeField] private float dashSfxVolume = 0.85f;

    [Header("Projectile Rifle")]
    [SerializeField] private NetworkPrefabRef rifleProjectilePrefab;
    [SerializeField] private float projectileSpeed = 35f;
    [SerializeField] private float projectileSpawnForwardOffset = 0.4f;

    [Header("Active Item Prefabs")]
    [SerializeField] private NetworkPrefabRef throwableItemPrefab;
    [SerializeField] private NetworkPrefabRef throwingAxePrefab;
    [SerializeField] private NetworkPrefabRef explosionFxPrefab;
    [SerializeField] private NetworkPrefabRef smokeZonePrefab;

    [Header("Augment Runtime Stats")]
    [Networked] public int ProjectileExtraProjectiles { get; private set; }
    [Networked] public float ProjectileSpreadAngle { get; private set; }
    [Networked] public float ProjectileSizeMultiplier { get; private set; }
    [Networked] public float ProjectileSpeedMultiplier { get; private set; }
    [Networked] public float ProjectileDamageMultiplier { get; private set; }
    [Networked] public float FireIntervalMultiplier { get; private set; }

    [Header("Projectile Behavior Runtime")]
    [Networked] public int ProjectileBounceCount { get; private set; }
    [Networked] public int ProjectilePierceCount { get; private set; }
    [Networked] public NetworkBool ProjectileTargetBounce { get; private set; }
    [Networked] public NetworkBool ProjectileGrowDamageByDistance { get; private set; }
    [Networked] public float ProjectileMaxGrowDamageMultiplier { get; private set; }

    [Header("Status / Area Runtime")]
    [Networked] public NetworkBool ProjectileAppliesPoison { get; private set; }
    [Networked] public int ProjectilePoisonDamagePerTick { get; private set; }
    [Networked] public float ProjectilePoisonDuration { get; private set; }

    [Networked] public NetworkBool ProjectileExplodesOnImpact { get; private set; }
    [Networked] public float ProjectileExplosionRadius { get; private set; }
    [Networked] public float ProjectileExplosionDamageMultiplier { get; private set; }

    [Networked] public NetworkBool ProjectileCreatesToxicCloud { get; private set; }
    [Networked] public float ProjectileCloudRadius { get; private set; }
    [Networked] public float ProjectileCloudDuration { get; private set; }

    [Header("Accessory Runtime")]
    [Networked] public int OrbitShieldCount { get; private set; }
    [Networked] public int OrbitMeleeCount { get; private set; }
    [Networked] public int DropMeleeCount { get; private set; }

    [Networked] public float AccessoryRadius { get; private set; }
    [Networked] public float AccessoryRotateSpeed { get; private set; }
    [Networked] public int AccessoryDamage { get; private set; }
    [Networked] public float AccessoryHitInterval { get; private set; }
    [Networked] public float ShieldBlockAngle { get; private set; }

    [Header("Active Item Runtime")]
    [Networked] public ActiveItemType CurrentActiveItem { get; private set; }
    [Networked] public int ActiveItemUsesRemaining { get; private set; }
    [Networked] public int ActiveItemUsesPerRound { get; private set; }
    [Networked] public int MedKitHealAmount { get; private set; }

    [Networked] public NetworkString<_32> PlayerName { get; private set; }

    [Networked] public long BackendUserId { get; private set; } 
    [Networked] public NetworkBool HasAppliedProfile { get; private set; }

    [Networked] private int RoundTeleportSeq { get; set; }
    [Networked] private Vector3 RoundTeleportPosition { get; set; }
    [Networked] private float RoundTeleportYaw { get; set; }

    private int lastAppliedRoundTeleportSeq = -1;


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
    [Networked] public int DashAudioCount { get; set; }
    [Networked] public int MoveState { get; set; }

    [Networked] public int OfferedAugmentId0 { get; private set; }
    [Networked] public int OfferedAugmentId1 { get; private set; }
    [Networked] public int OfferedAugmentId2 { get; private set; }

    [Networked] public int SelectedAugmentId { get; private set; }
    [Networked] public NetworkBool HasSelectedAugmentNet { get; private set; }

    // 罹먮┃???쒖떆 ?대쫫(?꾨줈??RPC濡??몄뒪?몄뿉 ?숆린??. 寃곌낵 ?붾㈃/諛깆뿏????μ뿉 ?ъ슜.
    [Networked] public NetworkString<_32> CharacterDisplayName { get; private set; }

    // 留ㅼ튂 ?숈븞 ?좏깮??augment瑜??꾩쟻 蹂댁〈(SelectedAugmentId??留??쇱슫??珥덇린?붾릺??吏곸쟾 1媛쒕쭔 ?④린 ?뚮Ц).
    // 寃곌낵 ?붾㈃ ?쒖떆 諛?諛깆뿏??MySQL) ??μ뿉 ?ъ슜?쒕떎. (best-of-3 湲곗? 理쒕? ?쇱슫?????ъ쑀 ?덇쾶 8)
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
    private int lastAudioFireAnimCount = -1;
    private int lastAudioJumpAnimCount = -1;
    private int lastAudioDashCount = -1;
    private float nextFootstepTime;

    private int pendingRoundTeleportFrames;
    private const int RoundTeleportApplyFrames = 3;

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
            BackendUserId = 0;
        }
        HasAppliedProfile = false;

        ResetAugmentHistory();

        MoveSpeedBonus = 0f;

        LookYaw = transform.eulerAngles.y;
        LookPitch = 0f;

        IsGroundedNet = false;
        IsDead = false;
        JumpAnimCount = 0;
        DashAudioCount = 0;
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

        ProjectileBounceCount = 0;
        ProjectilePierceCount = 0;
        ProjectileTargetBounce = false;
        ProjectileGrowDamageByDistance = false;
        ProjectileMaxGrowDamageMultiplier = 1f;

        ProjectileAppliesPoison = false;
        ProjectilePoisonDamagePerTick = 0;
        ProjectilePoisonDuration = 0f;

        ProjectileExplodesOnImpact = false;
        ProjectileExplosionRadius = 0f;
        ProjectileExplosionDamageMultiplier = 0f;

        ProjectileCreatesToxicCloud = false;
        ProjectileCloudRadius = 0f;
        ProjectileCloudDuration = 0f;

        OrbitShieldCount = 0;
        OrbitMeleeCount = 0;
        DropMeleeCount = 0;
        AccessoryRadius = 1.4f;
        AccessoryRotateSpeed = 180f;
        AccessoryDamage = 0;
        AccessoryHitInterval = 0.7f;
        ShieldBlockAngle = 75f;
        OrbitMeleeDamageTimer = default;

        CurrentActiveItem = ActiveItemType.MedKit;
        ActiveItemUsesPerRound = 1;
        ActiveItemUsesRemaining = 1;
        MedKitHealAmount = 30;

        RoundTeleportSeq = 0;
        RoundTeleportPosition = transform.position;
        RoundTeleportYaw = transform.eulerAngles.y;
        lastAppliedRoundTeleportSeq = -1;
        pendingRoundTeleportFrames = 0;
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

        lastAppliedRoundTeleportSeq = RoundTeleportSeq;
        lastAppliedJumpAnimCount = JumpAnimCount;
        lastAppliedFireAnimCount = FireAnimCount;
        lastAudioJumpAnimCount = JumpAnimCount;
        lastAudioFireAnimCount = FireAnimCount;
        lastAudioDashCount = DashAudioCount;
        nextFootstepTime = Time.time + Random.Range(0f, footstepInterval);

        if (!HasInputAuthority)
            return;

        GameManager.Instance?.RegisterLocalPlayer(this, playerView);

        Debug.Log($"[PlayerNetwork] Send profile RPC. Name={LocalPlayerProfile.PlayerName}, CharacterId={LocalPlayerProfile.CharacterId}");
        RPC_RequestApplyProfile(
            LocalPlayerProfile.CharacterId,
            LocalPlayerProfile.PlayerName,
            LocalPlayerProfile.CharacterName
        );

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
        ApplyPendingRoundTeleport();

        playerVisuals?.Refresh(CharacterId);
        RefreshAnimatorReference();
        UpdateAnimator();
        UpdateAudioFeedback();
    }

    public override void FixedUpdateNetwork()
    {
        ApplyPendingRoundTeleport();

        if (!GetInput(out GameplayInput input))
            return;

        MatchManager match = MatchManager.Instance;

        if (match != null && match.CurrentPhase != MatchPhase.Playing)
        {
            MoveX = 0f;
            MoveY = 0f;
            MoveAmount = 0f;
            MoveState = 0;
            AirState = 0;
            VerticalSpeedForAnim = 0f;
            IsFiringNet = false;

            if (kcc != null)
                IsGroundedNet = kcc.IsGrounded;

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


        UpdateAccessoryCombat();
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

    private void UpdateAudioFeedback()
    {
        UpdateFireAudio();
        UpdateJumpAudio();
        UpdateDashAudio();
        UpdateFootstepAudio();
    }

    private void UpdateFireAudio()
    {
        if (FireAnimCount < lastAudioFireAnimCount)
        {
            lastAudioFireAnimCount = FireAnimCount;
            return;
        }

        int newShotCount = FireAnimCount - lastAudioFireAnimCount;
        if (newShotCount <= 0)
            return;

        int playCount = Mathf.Min(newShotCount, 3);
        for (int i = 0; i < playCount; i++)
            GameAudio.PlaySfxAt(GameAudioClipId.Shoot, GetFireOriginPosition(), shootSfxVolume, HasInputAuthority ? 0.35f : 0.85f);

        lastAudioFireAnimCount = FireAnimCount;
    }

    private void UpdateJumpAudio()
    {
        if (JumpAnimCount < lastAudioJumpAnimCount)
        {
            lastAudioJumpAnimCount = JumpAnimCount;
            return;
        }

        if (JumpAnimCount == lastAudioJumpAnimCount)
            return;

        GameAudio.PlaySfxAt(GameAudioClipId.Jump, transform.position, jumpSfxVolume, HasInputAuthority ? 0.35f : 0.8f);
        lastAudioJumpAnimCount = JumpAnimCount;
    }

    private void UpdateFootstepAudio()
    {
        if (!ShouldPlayFootstepAudio())
        {
            nextFootstepTime = Time.time + Mathf.Max(0.05f, footstepInterval);
            return;
        }

        if (Time.time < nextFootstepTime)
            return;

        GameAudio.PlaySfxAt(GameAudioClipId.Footstep, transform.position, footstepSfxVolume, HasInputAuthority ? 0.35f : 0.85f);

        float moveMultiplier = Mathf.Lerp(1.15f, 0.75f, Mathf.Clamp01(MoveAmount));
        nextFootstepTime = Time.time + Mathf.Max(0.05f, footstepInterval * moveMultiplier);
    }

    private bool ShouldPlayFootstepAudio()
    {
        if (IsDead || !IsGroundedNet || MoveState == 0 || MoveAmount < footstepMoveThreshold)
            return false;

        MatchManager match = MatchManager.Instance;
        return match == null || match.CurrentPhase == MatchPhase.Playing;
    }

    private void UpdateDashAudio()
    {
        if (DashAudioCount < lastAudioDashCount)
        {
            lastAudioDashCount = DashAudioCount;
            return;
        }

        if (DashAudioCount == lastAudioDashCount)
            return;

        GameAudio.PlaySfxAt(GameAudioClipId.Dash, transform.position, dashSfxVolume, HasInputAuthority ? 0.35f : 0.85f);
        lastAudioDashCount = DashAudioCount;
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
        DashAudioCount++;
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
        if (playerVisuals != null)
        {
            Transform activeMuzzle = playerVisuals.GetActiveMuzzle(CharacterId);
            if (activeMuzzle != null)
                return activeMuzzle.position;
        }

        if (fireOrigin != null)
            return fireOrigin.position;

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
                        projectile.Init(runner,
                                        this,
                                        shotDir,
                                        finalSpeed,
                                        finalDamage,
                                        finalSize,
                                        ProjectileBounceCount,
                                        ProjectilePierceCount,
                                        ProjectileTargetBounce,
                                        ProjectileGrowDamageByDistance,
                                        ProjectileMaxGrowDamageMultiplier,
                                        ProjectileExplodesOnImpact,
                                        ProjectileExplosionRadius,
                                        ProjectileExplosionDamageMultiplier,
                                        ProjectileAppliesPoison,
                                        ProjectilePoisonDamagePerTick,
                                        ProjectilePoisonDuration,
                                        ProjectileCreatesToxicCloud,
                                        ProjectileCloudRadius,
                                        ProjectileCloudDuration
                                    );
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
        if (!HasStateAuthority)
            return;

        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase != MatchPhase.Playing)
            return;

        if (IsDead)
            return;

        if (ActiveItemUsesRemaining <= 0)
            return;

        switch (CurrentActiveItem)
        {
            case ActiveItemType.MedKit:
                UseMedKit();
                break;

            case ActiveItemType.Grenade:
                ThrowGrenade();
                break;

            case ActiveItemType.SmokeBomb:
                ThrowSmokeBomb();
                break;

            case ActiveItemType.ThrowingAxe:
                ThrowAxe();
                break;
        }
    }

    private void UseMedKit()
    {
        if (playerHealth == null)
            return;

        if (playerHealth.Heal(MedKitHealAmount))
            ActiveItemUsesRemaining--;
    }

    private void Reload()
    {
        
    }

    private void HoldAltFire()
    {
        
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

        RoundTeleportPosition = spawnPosition;
        RoundTeleportYaw = yaw;
        RoundTeleportSeq++;

        ApplyRoundTeleport(spawnPosition, yaw);

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
        DashAudioCount = 0;

        if (playerHealth != null)
            playerHealth.ResetHealth();

        DashCooldown = default;
        DashActiveTimer = default;
        DashDirX = 0f;
        DashDirZ = 0f;

        FireCooldown = default;
        FireAnimCount = 0;

        PreviousButtons = default;

        HitConfirmCount = 0;
        ActiveItemUsesRemaining = ActiveItemUsesPerRound;
        OrbitMeleeDamageTimer = default;
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

        ProjectileBounceCount += Mathf.Max(0, def.bounceCountBonus);
        ProjectilePierceCount += Mathf.Max(0, def.pierceCountBonus);

        if (def.targetBounce)
            ProjectileTargetBounce = true;

        if (def.growDamageByDistance)
        {
            ProjectileGrowDamageByDistance = true;
            ProjectileMaxGrowDamageMultiplier = Mathf.Max(
                ProjectileMaxGrowDamageMultiplier,
                def.maxGrowDamageMultiplier
            );
        }

        if (def.appliesPoison)
        {
            ProjectileAppliesPoison = true;
            ProjectilePoisonDamagePerTick = Mathf.Max(ProjectilePoisonDamagePerTick, def.poisonDamagePerTick);
            ProjectilePoisonDuration = Mathf.Max(ProjectilePoisonDuration, def.poisonDuration);
        }

        if (def.explodesOnImpact)
        {
            ProjectileExplodesOnImpact = true;
            ProjectileExplosionRadius = Mathf.Max(ProjectileExplosionRadius, def.explosionRadius);
            ProjectileExplosionDamageMultiplier = Mathf.Max(
                ProjectileExplosionDamageMultiplier,
                def.explosionDamageMultiplier
            );
        }

        if (def.createsToxicCloud)
        {
            ProjectileCreatesToxicCloud = true;
            ProjectileCloudRadius = Mathf.Max(ProjectileCloudRadius, def.cloudRadius);
            ProjectileCloudDuration = Mathf.Max(ProjectileCloudDuration, def.cloudDuration);

            ProjectilePoisonDamagePerTick = Mathf.Max(
                ProjectilePoisonDamagePerTick,
                def.poisonDamagePerTick
            );

            ProjectilePoisonDuration = Mathf.Max(
                ProjectilePoisonDuration,
                def.poisonDuration > 0f ? def.poisonDuration : 1.25f
            );
        }

        switch (def.accessoryType)
        {
            case AugmentAccessoryType.OrbitShield:
                OrbitShieldCount += Mathf.Max(1, def.accessoryCountBonus);
                ShieldBlockAngle = Mathf.Max(ShieldBlockAngle, def.shieldBlockAngle);
                AccessoryRadius = Mathf.Max(AccessoryRadius, def.accessoryRadius);
                AccessoryRotateSpeed = Mathf.Max(AccessoryRotateSpeed, def.accessoryRotateSpeed);
                break;

            case AugmentAccessoryType.OrbitMelee:
                OrbitMeleeCount += Mathf.Max(1, def.accessoryCountBonus);
                AccessoryDamage = Mathf.Max(AccessoryDamage, def.accessoryDamage);
                AccessoryRadius = Mathf.Max(AccessoryRadius, def.accessoryRadius);
                AccessoryRotateSpeed = Mathf.Max(AccessoryRotateSpeed, def.accessoryRotateSpeed);
                AccessoryHitInterval = Mathf.Min(
                    AccessoryHitInterval <= 0f ? def.accessoryHitInterval : AccessoryHitInterval,
                    def.accessoryHitInterval
                );
                break;

            case AugmentAccessoryType.DropMelee:
                DropMeleeCount += Mathf.Max(1, def.accessoryCountBonus);
                AccessoryDamage = Mathf.Max(AccessoryDamage, def.accessoryDamage);
                AccessoryRadius = Mathf.Max(AccessoryRadius, def.accessoryRadius);
                AccessoryHitInterval = Mathf.Min(
                    AccessoryHitInterval <= 0f ? def.accessoryHitInterval : AccessoryHitInterval,
                    def.accessoryHitInterval
                );
                break;
        }

        if (def.replacesActiveItem)
        {
            CurrentActiveItem = def.activeItemType;
            ActiveItemUsesPerRound = Mathf.Max(1, def.activeItemUsesPerRound);
            ActiveItemUsesRemaining = ActiveItemUsesPerRound;
            MedKitHealAmount = Mathf.Max(MedKitHealAmount, def.medKitHealAmount);
        }

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
            yawOffset = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
        }
        else
        {
            float t = count == 1 ? 0.5f : index / (float)(count - 1);
            yawOffset = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t);
        }

        Quaternion yawRotation = Quaternion.AngleAxis(yawOffset, Vector3.up);
        return (yawRotation * centerDirection).normalized;
    }

    private void ThrowGrenade()
    {
        ThrowItem(ThrowableItemKind.Grenade);
    }

    private void ThrowSmokeBomb()
    {
        ThrowItem(ThrowableItemKind.SmokeBomb);
    }

    private void ThrowItem(ThrowableItemKind kind)
    {
        if (!HasStateAuthority)
            return;

        if (!throwableItemPrefab.IsValid)
        {
            Debug.LogWarning("[PlayerNetwork] Throwable item prefab is not assigned.");
            return;
        }

        Vector3 forward = GetAimDirection();
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Quaternion.Euler(0f, LookYaw, 0f) * Vector3.forward;

        forward.Normalize();

        Vector3 spawnPosition =
            GetFireOriginPosition() +
            forward * 0.75f +
            Vector3.up * 0.25f;

        NetworkObject spawned = Runner.Spawn(
            throwableItemPrefab,
            spawnPosition,
            Quaternion.LookRotation(forward),
            Object.InputAuthority,
            (runner, obj) =>
            {
                ThrowableItemProjectile item = obj.GetComponent<ThrowableItemProjectile>();
                if (item != null)
                {
                    item.Init(
                        runner,
                        this,
                        kind,
                        forward,
                        explosionFxPrefab,
                        smokeZonePrefab
                    );
                }
            }
        );

        if (spawned != null)
            ActiveItemUsesRemaining--;
    }

    private void ThrowAxe()
    {
        if (!HasStateAuthority)
            return;

        if (!throwingAxePrefab.IsValid)
        {
            Debug.LogWarning("[PlayerNetwork] Throwing axe prefab is not assigned.");
            return;
        }

        if (ActiveItemUsesRemaining <= 0)
            return;

        Vector3 forward = GetAimDirection();
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Quaternion.Euler(0f, LookYaw, 0f) * Vector3.forward;

        forward.Normalize();

        Vector3 spawnPosition =
            GetFireOriginPosition() +
            forward * 0.75f +
            Vector3.up * 0.25f;

        NetworkObject spawned = Runner.Spawn(
            throwingAxePrefab,
            spawnPosition,
            Quaternion.LookRotation(forward),
            Object.InputAuthority,
            (runner, obj) =>
            {
                ThrowingAxeProjectile axe = obj.GetComponent<ThrowingAxeProjectile>();
                if (axe != null)
                    axe.Init(runner, this, forward);
            }
        );

        if (spawned != null)
        {
            // Throwing Axe는 소모 횟수가 아니라 “현재 손에 있는지”를 나타냅니다.
            ActiveItemUsesRemaining = 0;
        }
    }

    public void RestoreThrowingAxe()
    {
        if (!HasStateAuthority)
            return;

        if (CurrentActiveItem != ActiveItemType.ThrowingAxe)
            return;

        ActiveItemUsesRemaining = 1;
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

    public Vector3 GetOrbitAccessoryPosition(int index, int count, float radius, float height)
    {
        Vector3 dir = GetOrbitAccessoryDirection(index, count);
        Vector3 center = transform.position + Vector3.up * height;

        return center + dir * Mathf.Max(0.1f, radius);
    }

    public Vector3 GetOrbitAccessoryDirection(int index, int count)
    {
        int safeCount = Mathf.Max(1, count);

        float baseAngle = GetAccessoryOrbitAngle();
        float offset = 360f * index / safeCount;

        Quaternion rot = Quaternion.Euler(0f, baseAngle + offset, 0f);
        return rot * Vector3.forward;
    }

    private float GetAccessoryOrbitAngle()
    {
        float time = Runner != null
            ? (float)Runner.SimulationTime
            : Time.time;

        return time * Mathf.Max(1f, AccessoryRotateSpeed);
    }

    public bool TryBlockProjectile(Vector3 projectilePosition, Vector3 projectileDirection)
    {
        if (!HasStateAuthority)
            return false;

        if (OrbitShieldCount <= 0)
            return false;

        if (IsDead || playerHealth == null || playerHealth.IsDead)
            return false;

        Vector3 toProjectile = projectilePosition - transform.position;
        toProjectile.y = 0f;

        if (toProjectile.sqrMagnitude < 0.0001f)
            return false;

        toProjectile.Normalize();

        int shieldCount = Mathf.Max(1, OrbitShieldCount);

        for (int i = 0; i < shieldCount; i++)
        {
            Vector3 shieldDir = GetOrbitAccessoryDirection(i, shieldCount);
            float angle = Vector3.Angle(shieldDir, toProjectile);

            if (angle <= ShieldBlockAngle * 0.5f)
            {
                Debug.Log($"[Shield] Slot {SlotIndex} blocked projectile.");
                return true;
            }
        }

        return false;
    }

    private void UpdateAccessoryCombat()
    {
        if (!HasStateAuthority)
            return;

        if (MatchManager.Instance == null ||
            MatchManager.Instance.CurrentPhase != MatchPhase.Playing)
        {
            return;
        }

        if (IsDead || playerHealth == null || playerHealth.IsDead)
            return;

        UpdateOrbitMeleeDamage();
    }

    private void UpdateOrbitMeleeDamage()
    {
        if (OrbitMeleeCount <= 0)
            return;

        if (AccessoryDamage <= 0)
            return;

        if (!OrbitMeleeDamageTimer.ExpiredOrNotRunning(Runner))
            return;

        OrbitMeleeDamageTimer = TickTimer.CreateFromSeconds(
            Runner,
            Mathf.Max(0.1f, AccessoryHitInterval)
        );

        int meleeCount = Mathf.Max(1, OrbitMeleeCount);
        HashSet<NetworkId> damagedPlayers = new HashSet<NetworkId>();
        HashSet<DummyTargetHealth> damagedDummies = new HashSet<DummyTargetHealth>();

        for (int i = 0; i < meleeCount; i++)
        {
            Vector3 weaponPosition = GetOrbitAccessoryPosition(
                i,
                meleeCount,
                AccessoryRadius,
                accessoryVisualHeight
            );

            Collider[] hits = Physics.OverlapSphere(
                weaponPosition,
                orbitMeleeHitRadius,
                accessoryHitMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (Collider col in hits)
            {
                PlayerNetwork targetPlayer = col.GetComponentInParent<PlayerNetwork>();

                if (targetPlayer != null && targetPlayer.Object != null)
                {
                    if (targetPlayer == this)
                        continue;

                    NetworkId targetId = targetPlayer.Object.Id;

                    if (damagedPlayers.Contains(targetId))
                        continue;

                    damagedPlayers.Add(targetId);

                    PlayerHealth health = targetPlayer.Health;
                    if (health != null)
                    {
                        bool applied = health.TakeDamage(AccessoryDamage, this);

                        if (applied)
                            AddHitConfirm();
                    }

                    continue;
                }

                DummyTargetHealth dummy = col.GetComponentInParent<DummyTargetHealth>();
                if (dummy != null && !damagedDummies.Contains(dummy))
                {
                    damagedDummies.Add(dummy);
                    dummy.TakeDamage(AccessoryDamage);
                }
            }
        }
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

    [ContextMenu("Debug/Apply Debug Augment")]
    private void DebugApplyAugment()
    {
        if (!Application.isPlaying)
            return;

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[Debug] Only StateAuthority can apply augments.");
            return;
        }

        if (debugAugmentDatabase == null)
        {
            MatchManager match = MatchManager.Instance;
            if (match != null)
            {
                AugmentDefinition fromMatch = match.GetAugmentById(debugAugmentId);
                if (fromMatch != null)
                {
                    ApplyAugment(fromMatch);
                    Debug.Log($"[Debug] Applied augment id {debugAugmentId}");
                    return;
                }
            }

            Debug.LogWarning("[Debug] DebugAugmentDatabase is not assigned.");
            return;
        }

        AugmentDefinition def = debugAugmentDatabase.GetById(debugAugmentId);
        if (def == null)
        {
            Debug.LogWarning($"[Debug] Augment id {debugAugmentId} not found.");
            return;
        }

        ApplyAugment(def);
        Debug.Log($"[Debug] Applied augment: {def.displayName}");
    }

    private void ApplyPendingRoundTeleport()
    {
        if (RoundTeleportSeq != lastAppliedRoundTeleportSeq)
        {
            lastAppliedRoundTeleportSeq = RoundTeleportSeq;
            pendingRoundTeleportFrames = RoundTeleportApplyFrames;

            Debug.Log(
                $"[PlayerNetwork] Received Round Teleport Slot {SlotIndex} -> {RoundTeleportPosition} " +
                $"StateAuthority={HasStateAuthority}, InputAuthority={HasInputAuthority}"
            );
        }

        if (pendingRoundTeleportFrames <= 0)
            return;

        pendingRoundTeleportFrames--;

        ApplyRoundTeleport(RoundTeleportPosition, RoundTeleportYaw);

        Debug.Log(
            $"[PlayerNetwork] Applied Round Teleport Slot {SlotIndex} -> {RoundTeleportPosition}, " +
            $"Current={transform.position}, RemainingFrames={pendingRoundTeleportFrames}"
        );
    }

    private void ApplyRoundTeleport(Vector3 spawnPosition, float yaw)
    {
        Quaternion spawnRotation = Quaternion.Euler(0f, yaw, 0f);

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (kcc != null)
        {
            kcc.SetPosition(spawnPosition);
            kcc.SetLookRotation(0f, yaw);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyRoundTeleport(Vector3 spawnPosition, float yaw)
    {
        ApplyRoundTeleport(spawnPosition, yaw);
    }
}
