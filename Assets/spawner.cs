using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    // 三种障碍物预制体（对应不同奖励）
    public GameObject rectanglePrefabNormal;   // 原版（掉落 +1）
    public GameObject rectanglePrefabPlus2;    // 掉落 +2
    public GameObject rectanglePrefabPlus4;    // 掉落 +4

    // 三种能量球预制体（对应不同奖励）
    public GameObject ballPrefabNormal;        // +1
    public GameObject ballPrefabPlus2;         // +2
    public GameObject ballPrefabPlus4;         // +4

    [Header("动态间隔设置")]
    public float minInterval = 0.5f;
    public float maxInterval = 2f;

    [Header("Spawn Settings")]
    public float lengthMin = 1f;
    public float lengthMax = 3f;
    public float width = 0.2f;

    [Header("Angle Ranges")]
    public float angleLeftMin = 300f;
    public float angleLeftMax = 330f;
    public float angleRightMin = 210f;
    public float angleRightMax = 240f;

    [Header("Lifecycle")]
    public float lifeTime = 0.5f;
    public float shrinkDuration = 0.2f;

    [Header("Warning Settings")]
    public GameObject warningPrefab;
    public float warningDuration = 0.2f;

    [Header("Position Offset")]
    public float yOffset = 0f;

    [Header("Ball Settings")]
    public float ballScale = 0.3f;

    // ---------- 音效 ----------
    [Header("音效")]
    public AudioSource spawnAudioSource;
    public float spawnStartTime = 0f;
    public float spawnDelay = 0f;

    private Camera cam;
    private Vector3 nextSpawnPos;
    private float nextAngle;
    private float nextLength;
    private bool nextIsLeft;

    private int consecutiveSameSide = 0;
    private bool lastSideLeft;

    private float startY;
    private bool isFirstSpawn = true;

    private Coroutine spawnCoroutine;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    // ---------- 枚举：障碍物类型 ----------
    private enum ObstacleType { Normal, Plus2, Plus4 }

    void Start()
    {
        cam = Camera.main;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player").transform;
        if (player == null)
            Debug.LogError("❌ 未找到玩家！");

        startY = player.position.y;
        PrepareNextSpawn();
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    // ---------- 进度计算 ----------
    float GetProgress()
    {
        if (player == null) return 0f;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null) return 0f;
        float winY = pc.GetWinLineY();
        if (winY <= startY) return 0f;
        float progress = (player.position.y - startY) / (winY - startY);
        return Mathf.Clamp01(progress);
    }

    // ---------- 根据进度选择障碍物类型（修正版） ----------
    ObstacleType SelectObstacleType(float progress)
    {
        // progress = 0 为起点，1 为终点
        // 分段：0~0.5 从 +4 过渡到 +2；0.5~1 从 +2 过渡到 +1
        if (progress < 0.5f)
        {
            float t = progress / 0.5f; // 0~1
            float p4 = 1 - t;          // +4 概率从1降到0
            float p2 = t;              // +2 概率从0升到1
            float rand = Random.value;
            if (rand < p4)
                return ObstacleType.Plus4;
            else
                return ObstacleType.Plus2;
        }
        else
        {
            float t = (progress - 0.5f) / 0.5f; // 0~1
            float p2 = 1 - t;                  // +2 概率从1降到0
            float p1 = t;                      // +1 概率从0升到1
            float rand = Random.value;
            if (rand < p2)
                return ObstacleType.Plus2;
            else
                return ObstacleType.Normal;
        }
    }

    // ---------- 生成协程 ----------
    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            float interval = GetDynamicInterval();
            yield return new WaitForSeconds(interval);

            float progress = GetProgress();
            ObstacleType type = SelectObstacleType(progress);

            GameObject rectPrefab = GetRectanglePrefab(type);
            GameObject ballPrefab = GetBallPrefab(type);
            int bonus = GetBonus(type);

            Vector3 currentPos = nextSpawnPos;
            float currentAngle = nextAngle;
            float currentLength = nextLength;

            PrepareNextSpawn();

            GameObject rect = Instantiate(rectPrefab, currentPos, Quaternion.identity);
            rect.transform.eulerAngles = new Vector3(0, 0, currentAngle);
            rect.transform.localScale = new Vector3(currentLength, width, 1);
            spawnedObjects.Add(rect);

            if (isFirstSpawn)
            {
                foreach (var sr in rect.GetComponentsInChildren<SpriteRenderer>())
                    sr.enabled = false;
                foreach (var col in rect.GetComponentsInChildren<Collider2D>())
                    col.enabled = false;
                isFirstSpawn = false;
            }
            else
            {
                if (spawnAudioSource != null && spawnAudioSource.clip != null)
                    StartCoroutine(PlaySoundDelayed(spawnAudioSource, spawnStartTime, spawnDelay, false));

                if (ballPrefab != null)
                {
                    Vector3 ballPos = GetBallPositionOnVerticalAxis(rect);
                    if (ballPos != Vector3.zero)
                    {
                        GameObject ball = Instantiate(ballPrefab, ballPos, Quaternion.identity);
                        ball.transform.localScale = Vector3.one * ballScale;
                        Balll ballScript = ball.GetComponent<Balll>();
                        if (ballScript != null)
                            ballScript.jumpBonus = bonus;
                        spawnedObjects.Add(ball);
                    }
                }
            }

            StartCoroutine(ShrinkAndDestroy(rect, lifeTime, shrinkDuration,
                                            nextSpawnPos, nextAngle, nextLength));
        }
    }

    // ---------- 辅助方法 ----------
    GameObject GetRectanglePrefab(ObstacleType type)
    {
        switch (type)
        {
            case ObstacleType.Normal: return rectanglePrefabNormal ?? rectanglePrefabNormal;
            case ObstacleType.Plus2: return rectanglePrefabPlus2 ?? rectanglePrefabNormal;
            case ObstacleType.Plus4: return rectanglePrefabPlus4 ?? rectanglePrefabNormal;
            default: return rectanglePrefabNormal;
        }
    }

    GameObject GetBallPrefab(ObstacleType type)
    {
        switch (type)
        {
            case ObstacleType.Normal: return ballPrefabNormal ?? ballPrefabNormal;
            case ObstacleType.Plus2: return ballPrefabPlus2 ?? ballPrefabNormal;
            case ObstacleType.Plus4: return ballPrefabPlus4 ?? ballPrefabNormal;
            default: return ballPrefabNormal;
        }
    }

    int GetBonus(ObstacleType type)
    {
        switch (type)
        {
            case ObstacleType.Normal: return 1;
            case ObstacleType.Plus2: return 2;
            case ObstacleType.Plus4: return 4;
            default: return 1;
        }
    }

    // ---------- 动态间隔 ----------
    float GetDynamicInterval()
    {
        if (player == null) return maxInterval;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null) return maxInterval;
        float winY = pc.GetWinLineY();
        if (winY <= startY) return maxInterval;
        float progress = GetProgress();
        return Mathf.Lerp(maxInterval, minInterval, progress);
    }

    // ---------- 左右平衡 ----------
    bool DecideNextSide()
    {
        bool newSide;
        if (consecutiveSameSide >= 2)
        {
            newSide = !lastSideLeft;
            consecutiveSameSide = 1;
        }
        else
        {
            newSide = Random.value < 0.5f;
            if (newSide == lastSideLeft)
                consecutiveSameSide++;
            else
                consecutiveSameSide = 1;
        }
        lastSideLeft = newSide;
        return newSide;
    }

    void PrepareNextSpawn()
    {
        bool left = DecideNextSide();
        nextIsLeft = left;

        Vector3 playerPos = player.position;
        float leftX = cam.ScreenToWorldPoint(new Vector3(0, 0, 0)).x;
        float rightX = cam.ScreenToWorldPoint(new Vector3(Screen.width, 0, 0)).x;
        float topY = cam.ScreenToWorldPoint(new Vector3(0, Screen.height, 0)).y;

        float minY = playerPos.y + 1f;
        float maxY = topY - 0.5f;
        if (maxY < minY) maxY = minY + 1f;

        float y = Random.Range(minY, maxY) + yOffset;
        float x = left ? leftX : rightX;
        nextSpawnPos = new Vector3(x, y, 0);

        float angleMin = left ? angleLeftMin : angleRightMin;
        float angleMax = left ? angleLeftMax : angleRightMax;
        nextAngle = Random.Range(angleMin, angleMax);
        nextLength = Random.Range(lengthMin, lengthMax);
    }

    // ---------- 小球定位 ----------
    Vector3 GetBallPositionOnVerticalAxis(GameObject rect)
    {
        PolygonCollider2D poly = rect.GetComponent<PolygonCollider2D>();
        if (poly == null)
        {
            Debug.LogWarning("矩形缺少 PolygonCollider2D，使用后备位置");
            return new Vector3(0, rect.transform.position.y, 0);
        }

        Transform rectTransform = rect.transform;
        List<Vector2> intersections = new List<Vector2>();

        Vector2[] localPoints = poly.points;
        Vector3[] worldPoints = new Vector3[localPoints.Length];
        for (int i = 0; i < localPoints.Length; i++)
            worldPoints[i] = rectTransform.TransformPoint(localPoints[i]);

        for (int i = 0; i < worldPoints.Length; i++)
        {
            int j = (i + 1) % worldPoints.Length;
            Vector3 p1 = worldPoints[i];
            Vector3 p2 = worldPoints[j];
            Vector2? intersect = LineIntersectionWithVertical(p1, p2, 0f);
            if (intersect.HasValue)
                intersections.Add(intersect.Value);
        }

        if (intersections.Count >= 2)
        {
            Vector2 sum = Vector2.zero;
            foreach (var p in intersections) sum += p;
            Vector2 mid = sum / intersections.Count;
            return new Vector3(mid.x, mid.y, 0);
        }
        else if (intersections.Count == 1)
            return new Vector3(intersections[0].x, intersections[0].y, 0);
        else
            return Vector3.zero;
    }

    Vector2? LineIntersectionWithVertical(Vector3 p1, Vector3 p2, float verticalX)
    {
        if ((p1.x - verticalX) * (p2.x - verticalX) > 0)
            return null;
        if (Mathf.Approximately(p1.x, p2.x) && Mathf.Approximately(p1.x, verticalX))
            return new Vector2(p1.x, (p1.y + p2.y) / 2);
        float t = (verticalX - p1.x) / (p2.x - p1.x);
        float y = p1.y + t * (p2.y - p1.y);
        return new Vector2(verticalX, y);
    }

    // ---------- 缩回 + 预警 ----------
    IEnumerator ShrinkAndDestroy(GameObject rect, float delay, float shrinkTime,
                                 Vector3 warningPos, float warningAngle, float warningLength)
    {
        yield return new WaitForSeconds(delay);

        GameObject warning = null;
        if (warningPrefab != null)
        {
            warning = Instantiate(warningPrefab, warningPos, Quaternion.identity);
            warning.transform.eulerAngles = new Vector3(0, 0, warningAngle);
            warning.transform.localScale = new Vector3(warningLength, width, 1);
            spawnedObjects.Add(warning);
            StartCoroutine(DestroyWarningAfterDelay(warning, warningDuration));
        }

        float startScaleX = rect.transform.localScale.x;
        float elapsed = 0f;
        while (elapsed < shrinkTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkTime;
            float newX = Mathf.Lerp(startScaleX, 0, t);
            rect.transform.localScale = new Vector3(newX, rect.transform.localScale.y, 1);
            yield return null;
        }
        rect.transform.localScale = new Vector3(0, rect.transform.localScale.y, 1);
        Destroy(rect);
    }

    IEnumerator DestroyWarningAfterDelay(GameObject warning, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (warning != null)
            Destroy(warning);
    }

    // ---------- 重置 ----------
    public void ResetSpawner()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        consecutiveSameSide = 0;
        lastSideLeft = false;
        isFirstSpawn = true;

        PrepareNextSpawn();
        spawnCoroutine = StartCoroutine(SpawnRoutine());
        Debug.Log("🔄 Spawner 已重置");
    }

    // ---------- 音效辅助 ----------
    IEnumerator PlaySoundDelayed(AudioSource source, float startTime, float delay, bool loop)
    {
        if (source == null || source.clip == null) yield break;
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);
        source.time = startTime;
        source.loop = loop;
        source.Play();
    }
}