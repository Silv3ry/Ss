using UnityEngine;
using UnityEngine.UI;

public class JumpEnergyUI : MonoBehaviour
{
    [Header("玩家引用")]
    public PlayerController player;

    [Header("跳跃图标（4个）")]
    public Image[] jumpImages;          // 4个图标的 Image 组件
    public Sprite[] jumpLitSprites;     // 对应的亮态贴图（4个）
    public Sprite[] jumpDimSprites;     // 对应的暗态贴图（4个）

    [Header("护盾图标（1个）")]
    public Image shieldImage;           // 护盾图标 Image
    public Sprite shieldLitSprite;      // 护盾亮态贴图
    public Sprite shieldDimSprite;      // 护盾暗态贴图

    private void Start()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<PlayerController>();
        }
        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (player == null) return;

        // 1. 更新跳跃图标
        int current = player.GetCurrentJumpCount();
        for (int i = 0; i < jumpImages.Length; i++)
        {
            if (jumpImages[i] == null) continue;
            bool isLit = (current > i);
            // 确保亮/暗贴图数组有对应元素
            Sprite target = isLit ? (i < jumpLitSprites.Length ? jumpLitSprites[i] : null)
                                   : (i < jumpDimSprites.Length ? jumpDimSprites[i] : null);
            if (target != null)
                jumpImages[i].sprite = target;
        }

        // 2. 更新护盾图标
        if (shieldImage != null)
        {
            bool hasShield = player.HasShield();
            Sprite target = hasShield ? shieldLitSprite : shieldDimSprite;
            if (target != null)
                shieldImage.sprite = target;
        }
    }
}