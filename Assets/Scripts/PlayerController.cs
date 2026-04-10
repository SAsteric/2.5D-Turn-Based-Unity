using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.8f;

    [Header("Jump")]
    public float jumpForce = 7f;
    public Vector3 groundCheckOrigin = Vector3.zero;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Sprite Renderer")]
    public SpriteRenderer spriteRenderer;

    // ─── Animation Settings Per State ────────────────────────────────────────

    [System.Serializable]
    public class AnimationSettings
    {
        [Tooltip("Frames played per second for this state.")]
        public float fps = 8f;

        [Tooltip("If true, the animation plays once and holds the last frame instead of looping.")]
        public bool playOnce = false;

        [Tooltip("If true, this state's animation continues from wherever the previous state left off instead of resetting to frame 0.")]
        public bool continueFromPreviousFrame = false;

        [Tooltip("Delay in seconds before the animation starts playing after entering this state.")]
        public float startDelay = 0f;

        [Tooltip("Scale the playback speed based on how fast the player is moving. Only meaningful for walk and run states.")]
        public bool scaleWithMoveSpeed = false;

        [Tooltip("The reference speed used when scaleWithMoveSpeed is on. Animation plays at normal FPS at this speed.")]
        public float referenceSpeed = 5f;
    }

    [Header("Idle Animation")]
    public AnimationSettings idleAnim = new AnimationSettings { fps = 8f };

    [Header("Walk Animation")]
    public AnimationSettings walkAnim = new AnimationSettings { fps = 12f, scaleWithMoveSpeed = true, referenceSpeed = 5f };

    [Header("Run Animation")]
    public AnimationSettings runAnim  = new AnimationSettings { fps = 18f, scaleWithMoveSpeed = true, referenceSpeed = 9f };

    [Header("Jump Animation")]
    public AnimationSettings jumpAnim = new AnimationSettings { fps = 8f, playOnce = true };

    [Header("Land Animation")]
    public AnimationSettings landAnim = new AnimationSettings { fps = 12f, playOnce = true };

    // ─── Sprite Arrays ───────────────────────────────────────────────────────
    // Every array has 8 directions × 4 frames = 32 slots.
    //
    // DIRECTION ORDER (by slot block):
    //   Slots  0- 3  →  East       (right)
    //   Slots  4- 7  →  NorthEast  (up-right)
    //   Slots  8-11  →  North      (up / away from camera)
    //   Slots 12-15  →  NorthWest  (up-left)
    //   Slots 16-19  →  West       (left)
    //   Slots 20-23  →  SouthWest  (down-left)
    //   Slots 24-27  →  South      (down / toward camera)  ← default facing
    //   Slots 28-31  →  SouthEast  (down-right)
    //
    // FRAME ORDER within each block of 4:
    //   +0 = Frame 1 (first frame / start of cycle)
    //   +1 = Frame 2
    //   +2 = Frame 3
    //   +3 = Frame 4 (last frame / end of cycle)
    //
    // EXAMPLE — Walk East:
    //   Slot 0 = Walk East Frame 1
    //   Slot 1 = Walk East Frame 2
    //   Slot 2 = Walk East Frame 3
    //   Slot 3 = Walk East Frame 4
    //
    // EXAMPLE — Idle North:
    //   Slot  8 = Idle North Frame 1
    //   Slot  9 = Idle North Frame 2
    //   Slot 10 = Idle North Frame 3
    //   Slot 11 = Idle North Frame 4

    [Header("Idle Sprites  (8 dirs × 4 frames = 32 slots)")]
    [Tooltip("Slots 0-3: East | 4-7: NE | 8-11: North | 12-15: NW | 16-19: West | 20-23: SW | 24-27: South | 28-31: SE")]
    public Sprite[] idleSprites = new Sprite[32];

    [Header("Walk Sprites  (8 dirs × 4 frames = 32 slots)")]
    [Tooltip("Slots 0-3: East | 4-7: NE | 8-11: North | 12-15: NW | 16-19: West | 20-23: SW | 24-27: South | 28-31: SE")]
    public Sprite[] walkSprites = new Sprite[32];

    [Header("Run Sprites   (8 dirs × 4 frames = 32 slots)")]
    [Tooltip("Slots 0-3: East | 4-7: NE | 8-11: North | 12-15: NW | 16-19: West | 20-23: SW | 24-27: South | 28-31: SE")]
    public Sprite[] runSprites  = new Sprite[32];

    [Header("Jump Sprites  (8 dirs × 4 frames = 32 slots)")]
    [Tooltip("Slots 0-3: East | 4-7: NE | 8-11: North | 12-15: NW | 16-19: West | 20-23: SW | 24-27: South | 28-31: SE")]
    public Sprite[] jumpSprites = new Sprite[32];

    [Header("Land Sprites  (8 dirs × 4 frames = 32 slots)")]
    [Tooltip("Slots 0-3: East | 4-7: NE | 8-11: North | 12-15: NW | 16-19: West | 20-23: SW | 24-27: South | 28-31: SE")]
    public Sprite[] landSprites = new Sprite[32];

    // ─── Private ─────────────────────────────────────────────────────────────

    private Rigidbody rb;
    private Camera    mainCam;

    private Vector2 inputDir;
    private Vector3 moveDir;

    private bool isSprinting;
    private bool isGrounded;
    private bool wasGrounded;   // previous frame grounded state — used to detect landing
    private bool jumpRequested;

    private int   lastDirIndex  = 6; // default face south
    private int   currentFrame  = 0;
    private float frameTimer    = 0f;
    private float delayTimer    = 0f;
    private bool  delayDone     = false;
    private bool  playOnceDone  = false;

    private enum PlayerState { Idle, Walk, Run, Jump, Land }
    private PlayerState currentState = PlayerState.Idle;
    private PlayerState previousState;

    private Sprite[]           activeSet;
    private AnimationSettings  activeAnim;

    // ─── Unity ───────────────────────────────────────────────────────────────

    void Awake()
    {
        rb      = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        rb.freezeRotation = true;

        if (mainCam == null)
            Debug.LogError("PlayerController: No Camera tagged 'MainCamera' found.");
        if (spriteRenderer == null)
            Debug.LogError("PlayerController: SpriteRenderer is not assigned.");
    }

    void Update()
    {
        GatherInput();
        UpdateSprite();
    }

    void FixedUpdate()
    {
        CheckGround();
        Move();
        HandleJump();
    }

    // ─── Input ───────────────────────────────────────────────────────────────

    void GatherInput()
    {
        float rawX = Input.GetAxisRaw("Horizontal");
        float rawY = Input.GetAxisRaw("Vertical");

        inputDir    = new Vector2(rawX, rawY).normalized;
        isSprinting = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetButtonDown("Jump"))
            jumpRequested = true;
    }

    // ─── Physics ─────────────────────────────────────────────────────────────

    void CheckGround()
    {
        wasGrounded = isGrounded;
        Vector3 origin = transform.position + groundCheckOrigin;
        isGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundLayer);
    }

    void Move()
    {
        moveDir = new Vector3(inputDir.x, 0f, inputDir.y);

        float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        Vector3 vel = moveDir * speed;
        vel.y = rb.linearVelocity.y;

        rb.linearVelocity = vel;
    }

    void HandleJump()
    {
        if (!jumpRequested) return;
        jumpRequested = false;
        if (!isGrounded) return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // ─── Sprite / Animation ──────────────────────────────────────────────────

    // Returns 0=E 1=NE 2=N 3=NW 4=W 5=SW 6=S 7=SE
    int DirectionIndex(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) % 8;
    }

    PlayerState ResolveState()
    {
        // Landing takes priority — play once then fall through to next state
        if (isGrounded && !wasGrounded)
            return PlayerState.Land;

        // Keep Land playing until its playOnce finishes
        if (currentState == PlayerState.Land && !playOnceDone)
            return PlayerState.Land;

        if (!isGrounded)             return PlayerState.Jump;
        if (inputDir == Vector2.zero) return PlayerState.Idle;
        if (isSprinting)             return PlayerState.Run;
        return PlayerState.Walk;
    }

    void SelectSet(PlayerState state, out Sprite[] set, out AnimationSettings anim)
    {
        switch (state)
        {
            case PlayerState.Walk: set = walkSprites; anim = walkAnim; break;
            case PlayerState.Run:  set = runSprites;  anim = runAnim;  break;
            case PlayerState.Jump: set = jumpSprites; anim = jumpAnim; break;
            case PlayerState.Land: set = landSprites; anim = landAnim; break;
            default:               set = idleSprites; anim = idleAnim; break;
        }
    }

    void UpdateSprite()
    {
        if (spriteRenderer == null) return;

        // ── 1. Direction ─────────────────────────────────────────────────────
        if (inputDir != Vector2.zero)
            lastDirIndex = DirectionIndex(inputDir);

        // ── 2. State resolution ───────────────────────────────────────────────
        previousState = currentState;
        currentState  = ResolveState();

        bool stateChanged = currentState != previousState;

        // ── 3. Swap animation set on state change ─────────────────────────────
        if (stateChanged)
        {
            SelectSet(currentState, out activeSet, out activeAnim);

            playOnceDone = false;
            delayDone    = activeAnim.startDelay <= 0f;
            delayTimer   = 0f;

            if (!activeAnim.continueFromPreviousFrame)
            {
                currentFrame = 0;
                frameTimer   = 0f;
            }
        }

        // ── 4. Start delay ────────────────────────────────────────────────────
        if (!delayDone)
        {
            delayTimer += Time.deltaTime;
            if (delayTimer >= activeAnim.startDelay)
                delayDone = true;
            else
                return; // hold frame 0 during delay
        }

        // ── 5. Compute effective FPS ──────────────────────────────────────────
        float effectiveFPS = activeAnim.fps;

        if (activeAnim.scaleWithMoveSpeed && activeAnim.referenceSpeed > 0f)
        {
            float currentSpeed = moveDir.magnitude * moveSpeed * (isSprinting ? sprintMultiplier : 1f);
            effectiveFPS = activeAnim.fps * (currentSpeed / activeAnim.referenceSpeed);
            effectiveFPS = Mathf.Max(effectiveFPS, 1f); // never go below 1 FPS
        }

        // ── 6. Advance frame ──────────────────────────────────────────────────
        if (!playOnceDone)
        {
            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(effectiveFPS, 0.01f);

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;

                if (activeAnim.playOnce && currentFrame >= 3)
                {
                    // Hold last frame
                    playOnceDone = true;
                }
                else
                {
                    currentFrame = (currentFrame + 1) % 4;
                }
            }
        }

        // ── 7. Assign sprite ──────────────────────────────────────────────────
        int index = lastDirIndex * 4 + currentFrame;

        if (activeSet != null && index < activeSet.Length && activeSet[index] != null)
            spriteRenderer.sprite = activeSet[index];
    }

    // ─── Debug ───────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + groundCheckOrigin, groundCheckRadius);
    }
}