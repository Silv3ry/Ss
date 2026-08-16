using System.Collections;
using UnityEngine;

public enum PopupMotionType
{
    FallDown,
    ThrowUp,
    HoverAbove,
    HoverBelow
}

public class PopupEffect : MonoBehaviour
{
    private Vector2 startPos;
    private PopupMotionType motion;
    private float fadeDuration;
    private PopupEffectManager manager;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    public void Initialize(Vector2 pos, PopupMotionType motionType, float fadeTime, PopupEffectManager mgr)
    {
        startPos = pos;
        motion = motionType;
        fadeDuration = fadeTime;
        manager = mgr;
        transform.position = pos;
        StartCoroutine(MotionCoroutine());
    }

    IEnumerator MotionCoroutine()
    {
        float elapsed = 0f;
        Vector2 currentPos = startPos;
        Vector2 velocity = Vector2.zero;

        switch (motion)
        {
            case PopupMotionType.FallDown:
                break;
            case PopupMotionType.ThrowUp:
                float xDir = Random.Range(-1f, 1f);
                if (Mathf.Approximately(xDir, 0)) xDir = 1f;
                velocity = new Vector2(xDir * manager.GetHorizontalSpread() * 0.5f, manager.GetThrowUpHeight() * 0.5f);
                break;
            case PopupMotionType.HoverAbove:
                currentPos.y += 1.5f;
                transform.position = currentPos;
                break;
            case PopupMotionType.HoverBelow:
                currentPos.y -= 1.5f;
                transform.position = currentPos;
                break;
        }

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeDuration;

            switch (motion)
            {
                case PopupMotionType.FallDown:
                    currentPos.y -= manager.GetFallSpeed() * Time.deltaTime;
                    transform.position = currentPos;
                    break;
                case PopupMotionType.ThrowUp:
                    velocity.y -= manager.GetThrowUpHeight() * 1.5f * Time.deltaTime;
                    if (velocity.y < -manager.GetThrowUpHeight() * 0.8f)
                        velocity.y = -manager.GetThrowUpHeight() * 0.8f;
                    currentPos += velocity * Time.deltaTime;
                    transform.position = currentPos;
                    break;
                case PopupMotionType.HoverAbove:
                case PopupMotionType.HoverBelow:
                    break;
            }

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f - progress;
                spriteRenderer.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

public class PopupEffectManager : MonoBehaviour
{
    public static PopupEffectManager Instance { get; private set; }

    [Header("预制体（可选）")]
    public GameObject popupPrefab;

    [Header("运动参数")]
    public float throwUpHeight = 2f;
    public float horizontalSpread = 1f;
    public float fallSpeed = 1.5f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // 新增图层参数
    public void SpawnPopup(Vector2 position, Sprite sprite, PopupMotionType motion, float fadeDuration,
                           float scale = 1f, string sortingLayer = "Default", int sortingOrder = 0)
    {
        if (sprite == null)
        {
            Debug.LogWarning("弹窗 Sprite 为空，跳过生成。");
            return;
        }

        GameObject obj;
        if (popupPrefab != null)
        {
            obj = Instantiate(popupPrefab, position, Quaternion.identity);
        }
        else
        {
            obj = new GameObject("PopupEffect");
            obj.transform.position = position;
            obj.AddComponent<SpriteRenderer>();
        }

        // 设置缩放
        obj.transform.localScale = Vector3.one * scale;

        // 设置图层
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
        }

        PopupEffect effect = obj.GetComponent<PopupEffect>();
        if (effect == null)
            effect = obj.AddComponent<PopupEffect>();

        effect.Initialize(position, motion, fadeDuration, this);
        Debug.Log($"弹窗生成：位置 {position}，运动 {motion}，渐隐 {fadeDuration}秒，缩放 {scale}，图层 {sortingLayer}，层级 {sortingOrder}");
    }

    public float GetThrowUpHeight() => throwUpHeight;
    public float GetHorizontalSpread() => horizontalSpread;
    public float GetFallSpeed() => fallSpeed;
}