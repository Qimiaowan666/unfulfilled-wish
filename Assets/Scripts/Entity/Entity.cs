using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    public Rigidbody2D Rb          { get; private set; }
    public Animator    Anim        { get; private set; }
    public bool        IsGrounded  { get; protected set; }   // 玩家覆盖 CheckGrounded 设置;敌人不读
    public bool        FacingRight { get; protected set; } = true;
    public int         FacingDir   => FacingRight ? 1 : -1;

    [Tooltip("地面/墙体层。敌人悬崖·墙检测(GroundMask)、玩家落地·冲刺撞墙等共用;留空多数地方会 fallback 到 Ground 命名层")]
    public LayerMask  groundLayer;

    protected virtual void Awake()
    {
        Rb   = GetComponent<Rigidbody2D>();
        Anim = GetComponent<Animator>();
    }

    protected virtual void Update()
    {
        CheckGrounded();
    }

    // 落地检测:默认空(敌人用射线 LedgeAhead/WallAhead,不需要 IsGrounded)。玩家覆盖做 OverlapCircle。
    protected virtual void CheckGrounded() { }

    public void SetFacing(float dir)
    {
        if (dir > 0f && !FacingRight) Flip();
        else if (dir < 0f && FacingRight) Flip();
    }

    // 指向某世界位置的水平方向(-1/0/1)——朝向 + 位移共用，省掉到处写 Mathf.Sign(目标.x - 自身.x)
    public float DirToward(float worldX)    => Mathf.Sign(worldX - transform.position.x);
    public float DirToward(Vector3 worldPos) => DirToward(worldPos.x);

    // 直接转身面向某世界位置/目标
    public void FaceToward(float worldX)     => SetFacing(DirToward(worldX));
    public void FaceToward(Vector3 worldPos) => FaceToward(worldPos.x);

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    protected virtual void OnDrawGizmos() { }
}
