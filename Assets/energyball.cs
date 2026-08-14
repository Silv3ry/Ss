using System.Collections;
using UnityEngine;

public class Balll : MonoBehaviour
{
    [Header("碰撞设置")]
    public string targetTag = "Player";
    public int jumpBonus = 2;

    // ---------- 音效 ----------
    [Header("音效")]
    public AudioSource energyCollectAudioSource;   // 能量球收集音效（独立 AudioSource）
    public float energyCollectStartTime = 0f;      // 起始播放位置（秒）
    public float energyCollectDelay = 0f;          // 延迟播放（秒）

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"⚪ 球碰到 {other.name}，标签：{other.tag}");

        if (other.CompareTag(targetTag))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.AddJumpCount(jumpBonus);

                // 播放收集音效（带延迟）
                PlaySoundWithDelay(energyCollectAudioSource, energyCollectStartTime, energyCollectDelay);

                // 销毁球（音效会继续播放，因为 AudioSource 独立）
                Destroy(gameObject);
                Debug.Log("💥 球已销毁，跳跃次数已增加");
            }
            else
            {
                Debug.LogWarning("⚠️ 玩家物体上未找到 PlayerController 脚本！");
            }
        }
    }

    // ---------- 音效辅助方法 ----------
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
}