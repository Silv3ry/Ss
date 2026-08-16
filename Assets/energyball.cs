using UnityEngine;

public class Balll : MonoBehaviour
{
    [Header("碰撞设置")]
    public string targetTag = "Player";
    public int jumpBonus = 2;

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"⚪ 球碰到 {other.name}，标签：{other.tag}");

        if (other.CompareTag(targetTag))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.AddJumpCount(jumpBonus);
                Destroy(gameObject);
                Debug.Log("💥 球已销毁，跳跃次数已增加");
            }
            else
            {
                Debug.LogWarning("⚠️ 玩家物体上未找到 PlayerController 脚本！");
            }
        }
    }
}