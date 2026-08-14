using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI 引用")]
    public Slider jumpEnergySlider;      // 能量条 Slider
    public Text energyText;              // 可选，显示 "当前/最大"

    private PlayerController player;

    void Start()
    {
        // 1. 查找玩家
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerController>();
            if (player == null)
                Debug.LogError("❌ 玩家物体上没有 PlayerController 脚本！");
        }
        else
        {
            Debug.LogError("❌ 未找到标签为 'Player' 的物体！请确保玩家物体标签已设置为 'Player'。");
        }

        // 2. 检查 Slider 引用
        if (jumpEnergySlider == null)
            Debug.LogError("❌ UIManager 中未拖拽 Slider 引用！");
    }

    void Update()
    {
        // 每帧更新 UI（若引用缺失则跳过）
        if (player == null || jumpEnergySlider == null) return;

        int current = player.GetCurrentJumpCount();
        int max = player.maxJumpCount;

        // 更新 Slider 范围（防止因 Max 变化导致显示异常）
        jumpEnergySlider.minValue = 0;
        jumpEnergySlider.maxValue = max;
        jumpEnergySlider.wholeNumbers = true;
        jumpEnergySlider.value = current;

        // 更新文本（如果有）
        if (energyText != null)
        {
            energyText.text = $"{current} / {max}";
        }
    }
}