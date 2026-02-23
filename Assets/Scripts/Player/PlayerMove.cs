using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    [Header("Move")]
    public float walkSpeed = 5f;
    public float runMultiplier = 1.6f;
    public float airControl = 0.6f;

    [Header("Jump/Gravity")]
    public float gravity = -20f;
    public float jumpHeight = 1.25f;

    [Header("Double Jump")]
    public int maxJumps = 2;
    public float doubleJumpHeight = 1.1f;

    [Header("Animation")]
    public Animator animator;

    CharacterController cc;
    float yVel;
    int jumpsUsed;

    // ✅ 외부 속도 배율(ADS/디버프 등)
    float externalSpeedMul = 1f;

    // Animator hashes
    static readonly int AnimSpeed = Animator.StringToHash("Speed");
    static readonly int AnimGrounded = Animator.StringToHash("Grounded");
    static readonly int AnimRunning = Animator.StringToHash("Running");
    static readonly int AnimYVel = Animator.StringToHash("YVel");

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false; // 입력은 로컬만
            return;
        }
    }

    public void SetExternalSpeedMultiplier(float mul)
    {
        externalSpeedMul = Mathf.Clamp(mul, 0.1f, 2f);
    }

    void Update()
    {
        if (!IsOwner) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputMove = (transform.right * h + transform.forward * v);
        if (inputMove.sqrMagnitude > 1f) inputMove.Normalize();

        bool run = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = walkSpeed * (run ? runMultiplier : 1f) * externalSpeedMul;

        // 지면 처리
        if (cc.isGrounded)
        {
            jumpsUsed = 0;
            if (yVel < 0f) yVel = -2f;
        }

        // 점프 입력
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (jumpsUsed < maxJumps)
            {
                bool isFirstJump = (jumpsUsed == 0);
                float hgt = isFirstJump ? jumpHeight : doubleJumpHeight;

                if (yVel < 0f) yVel = 0f;
                yVel = Mathf.Sqrt(hgt * -2f * gravity);
                jumpsUsed++;

                if (animator)
                {
                    if (isFirstJump) animator.SetTrigger("Jump");
                    else animator.SetTrigger("DoubleJump");
                }
            }
        }

        // 중력
        yVel += gravity * Time.deltaTime;

        // 공중 제어
        float control = cc.isGrounded ? 1f : airControl;
        Vector3 planarVel = inputMove * (targetSpeed * control);
        Vector3 vel = planarVel + Vector3.up * yVel;

        cc.Move(vel * Time.deltaTime);

        // 애니 파라미터
        if (animator)
        {
            float speed01 = Mathf.Clamp01(planarVel.magnitude / (walkSpeed * runMultiplier));
            animator.SetFloat(AnimSpeed, speed01, 0.08f, Time.deltaTime);
            animator.SetBool(AnimRunning, run && inputMove.sqrMagnitude > 0.01f);
            animator.SetBool(AnimGrounded, cc.isGrounded);
            animator.SetFloat(AnimYVel, yVel);
        }
    }

    public void SetSpeed(float s) => walkSpeed = s;
}