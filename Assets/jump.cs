using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    // ---------- 输入模式 ----------
    public enum InputMode { Keyboard, Joystick }

    [Header("输入设置")]
    public InputMode inputMode = InputMode.Keyboard;

    [Header("跳跃参数")]
    public float jumpForce = 10f;
    public int maxJumpCount = 4;

    [Header("物理减速度")]
    public float gravityScale = 1f;
    public float airDrag = 0.5f;

    [Header("初始状态")]
    public bool startWithFullEnergy = true;
    public int initialJumpCount = 2;
    public bool startWithShield = true;
    public float invincibleDuration = 5f;

    [Header("缓降参数")]
    public float slowFallSpeed = 2f;
    public float horizontalDamping = 5f;

    [Header("碰撞惩罚")]
    public string obstacleTag = "Obstacle";

    [Header("无敌与护盾")]
    public SpriteRenderer shieldSpriteRenderer;
    public Sprite invincibleSprite;
    public Sprite shieldSprite;

    [Header("无敌闪烁")]
    public float blinkBeforeEnd = 2f;
    public float blinkInterval = 0.15f;
    private float blinkTimer = 0f;

    [Header("临时无敌（护盾破碎后）")]
    public float tempInvincibleDuration = 1f;
    public float tempBlinkInterval = 0.15f;

    [Header("无能量UI反馈")]
    public Image vignetteImage;
    public Image grayOverlayImage;
    public float feedbackFadeSpeed = 5f;
    private float feedbackTargetAlpha = 0f;

    [Header("胜利条件")]
    public GameObject winLineObject;
    public float winLineYOffset = 5f;

    [Header("底部死亡线")]
    public GameObject bottomLineObject;
    public float bottomLineDistance = 3f;
    public float bottomLineFollowSpeed = 5f;

    [Header("跳跃统计")]
    public int totalJumps = 0;

    [Header("重开设置")]
    public float restartHoldDuration = 1.5f;

    // ---------- 独立音效 ----------
    [Header("独立音效")]
    public AudioSource bgmSource;
    public float bgmStartTime = 0f;
    public float bgmDelay = 0f;

    public AudioSource jumpAudioSource;
    public float jumpStartTime = 0f;
    public float jumpDelay = 0f;

    public AudioSource gameOverAudioSource;
    public float gameOverStartTime = 0f;
    public float gameOverDelay = 0f;

    public AudioSource winAudioSource;
    public float winStartTime = 0f;
    public float winDelay = 0f;

    public AudioSource shieldAppearAudioSource;
    public float shieldAppearStartTime = 0f;
    public float shieldAppearDelay = 0f;

    public AudioSource shieldBreakAudioSource;
    public float shieldBreakStartTime = 0f;
    public float shieldBreakDelay = 0f;

    [Header("当前状态（只读）")]
    [SerializeField] private int currentJumpCount = 0;
    [SerializeField] private bool hasShield = false;

    private Rigidbody2D rb;
    private bool isGameOver = false;
    private bool hasWon = false;
    private float gameStartTime;
    private bool isInvincible = false;
    private bool isHoldingSpace = false;
    private bool isBottomLineInitialized = false;

    private bool isGameEnded = false;
    private float spaceHoldTimer = 0f;
    private float startY;

    private Spawner spawner;

    // ---------- 临时无敌 ----------
    private bool isTempInvincible = false;
    private float tempInvincibleTimer = 0f;

    // ---------- 输入辅助 ----------
    bool IsJumpKeyDown() => inputMode == InputMode.Keyboard ? Input.GetKeyDown(KeyCode.Space) : Input.GetKeyDown(KeyCode.JoystickButton1);
    bool IsJumpKeyHeld() => inputMode == InputMode.Keyboard ? Input.GetKey(KeyCode.Space) : Input.GetKey(KeyCode.JoystickButton1);
    bool IsJumpKeyUp() => inputMode == InputMode.Keyboard ? Input.GetKeyUp(KeyCode.Space) : Input.GetKeyUp(KeyCode.JoystickButton1);

    // ---------- MonoBehaviour ----------
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("❌ 缺少 Rigidbody2D");

        rb.constraints = RigidbodyConstraints2D.FreezePositionX;
        rb.gravityScale = gravityScale;

        startY = transform.position.y;

        if (startWithFullEnergy)
            currentJumpCount = maxJumpCount;
        else
            currentJumpCount = Mathf.Clamp(initialJumpCount, 0, maxJumpCount);

        hasShield = startWithShield;
        gameStartTime = Time.time;
        isInvincible = true;
        UpdateShieldVisual();

        // 背景音乐
        if (bgmSource != null && bgmSource.clip != null)
            StartCoroutine(PlaySoundDelayed(bgmSource, bgmStartTime, bgmDelay, true));

        if (hasShield && shieldAppearAudioSource != null && shieldAppearAudioSource.clip != null)
            StartCoroutine(PlaySoundDelayed(shieldAppearAudioSource, shieldAppearStartTime, shieldAppearDelay, false));

        SetupWinLine();
        InitializeBottomLine();

        spawner = FindObjectOfType<Spawner>();
        if (spawner == null) Debug.LogWarning("未找到 Spawner");

        Debug.Log($"🔄 初始化，能量={currentJumpCount}，无敌 {invincibleDuration} 秒，护盾={hasShield}");
    }

    void Update()
    {
        // --- 游戏结束长按重开 ---
        if (isGameEnded)
        {
            if (IsJumpKeyHeld())
            {
                spaceHoldTimer += Time.unscaledDeltaTime;
                if (spaceHoldTimer >= restartHoldDuration)
                {
                    RestartGame();
                    return;
                }
            }
            else
            {
                spaceHoldTimer = 0f;
            }
            return;
        }

        // --- 正常游戏 ---
        if (isGameOver || hasWon) return;

        // --- 无敌与闪烁逻辑（开局无敌） ---
        if (isInvincible)
        {
            float remaining = (gameStartTime + invincibleDuration) - Time.time;
            if (remaining > 0f)
            {
                if (remaining <= blinkBeforeEnd)
                {
                    blinkTimer += Time.deltaTime;
                    if (blinkTimer >= blinkInterval)
                    {
                        blinkTimer = 0f;
                        if (shieldSpriteRenderer != null)
                            shieldSpriteRenderer.enabled = !shieldSpriteRenderer.enabled;
                    }
                    if (shieldSpriteRenderer != null && shieldSpriteRenderer.sprite != invincibleSprite)
                        shieldSpriteRenderer.sprite = invincibleSprite;
                }
                else
                {
                    if (shieldSpriteRenderer != null)
                    {
                        shieldSpriteRenderer.sprite = invincibleSprite;
                        shieldSpriteRenderer.enabled = true;
                    }
                }
            }
            else
            {
                isInvincible = false;
                UpdateShieldVisual();
            }
        }

        // --- 临时无敌（护盾破碎后） ---
        if (isTempInvincible)
        {
            tempInvincibleTimer -= Time.deltaTime;
            if (tempInvincibleTimer <= 0f)
            {
                isTempInvincible = false;
                UpdateShieldVisual();
                Debug.Log("🛡️ 临时无敌结束");
            }
            else
            {
                if (shieldSpriteRenderer != null)
                {
                    shieldSpriteRenderer.sprite = shieldSprite;
                    shieldSpriteRenderer.enabled = Mathf.Floor(Time.time / tempBlinkInterval) % 2 == 0;
                }
            }
        }

        // --- 无能量 UI 反馈 ---
        float energyRatio = (float)currentJumpCount / maxJumpCount;
        feedbackTargetAlpha = Mathf.Approximately(energyRatio, 0f) ? 1f : 0f;
        UpdateFeedbackUI();

        // --- 输入处理 ---
        if (IsJumpKeyDown())
            isHoldingSpace = true;
        if (IsJumpKeyUp())
        {
            TryJump();
            isHoldingSpace = false;
        }
    }

    void FixedUpdate()
    {
        if (isGameOver || hasWon || isGameEnded) return;

        if (isHoldingSpace)
            ApplySlowFall();
        else
            ApplyAirDrag();

        if (isBottomLineInitialized && bottomLineObject != null)
        {
            Vector3 targetPos = transform.position;
            targetPos.y -= bottomLineDistance;
            targetPos.x = transform.position.x;
            bottomLineObject.transform.position = Vector3.Lerp(
                bottomLineObject.transform.position,
                targetPos,
                bottomLineFollowSpeed * Time.fixedDeltaTime
            );
        }
    }

    // ---------- 跳跃与物理 ----------
    void TryJump()
    {
        if (currentJumpCount > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            currentJumpCount--;
            totalJumps++;
            PlaySoundWithDelay(jumpAudioSource, jumpStartTime, jumpDelay);
            Debug.Log($"✅ 跳跃，剩余：{currentJumpCount}");
        }
        else
        {
            Debug.Log("⛔ 无跳跃次数");
        }
    }

    void ApplySlowFall()
    {
        float newXVel = Mathf.Lerp(rb.linearVelocity.x, 0f, horizontalDamping * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newXVel, -slowFallSpeed);
    }

    void ApplyAirDrag()
    {
        float dragFactor = 1f - airDrag * Time.fixedDeltaTime;
        if (dragFactor < 0) dragFactor = 0;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * dragFactor, rb.linearVelocity.y);
    }

    // ---------- 碰撞 ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isGameOver || hasWon || isGameEnded) return;
        if (!collision.gameObject.CompareTag(obstacleTag)) return;
        if (collision.gameObject.GetComponent<PolygonCollider2D>() == null) return;

        // 免疫条件：开局无敌、临时无敌
        if (isInvincible || isTempInvincible) return;

        if (hasShield)
        {
            PlaySoundWithDelay(shieldBreakAudioSource, shieldBreakStartTime, shieldBreakDelay);
            hasShield = false;
            UpdateShieldVisual();
            isTempInvincible = true;
            tempInvincibleTimer = tempInvincibleDuration;
            Debug.Log($"🛡️ 护盾破碎，进入 {tempInvincibleDuration} 秒临时无敌（护盾精灵闪烁）");
            return;
        }

        GameOver();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isGameOver || hasWon || isGameEnded) return;

        if (other.gameObject == winLineObject || other.CompareTag("WinLine"))
        {
            WinGame();
            return;
        }
        if (other.gameObject == bottomLineObject || other.CompareTag("BottomLine"))
        {
            Debug.Log("💀 碰到底部线");
            GameOver();
        }
    }

    // ---------- 游戏结束/胜利 ----------
    void GameOver()
    {
        isGameOver = true;
        isGameEnded = true;
        Time.timeScale = 0f;
        PlaySoundWithDelay(gameOverAudioSource, gameOverStartTime, gameOverDelay);
        if (bgmSource != null) bgmSource.Pause();
        Debug.Log("💀 游戏结束！");
    }

    void WinGame()
    {
        hasWon = true;
        isGameEnded = true;
        Time.timeScale = 0f;
        PlaySoundWithDelay(winAudioSource, winStartTime, winDelay);
        if (bgmSource != null) bgmSource.Pause();
        Debug.Log("🏆 游戏胜利！");
    }

    // ---------- 重置 ----------
    void RestartGame()
    {
        Debug.Log("🔄 重置游戏...");
        Time.timeScale = 1f;

        transform.position = new Vector3(0, startY, 0);
        rb.linearVelocity = Vector2.zero;

        if (startWithFullEnergy)
            currentJumpCount = maxJumpCount;
        else
            currentJumpCount = Mathf.Clamp(initialJumpCount, 0, maxJumpCount);

        bool hadShield = hasShield;
        hasShield = startWithShield;
        isInvincible = true;
        gameStartTime = Time.time;
        blinkTimer = 0f;
        isTempInvincible = false;
        tempInvincibleTimer = 0f;
        UpdateShieldVisual();

        if (hasShield && !hadShield && shieldAppearAudioSource != null && shieldAppearAudioSource.clip != null)
            StartCoroutine(PlaySoundDelayed(shieldAppearAudioSource, shieldAppearStartTime, shieldAppearDelay, false));

        isGameOver = false;
        hasWon = false;
        isGameEnded = false;
        isHoldingSpace = IsJumpKeyHeld();
        spaceHoldTimer = 0f;
        totalJumps = 0;

        // 重置底部线位置
        if (bottomLineObject != null)
        {
            Vector3 pos = transform.position;
            pos.y -= bottomLineDistance;
            pos.x = transform.position.x;
            bottomLineObject.transform.position = pos;
            isBottomLineInitialized = true;
        }

        if (bgmSource != null && bgmSource.clip != null)
            StartCoroutine(PlaySoundDelayed(bgmSource, bgmStartTime, bgmDelay, true));

        if (spawner != null)
            spawner.ResetSpawner();

        Debug.Log("✅ 重置完成，缓降状态：" + isHoldingSpace);
    }

    // ---------- UI 反馈更新 ----------
    void UpdateFeedbackUI()
    {
        if (vignetteImage != null)
        {
            float currentAlpha = vignetteImage.color.a;
            float newAlpha = Mathf.Lerp(currentAlpha, feedbackTargetAlpha, feedbackFadeSpeed * Time.deltaTime);
            Color c = vignetteImage.color;
            c.a = newAlpha;
            vignetteImage.color = c;
        }
        if (grayOverlayImage != null)
        {
            float currentAlpha = grayOverlayImage.color.a;
            float newAlpha = Mathf.Lerp(currentAlpha, feedbackTargetAlpha, feedbackFadeSpeed * Time.deltaTime);
            Color c = grayOverlayImage.color;
            c.a = newAlpha;
            grayOverlayImage.color = c;
        }
    }

    // ---------- 音效辅助 ----------
    IEnumerator PlaySoundDelayed(AudioSource source, float startTime, float delay, bool loop)
    {
        if (source == null || source.clip == null) yield break;
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);
        source.time = startTime;
        source.loop = loop;
        source.Play();
    }

    void PlaySoundWithDelay(AudioSource source, float startTime, float delay)
    {
        if (source == null || source.clip == null) return;
        StartCoroutine(PlaySoundDelayed(source, startTime, delay, false));
    }

    // ---------- 辅助方法 ----------
    void SetupWinLine()
    {
        if (winLineObject == null) return;
        Vector3 pos = winLineObject.transform.position;
        pos.y = transform.position.y + winLineYOffset;
        winLineObject.transform.position = pos;

        Collider2D col = winLineObject.GetComponent<Collider2D>();
        if (col == null) col = winLineObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        ((BoxCollider2D)col).size = new Vector2(20, 0.5f);
        winLineObject.tag = "WinLine";
        Debug.Log($"🏁 胜利线 Y = {pos.y}");
    }

    void InitializeBottomLine()
    {
        if (bottomLineObject == null)
        {
            GameObject newLine = new GameObject("BottomLine");
            newLine.transform.SetParent(transform);
            SpriteRenderer sr = newLine.AddComponent<SpriteRenderer>();
            sr.color = Color.red;
            BoxCollider2D col = newLine.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(20, 0.5f);
            newLine.tag = "BottomLine";
            bottomLineObject = newLine;
            Debug.Log("🆕 自动创建底部线");
        }
        else
        {
            Collider2D col = bottomLineObject.GetComponent<Collider2D>();
            if (col == null) col = bottomLineObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            ((BoxCollider2D)col).size = new Vector2(20, 0.5f);
            SpriteRenderer sr = bottomLineObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = bottomLineObject.AddComponent<SpriteRenderer>();
            sr.color = Color.red;
            sr.enabled = true;
            bottomLineObject.tag = "BottomLine";
        }

        Vector3 pos = bottomLineObject.transform.position;
        pos.y = transform.position.y - bottomLineDistance;
        pos.x = transform.position.x;
        bottomLineObject.transform.position = pos;
        isBottomLineInitialized = true;
        Debug.Log($"🔽 底部线 Y = {pos.y}");
    }

    void UpdateShieldVisual()
    {
        if (shieldSpriteRenderer == null) return;
        if (isTempInvincible) return;

        if (isInvincible && invincibleSprite != null)
        {
            shieldSpriteRenderer.sprite = invincibleSprite;
            shieldSpriteRenderer.enabled = true;
        }
        else if (hasShield && shieldSprite != null)
        {
            shieldSpriteRenderer.sprite = shieldSprite;
            shieldSpriteRenderer.enabled = true;
        }
        else
        {
            shieldSpriteRenderer.enabled = false;
        }
    }

    // ---------- 公共接口 ----------
    public void AddJumpCount(int amount)
    {
        if (isGameOver || hasWon || isGameEnded) return;

        if (currentJumpCount >= maxJumpCount)
        {
            if (!hasShield)
            {
                hasShield = true;
                UpdateShieldVisual();
                PlaySoundWithDelay(shieldAppearAudioSource, shieldAppearStartTime, shieldAppearDelay);
                Debug.Log("🛡️ 满能量获得护盾！");
            }
            return;
        }

        int remaining = maxJumpCount - currentJumpCount;
        if (amount > remaining)
        {
            currentJumpCount = maxJumpCount;
            if (!hasShield)
            {
                hasShield = true;
                UpdateShieldVisual();
                PlaySoundWithDelay(shieldAppearAudioSource, shieldAppearStartTime, shieldAppearDelay);
                Debug.Log($"🛡️ 能量溢出！奖励 {amount} > 剩余 {remaining}，获得护盾！");
            }
        }
        else
        {
            currentJumpCount = Mathf.Min(currentJumpCount + amount, maxJumpCount);
        }
    }

    public int GetCurrentJumpCount() => currentJumpCount;
    public bool HasShield() => hasShield;

    public float GetWinLineY()
    {
        if (winLineObject == null) return 0f;
        return winLineObject.transform.position.y;
    }

    public float GetBottomLineY()
    {
        if (bottomLineObject == null) return transform.position.y - bottomLineDistance;
        return bottomLineObject.transform.position.y;
    }
}