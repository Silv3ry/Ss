using UnityEngine;

public class BackgroundColorController : MonoBehaviour
{
    [Header("玩家引用")]
    public PlayerController player;

    [Header("目标摄像机")]
    public Camera targetCamera;              // 手动指定要改变背景色的摄像机
    public string cameraTag = "";            // 或通过标签查找（如 "FollowCamera"）

    [Header("颜色设置")]
    public Color startColor = new Color(0.1f, 0.1f, 0.2f);
    public Color endColor = new Color(0.8f, 0.2f, 0.1f);
    public AnimationCurve progressCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private float startY;
    private bool hasStarted = false;

    void Start()
    {
        // 1. 查找玩家
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<PlayerController>();
        }
        if (player == null)
        {
            Debug.LogError("❌ 未找到 PlayerController！");
            return;
        }

        startY = player.transform.position.y;
        hasStarted = true;
        Debug.Log($"✅ 背景控制器初始化，起点 Y = {startY}");

        // 2. 查找目标摄像机
        if (targetCamera == null && !string.IsNullOrEmpty(cameraTag))
        {
            GameObject camObj = GameObject.FindGameObjectWithTag(cameraTag);
            if (camObj != null)
                targetCamera = camObj.GetComponent<Camera>();
        }
        if (targetCamera == null)
        {
            // 如果仍未找到，尝试使用 MainCamera
            targetCamera = Camera.main;
            if (targetCamera != null)
                Debug.LogWarning("⚠️ 未指定摄像机，自动使用 MainCamera。但您的跟随摄像机可能不是主摄像机，建议手动指定。");
            else
                Debug.LogError("❌ 未找到任何摄像机！请指定 targetCamera 或设置 cameraTag。");
        }
        else
        {
            Debug.Log($"📷 使用摄像机：{targetCamera.name}");
        }

        if (targetCamera != null)
        {
            // 确保摄像机为纯色背景模式
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            // 立即应用一次起始颜色
            SetBackgroundColor(startColor);
        }
    }

    void Update()
    {
        if (!hasStarted || player == null || targetCamera == null) return;

        float winY = player.GetWinLineY();
        if (winY <= startY)
        {
            SetBackgroundColor(startColor);
            return;
        }

        float progress = (player.transform.position.y - startY) / (winY - startY);
        progress = Mathf.Clamp01(progress);
        float curveProgress = progressCurve.Evaluate(progress);

        Color currentColor = Color.Lerp(startColor, endColor, curveProgress);
        SetBackgroundColor(currentColor);
    }

    void SetBackgroundColor(Color color)
    {
        if (targetCamera != null)
            targetCamera.backgroundColor = color;
    }
}