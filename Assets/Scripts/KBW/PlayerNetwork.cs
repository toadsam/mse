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

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 3f;
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 75f;

    private NetworkCharacterController cc;
    private PlayerView playerView;
    private PlayerVisuals playerVisuals;

    [Networked] public byte SlotIndex { get; set; }
    [Networked] public byte CharacterId { get; set; }
    [Networked] public float MoveSpeedBonus { get; set; }

    [Networked] public float LookYaw { get; set; }
    [Networked] public float LookPitch { get; set; }

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private void Awake()
    {
        cc = GetComponent<NetworkCharacterController>();
        playerView = GetComponent<PlayerView>();
        playerVisuals = GetComponent<PlayerVisuals>();
    }

    public void ServerInitialize(byte slotIndex)
    {
        if (!HasStateAuthority)
            return;

        SlotIndex = slotIndex;

        // 테스트용: 0번은 캐릭터 A, 1번은 캐릭터 B
        CharacterId = slotIndex;
        MoveSpeedBonus = 0f;

        // 시작 방향
        LookYaw = transform.eulerAngles.y;
        LookPitch = 0f;
    }

    public override void Spawned()
    {
        playerVisuals?.Refresh(CharacterId);

        if (!HasInputAuthority)
            return;

        LocalCamera cam = Camera.main != null
            ? Camera.main.GetComponent<LocalCamera>()
            : null;

        if (cam != null && playerView != null)
            cam.Bind(playerView, this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (!HasInputAuthority)
            return;

        LocalCamera cam = Camera.main != null
            ? Camera.main.GetComponent<LocalCamera>()
            : null;

        if (cam != null)
            cam.Unbind();
    }

    public override void Render()
    {
        playerVisuals?.Refresh(CharacterId);
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out GameplayInput input))
            return;

        // 각 플레이어 입력으로만 회전 갱신
        LookYaw += input.Look.x * lookSensitivity;
        LookPitch -= input.Look.y * lookSensitivity;
        LookPitch = Mathf.Clamp(LookPitch, minPitch, maxPitch);

        // 플레이어 몸체는 yaw만 회전
        transform.rotation = Quaternion.Euler(0f, LookYaw, 0f);

        // 이동
        Vector3 rawMove = new Vector3(input.Move.x, 0f, input.Move.y);
        if (rawMove.sqrMagnitude > 1f)
            rawMove.Normalize();

        Vector3 moveDir = Quaternion.Euler(0f, LookYaw, 0f) * rawMove;
        float finalMoveSpeed = baseMoveSpeed + MoveSpeedBonus;
        cc.Move(moveDir * finalMoveSpeed * Runner.DeltaTime);

        // 버튼 이벤트
        if (input.Buttons.WasPressed(PreviousButtons, EInputButton.Dash))
        {
            Vector3 dashDir = moveDir.sqrMagnitude > 0.0001f
                ? moveDir.normalized
                : transform.forward;

            DoDash(dashDir);
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

    private void DoDash(Vector3 dir)
    {
        cc.Move(dir * dashDistance);
    }

    private void UseAbility()
    {
        // 나중에 캐릭터별 능력 연결
        // Debug.Log($"{name} UseAbility");
    }

    private void Reload()
    {
        // 나중에 재장전 연결
        // Debug.Log($"{name} Reload");
    }

    private void HoldFire()
    {
        // 나중에 좌클릭 발사 연결
    }

    private void HoldAltFire()
    {
        // 나중에 우클릭 조준/보조사격 연결
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestCharacter(byte requestedCharacterId)
    {
        if (requestedCharacterId > 1)
            return;

        CharacterId = requestedCharacterId;
    }
}