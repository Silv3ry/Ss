using UnityEngine;
using UnityEngine.UI;

public class DistanceDisplay : MonoBehaviour
{
    [Header("引用")]
    public Text distanceText;               // 如果是旧版 Text 用此字段
    // 如果您使用 TextMeshPro，请改为：public TextMeshProUGUI distanceText;
    public PlayerController player;

    [Header("格式")]
    public string prefix = "距离终点: ";
    public string suffix = "m";
    public float updateInterval = 0.1f;

    private float timer = 0f;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.GetComponent<PlayerController>();
            else
                Debug.LogError("❌ 未找到标签为 'Player' 的物体！");
        }

        if (distanceText == null)
            Debug.LogError("❌ 未指定 Text 组件！请在 Inspector 中拖拽。");
    }

    void Update()
    {
        if (player == null || distanceText == null) return;

        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;

        float winLineY = player.GetWinLineY();
        float playerY = player.transform.position.y;
        float distance = Mathf.Max(0, winLineY - playerY);

        distanceText.text = $"{prefix}{distance:F1}{suffix}";
    }
}