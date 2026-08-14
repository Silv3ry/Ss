using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Settings")]
    public string targetTag = "Player";   // 要检测的物体标签

    // 如果希望球也有生命周期（可选），可取消注释
    // public float lifeTime = 5f; 
    // void Start() { Destroy(gameObject, lifeTime); }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag))
        {
            Destroy(gameObject);
        }
    }

    // 如果是碰撞模式（非触发器），使用 OnCollisionEnter2D
    // void OnCollisionEnter2D(Collision2D collision) { ... }
}