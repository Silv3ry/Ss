using UnityEngine;

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

    // ---- 能量球与空能量音效 ----
    [Header("能量球音效")]
    public AudioSource energyPlus1AudioSource;
    public float energyPlus1StartTime = 0f;
    public float energyPlus1Delay = 0f;

    public AudioSource energyPlus2AudioSource;
    public float energyPlus2StartTime = 0f;
    public float energyPlus2Delay = 0f;

    public AudioSource energyPlus4AudioSource;
    public float energyPlus4StartTime = 0f;
    public float energyPlus4Delay = 0f;

    [Header("空能量音效")]
    public AudioSource energyInsufficientAudioSource;
    public float energyInsufficientStartTime = 0f;
    public float energyInsufficientDelay = 0f;

    // ---------- 蹦出图片配置 ----------
    [System.Serializable]
    public class PopupEventConfig
    {
        public Sprite sprite;
        public PopupMotionType motionType = PopupMotionType.FallDown;
        public float fadeDuration = 1f;
        public float scale = 1f;
        public string sortingLayer = "Default";
        public int sortingOrder = 0;
    }

    [Header("蹦出图片配置")]
    public PopupEventConfig jumpPopup;
    public PopupEventConfig energyPlus1Popup;
    public PopupEventConfig energyPlus2Popup;
    public PopupEventConfig energyPlus4Popup;
    public PopupEventConfig energyInsufficientPopup;
    public PopupEventConfig energyFullPopup;
    public PopupEventConfig shieldGainPopup;

    // ---------- 结算画面UI ----------
    [Header("结算画面UI（同一个Canvas下）")]
    public GameObject endUIRoot;
    public Image failFinalImage;
    public Image winFinalImage;

    [Header("游戏UI（同一个Canvas下）")]
    public GameObject gameUIRoot;

    // ---------- 当前状态 ----------
    [Header("当前状态（只读）")]
    [SerializeField] private int currentJumpCount = 0;
    [SerializeField] private bool hasShield = false;

    private Rigidbody2D rb;
    private bool isGameOver = false;
    private bool hasWon = false;
    private float gameStartTime;
    private bool isInvincible = false;
    private bool isHoldingSpace = false;

    private bool isGameEnded = false;
    private float spaceHoldTimer = 0f;
    private float startY;

    private Spawner spawner;
    private Camera mainCam;

    // ---------- 临时无敌 ----------
    private bool isTempInvincible = false;
    private float tempInvincibleTimer = 0f;

    // ---------- 输入辅助 ----------
    bool IsJumpKeyDown()
    {
        if (inputMode == InputMode.Keyboard)
            return Input.GetKeyDown(KeyCode.Space);
        else
            return Input.GetKeyDown(KeyCode.JoystickButton1);
    }

    bool IsJumpKeyHeld()
    {
        if (inputMode == InputMode.Keyboard)
            return Input.GetKey(KeyCode.Space);
        else
            return Input.GetKey(KeyCode.JoystickButton1);
    }

    bool IsJumpKeyUp()
    {
        if (inputMode == InputMode.Keyboard)
            return Input.GetKeyUp(KeyCode.Space);
        else
            return Input.GetKeyUp(KeyCode.JoystickButton1);
    }

    // ---------- 确保 PopupEffectManager 存在 ----------
    private void EnsurePopupManagerExists()
    {
        if (PopupEffectManager.Instance == null)
        {
            GameObject managerObj = new GameObject("PopupManager");
            PopupEffectManager manager = managerObj.AddComponent<PopupEffectManager>();
            Debug.Log("✅ 已自动创建 PopupEffectManager，弹窗功能已启用。");
        }
    }

    // ---------- 音效播放辅助 ----------
    void PlaySoundWithDelay(AudioSource source, float startTime, float delay)
    {
        if (source == null || source.clip == null) return;
        StartCoroutine(PlaySoundDelayed(source, startTime, delay, false));
    }

    IEnumerator PlaySoundDelayed(AudioSource source, float startTime, float delay, bool loop)
    {
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);
        source.time = startTime;
        source.loop = loop;
        source.Play();
    }

    // ---------- MonoBehaviour ----------
    void Start()
    {
        EnsurePopupManagerExists();

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

        if (bgmSource != null && bgmSource.clip != null)
            StartCoroutine(PlaySoundDelayed(bgmSource, bgmStartTime, bgmDelay, true));

        if (hasShield && shieldAppearAudioSource != null && shieldAppearAudioSource.clip != null)
            StartCoroutine(PlaySoundDelayed(shieldAppearAudioSource, shieldAppearStartTime, shieldAppearDelay, false));

        SetupWinLine();
        InitializeBottomLine();

        spawner = FindObjectOfType<Spawner>();
        if (spawner == null) Debug.LogWarning("未找到 Spawner");

        mainCam = Camera.main;

        if (gameUIRoot != null) gameUIRoot.SetActive(true);
        if (endUIRoot != null) endUIRoot.SetActive(false);

        ValidateEndImages();

        Debug.Log($"🔄 初始化，能量={currentJumpCount}，无敌 {invincibleDuration} 秒，护盾={hasShield}");
    }

    void ValidateEndImages()
    {
        if (failFinalImage == null)
            Debug.LogError("❌ failFinalImage 未设置！");
        else
        {
            failFinalImage.color = Color.white;
            failFinalImage.raycastTarget = false;
            if (failFinalImage.sprite == null)
                Debug.LogWarning("⚠️ failFinalImage 的 Sprite 为空，请为其指定一张图片。");
            else
                Debug.Log($"✅ failFinalImage Sprite: {failFinalImage.sprite.name}");
        }

        if (winFinalImage == null)
            Debug.LogError("❌ winFinalImage 未设置！");
        else
        {
            winFinalImage.color = Color.white;
            winFinalImage.raycastTarget = false;
            if (winFinalImage.sprite == null)
                Debug.LogWarning("⚠️ winFinalImage 的 Sprite 为空，请为其指定一张图片。");
            else
                Debug.Log($"✅ winFinalImage Sprite: {winFinalImage.sprite.name}");
        }

        SetAlpha(failFinalImage, 0f);
        SetAlpha(winFinalImage, 0f);
    }

    void SetAlpha(Image img, float alpha)
    {
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    void Update()
    {
        // ★ 开始界面期间（Time.timeScale == 0）不处理任何输入
        if (Time.timeScale == 0f) return;

        if (isGameEnded) return;

        if (isGameOver || hasWon) return;

        // 无敌与闪烁逻辑
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

        // 临时无敌
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

        // 无能量 UI 反馈
        float energyRatio = (float)currentJumpCount / maxJumpCount;
        feedbackTargetAlpha = Mathf.Approximately(energyRatio, 0f) ? 1f : 0f;
        UpdateFeedbackUI();

        // 输入处理
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
    }

    // ---------- 跳跃与物理 ----------
    void TryJump()
    {
        if (currentJumpCount > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            currentJumpCount--;
            totalJumps++;

            // 跳跃音效
            if (jumpAudioSource != null && jumpAudioSource.clip != null)
            {
                jumpAudioSource.time = jumpStartTime;
                jumpAudioSource.Play();
            }

            if (jumpPopup.sprite != null && PopupEffectManager.Instance != null)
            {
                PopupEffectManager.Instance.SpawnPopup(
                    transform.position,
                    jumpPopup.sprite,
                    jumpPopup.motionType,
                    jumpPopup.fadeDuration,
                    jumpPopup.scale,
                    jumpPopup.sortingLayer,
                    jumpPopup.sortingOrder
                );
            }

            Debug.Log($"✅ 跳跃，剩余：{currentJumpCount}");
        }
        else
        {
            // 空能量尝试跳跃音效
            PlaySoundWithDelay(energyInsufficientAudioSource, energyInsufficientStartTime, energyInsufficientDelay);

            if (energyInsufficientPopup.sprite != null && PopupEffectManager.Instance != null)
            {
                PopupEffectManager.Instance.SpawnPopup(
                    transform.position,
                    energyInsufficientPopup.sprite,
                    energyInsufficientPopup.motionType,
                    energyInsufficientPopup.fadeDuration,
                    energyInsufficientPopup.scale,
                    energyInsufficientPopup.sortingLayer,
                    energyInsufficientPopup.sortingOrder
                );
            }
            Debug.Log("⛔ 无跳跃次数");
        }
    }

    void ApplySlowFall()
    {
        float newXVel = Mathf.Lerp(rb.velocity.x, 0f, horizontalDamping * Time.fixedDeltaTime);
        rb.velocity = new Vector2(newXVel, -slowFallSpeed);
    }

    void ApplyAirDrag()
    {
        float dragFactor = 1f - airDrag * Time.fixedDeltaTime;
        if (dragFactor < 0) dragFactor = 0;
        rb.velocity = new Vector2(rb.velocity.x * dragFactor, rb.velocity.y);
    }

    // ---------- 碰撞 ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isGameOver || hasWon || isGameEnded) return;
        if (!collision.gameObject.CompareTag(obstacleTag)) return;
        if (collision.gameObject.GetComponent<PolygonCollider2D>() == null) return;

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

        StartCoroutine(FailAnimation());
    }

    void WinGame()
    {
        hasWon = true;
        isGameEnded = true;
        Time.timeScale = 0f;
        PlaySoundWithDelay(winAudioSource, winStartTime, winDelay);
        if (bgmSource != null) bgmSource.Pause();

        StartCoroutine(WinAnimation());
    }

    // ---------- 失败动画 ----------
    IEnumerator FailAnimation()
    {
        if (gameUIRoot != null) gameUIRoot.SetActive(false);
        if (endUIRoot != null) endUIRoot.SetActive(true);

        Vector3 startCamPos = mainCam.transform.position;
        Vector3 targetCamPos = new Vector3(0, transform.position.y, startCamPos.z);
        float timer = 0f;
        float moveDuration = 0.5f;
        while (timer < moveDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / moveDuration;
            mainCam.transform.position = Vector3.Lerp(startCamPos, targetCamPos, t);
            yield return null;
        }
        mainCam.transform.position = targetCamPos;

        if (failFinalImage != null && failFinalImage.sprite != null)
        {
            SetAlpha(failFinalImage, 0f);
            timer = 0f;
            float fadeDuration = 1f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / fadeDuration;
                SetAlpha(failFinalImage, Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            SetAlpha(failFinalImage, 1f);
        }
        else
        {
            Debug.LogWarning("⚠️ failFinalImage 或其 Sprite 为空，无法显示失败图！");
        }

        yield return StartCoroutine(WaitForRestart());
    }

    // ---------- 胜利动画 ----------
    IEnumerator WinAnimation()
    {
        if (gameUIRoot != null) gameUIRoot.SetActive(false);
        if (endUIRoot != null) endUIRoot.SetActive(true);

        if (winFinalImage != null && winFinalImage.sprite != null)
        {
            SetAlpha(winFinalImage, 0f);
            float timer = 0f;
            float fadeDuration = 1f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / fadeDuration;
                SetAlpha(winFinalImage, Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            SetAlpha(winFinalImage, 1f);
        }
        else
        {
            Debug.LogWarning("⚠️ winFinalImage 或其 Sprite 为空，无法显示胜利图！");
        }

        yield return StartCoroutine(WaitForRestart());
    }

    // ---------- 等待长按重开 ----------
    IEnumerator WaitForRestart()
    {
        float holdTimer = 0f;
        while (true)
        {
            if (IsJumpKeyHeld())
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= restartHoldDuration)
                {
                    RestartGame();
                    yield break;
                }
            }
            else
            {
                holdTimer = 0f;
            }
            yield return null;
        }
    }

    // ---------- 重置 ----------
    void RestartGame()
    {
        Debug.Log("🔄 重置游戏...");
        Time.timeScale = 1f;

        transform.position = new Vector3(0, startY, 0);
        rb.velocity = Vector2.zero;

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
        isHoldingSpace = false;
        spaceHoldTimer = 0f;
        totalJumps = 0;

        if (mainCam != null)
        {
            Vector3 camPos = mainCam.transform.position;
            camPos.y = startY;
            camPos.x = 0;
            mainCam.transform.position = camPos;
        }

        if (gameUIRoot != null) gameUIRoot.SetActive(true);
        if (endUIRoot != null) endUIRoot.SetActive(false);
        SetAlpha(failFinalImage, 0f);
        SetAlpha(winFinalImage, 0f);

        if (bgmSource != null && bgmSource.clip != null)
            StartCoroutine(PlaySoundDelayed(bgmSource, bgmStartTime, bgmDelay, true));

        if (spawner != null)
            spawner.ResetSpawner();

        Debug.Log("✅ 重置完成");
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

            Vector3 pos = transform.position;
            pos.y -= bottomLineDistance;
            pos.x = transform.position.x;
            bottomLineObject.transform.position = pos;
            Debug.Log("🆕 自动创建底部线（固定位置）");
        }
        else
        {
            Collider2D col = bottomLineObject.GetComponent<Collider2D>();
            if (col == null)
            {
                col = bottomLineObject.AddComponent<BoxCollider2D>();
                ((BoxCollider2D)col).size = new Vector2(20, 0.5f);
            }
            col.isTrigger = true;
            SpriteRenderer sr = bottomLineObject.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = bottomLineObject.AddComponent<SpriteRenderer>();
            sr.color = Color.red;
            sr.enabled = true;
            bottomLineObject.tag = "BottomLine";
            Debug.Log($"🔽 底部线已固定，位置 Y = {bottomLineObject.transform.position.y}");
        }
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

        // 播放对应的能量球音效
        if (amount == 1)
            PlaySoundWithDelay(energyPlus1AudioSource, energyPlus1StartTime, energyPlus1Delay);
        else if (amount == 2)
            PlaySoundWithDelay(energyPlus2AudioSource, energyPlus2StartTime, energyPlus2Delay);
        else if (amount == 4)
            PlaySoundWithDelay(energyPlus4AudioSource, energyPlus4StartTime, energyPlus4Delay);

        int oldCount = currentJumpCount;
        int newCount = oldCount + amount;

        bool showPlus = (oldCount < maxJumpCount);
        bool showFull = (oldCount >= maxJumpCount || newCount > maxJumpCount);
        bool shouldGiveShield = (oldCount >= maxJumpCount || newCount > maxJumpCount);

        // ----- 显示加能量图片 -----
        if (showPlus)
        {
            if (amount == 1 && energyPlus1Popup.sprite != null && PopupEffectManager.Instance != null)
                PopupEffectManager.Instance.SpawnPopup(transform.position, energyPlus1Popup.sprite, energyPlus1Popup.motionType,
                    energyPlus1Popup.fadeDuration, energyPlus1Popup.scale, energyPlus1Popup.sortingLayer, energyPlus1Popup.sortingOrder);
            else if (amount == 2 && energyPlus2Popup.sprite != null && PopupEffectManager.Instance != null)
                PopupEffectManager.Instance.SpawnPopup(transform.position, energyPlus2Popup.sprite, energyPlus2Popup.motionType,
                    energyPlus2Popup.fadeDuration, energyPlus2Popup.scale, energyPlus2Popup.sortingLayer, energyPlus2Popup.sortingOrder);
            else if (amount == 4 && energyPlus4Popup.sprite != null && PopupEffectManager.Instance != null)
                PopupEffectManager.Instance.SpawnPopup(transform.position, energyPlus4Popup.sprite, energyPlus4Popup.motionType,
                    energyPlus4Popup.fadeDuration, energyPlus4Popup.scale, energyPlus4Popup.sortingLayer, energyPlus4Popup.sortingOrder);
        }

        // ----- 显示溢出图片 -----
        if (showFull)
        {
            if (energyFullPopup.sprite != null && PopupEffectManager.Instance != null)
                PopupEffectManager.Instance.SpawnPopup(
                    transform.position,
                    energyFullPopup.sprite,
                    energyFullPopup.motionType,
                    energyFullPopup.fadeDuration,
                    energyFullPopup.scale,
                    energyFullPopup.sortingLayer,
                    energyFullPopup.sortingOrder
                );
        }

        // ----- 给予护盾 -----
        if (shouldGiveShield && !hasShield)
        {
            hasShield = true;
            UpdateShieldVisual();
            PlaySoundWithDelay(shieldAppearAudioSource, shieldAppearStartTime, shieldAppearDelay);

            if (shieldGainPopup.sprite != null && PopupEffectManager.Instance != null)
                PopupEffectManager.Instance.SpawnPopup(
                    transform.position,
                    shieldGainPopup.sprite,
                    shieldGainPopup.motionType,
                    shieldGainPopup.fadeDuration,
                    shieldGainPopup.scale,
                    shieldGainPopup.sortingLayer,
                    shieldGainPopup.sortingOrder
                );
            Debug.Log("🛡️ 能量溢出或满能量获得护盾！");
        }

        // ----- 更新能量值 -----
        if (oldCount < maxJumpCount)
        {
            currentJumpCount = Mathf.Min(newCount, maxJumpCount);
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

    // ★ 新增：供 StartScreenManager 调用的方法，设置缓降状态
    public void SetHoldingSpace(bool holding)
    {
        isHoldingSpace = holding;
    }
}