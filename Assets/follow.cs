using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;          // 玩家或其他需要跟随的对象

    [Header("平滑参数")]
    public float smoothTime = 0.3f;   // 平滑时间，越小跟随越快
    public Vector3 offset = new Vector3(0, 0, -10); // 相机与目标的偏移量（Z轴通常设为 -10）

    [Header("可选：边界限制（世界坐标）")]
    public bool useBounds = false;
    public Vector2 minBounds;         // 左下角限制
    public Vector2 maxBounds;         // 右上角限制

    private Vector3 velocity = Vector3.zero; // SmoothDamp 使用的速度引用

    void LateUpdate()
    {
        if (target == null)
            return;

        // 计算目标位置（考虑偏移）
        Vector3 targetPosition = target.position + offset;

        // 使用 SmoothDamp 平滑移动
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        // 若启用边界，则限制相机位置（注意要减去偏移量或者直接限制世界坐标）
        if (useBounds)
        {
            // 由于相机可能带偏移，我们需要限制相机的实际世界坐标
            // 这里简单限制 transform.position，但如果有偏移量可能导致边界不准确，
            // 更严谨的做法是计算相机视口范围，但作为基础示例，直接限制位置即可。
            float clampedX = Mathf.Clamp(transform.position.x, minBounds.x, maxBounds.x);
            float clampedY = Mathf.Clamp(transform.position.y, minBounds.y, maxBounds.y);
            transform.position = new Vector3(clampedX, clampedY, transform.position.z);
        }
    }
}