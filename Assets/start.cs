using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StartScreenManager : MonoBehaviour
{
    [Header("UI 引用")]
    public Canvas startCanvas;
    public Image backgroundImage;
    public Text blinkingText;

    [Header("长按设置")]
    public float holdDuration = 1.5f;

    private float holdTimer = 0f;
    private bool gameStarted = false;

    void Start()
    {
        if (startCanvas != null)
            startCanvas.gameObject.SetActive(true);
        else
            Debug.LogError("? StartCanvas 未设置！");

        Time.timeScale = 0f;

        if (blinkingText != null)
            StartCoroutine(BlinkText());
        else
            Debug.LogWarning("?? 闪烁文本未设置！");
    }

    void Update()
    {
        if (gameStarted) return;

        // 检测长按：空格 或 手柄 Button1
        bool isHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton1);

        if (isHeld)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer >= holdDuration)
            {
                StartGame();
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        Time.timeScale = 1f;

        if (startCanvas != null)
            startCanvas.gameObject.SetActive(false);

        StopAllCoroutines();

        // ★ 如果按键仍被按住，通知 PlayerController 进入缓降
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            bool isPressed = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton1);
            if (isPressed)
            {
                pc.SetHoldingSpace(true);
                Debug.Log("?? 检测到按键按住，自动进入缓降");
            }
        }

        Debug.Log("?? 游戏开始！");
    }

    IEnumerator BlinkText()
    {
        Text text = blinkingText;
        if (text == null) yield break;

        while (!gameStarted)
        {
            float speed = 2f;
            float timer = 0f;
            while (timer < 1.5f)
            {
                timer += Time.unscaledDeltaTime;
                float alpha = Mathf.PingPong(timer * speed, 1f);
                Color c = text.color;
                c.a = alpha;
                text.color = c;
                yield return null;
            }
            yield return null;
        }
    }
}