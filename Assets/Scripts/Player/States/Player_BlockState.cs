using UnityEngine;

public class Player_BlockState : PlayerBaseState
{
    float perfectBlockTimer;
    public bool InPerfectWindow => perfectBlockTimer > 0f;

    public Player_BlockState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isBlocking") { }

    public override void Enter()
    {
        base.Enter();
        perfectBlockTimer = player.perfectBlockWindow;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        if (perfectBlockTimer > 0f) perfectBlockTimer -= Time.deltaTime;

        if (CheckGlobalTransitions()) return;   // 按住右键时再按左键 → 切识破

        if (IsInputBlocked) return;

        if (!input.BlockHeld)
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
    }

    public void ReceiveAttack(float damage)
    {
        if (InPerfectWindow)
        {
            Debug.Log($"[Player] 完美格挡！来袭伤害 {damage}，剩余完美窗口 {perfectBlockTimer:F2}s");
            player.Stats.RedeemGhostHP(player.Stats.perfectBlockHealAmount);
            player.Stats.GainStamina(player.Stats.perfectBlockStaminaGain);
            AudioManager.Instance?.PlayPerfectBlock();
            CameraShake.Shake(0.08f, 0.05f);
            // 精准格挡：冷白蓝火花（系统粒子预制，走 VfxManager 池）
            Vector3 bp = player.transform.position + new Vector3((player.FacingRight ? 1f : -1f) * 0.7f, 0.95f, 0f);
            VfxManager.Play("Vfx/GuardSpark", bp, Quaternion.identity, 0.95f,
                            new Color(0.8f, 0.95f, 1f), player.GetComponentInChildren<SpriteRenderer>());
        }
        else
        {
            Debug.Log($"[Player] 普通格挡。来袭伤害 {damage}，已按住 {(player.perfectBlockWindow - perfectBlockTimer):F2}s");
            player.Stats.OnNormalBlock(damage);
            AudioManager.Instance?.PlayBlock();
            CameraShake.Shake(0.06f, 0.03f);
        }
    }
}
