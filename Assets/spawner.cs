using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject rectanglePrefabNormal;   // 原版（掉落 +1）
    public GameObject rectanglePrefabPlus2;    // 掉落 +2
    public GameObject rectanglePrefabPlus4;    // 掉落 +4
    public GameObject warningPrefab;
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
    public float shrinkDuration = 0.2f;        // 缩回时间（不影响预警）

    [Header("Warning Settings")]
    public float warningLeadTime = 0.2f;        // 预警在障碍物出现前多少秒出现并淡入至100%

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
        // 初始化第一次参数
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

    // ---------- 根据进度选择障碍物类型 ----------
    ObstacleType SelectObstacleType(float progress)
    {
        if (progress < 0.5f)
        {
            float t = progress / 0.5f;
            float p4 = 1 - t;
            float p2 = t;
            float rand = Random.value;
            if (rand < p4)
                return ObstacleType.Plus4;
            else
                return ObstacleType.Plus2;
        }
        else
        {
            float t = (progress - 0.5f) / 0.5f;
            float p2 = 1 - t;
            float p1 = t;
            float rand = Random.value;
            if (rand < p2)
                return ObstacleType.Plus2;
            else
                return ObstacleType.Normal;
        }
    }

    // ---------- 生成协程（修复预警时机） ----------
    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            // 1. 获取本次间隔（用于等待和生成预警参考）
            float interval = GetDynamicInterval();

            // 2. 等待间隔（此时下一个障碍物将在 interval 秒后生成）
            yield return new WaitForSeconds(interval);

            // 3. 使用当前的 nextSpawnPos 生成障碍物（这些参数在循环外部或上一次循环中已经设置）
            Vector3 currentPos = nextSpawnPos;
            float currentAngle = nextAngle;
            float currentLength = nextLength;

            // --- 生成障碍物 ---
            GameObject rectPrefab = GetRectanglePrefab(SelectObstacleType(GetProgress())); // 但类型应基于当前进度，这里重新计算，但应与之前一致
            // 实际上，为了准确，我们在生成前就计算类型，但为了复用，我们可以先确定类型。
            float currentProgress = GetProgress();
            ObstacleType type = SelectObstacleType(currentProgress);
            rectPrefab = GetRectanglePrefab(type);
            GameObject ballPrefab = GetBallPrefab(type);
            int bonus = GetBonus(type);

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

            // 4. 预计算下一次参数（用于预警和下一次生成）
            PrepareNextSpawn();
            // 获取下一次的间隔（作为下一次等待和预警的依据）
            float nextInterval = GetDynamicInterval();

            // 5. 启动预警协程（为下一次障碍物准备）
            if (warningPrefab != null && warningLeadTime > 0)
            {
                StartCoroutine(SpawnWarningForNext(nextInterval, nextSpawnPos, nextAngle, nextLength));
            }

            // 6. 缩回当前障碍物
            StartCoroutine(ShrinkRect(rect, lifeTime, shrinkDuration));

            // 7. 更新间隔为下一次间隔，用于下次循环等待（因为我们已经等待了本次 interval，下次等待应基于 nextInterval）
            // 但此处我们不需要额外操作，因为循环会重新获取间隔，但为了确保一致性，我们可以在循环末尾将 interval 设为 nextInterval？
            // 实际上，由于我们已经在循环开头获取了 interval，并等待了它，现在 nextInterval 是下次要用的，但下一次循环会重新计算，所以这里不赋值。
            // 但为了预警的精确性，我们已在预警协程中使用了 nextInterval，所以没问题。
        }
    }

    // ---------- 辅助：获取预制体 ----------
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

    // ---------- 预警生成协程（使用下一次间隔） ----------
    IEnumerator SpawnWarningForNext(float nextInterval, Vector3 pos, float angle, float length)
    {
        // 计算等待时间：在下次障碍物出现前 warningLeadTime 秒
        float waitTime = nextInterval - warningLeadTime;
        if (waitTime < 0) waitTime = 0;

        yield return new WaitForSeconds(waitTime);

        // 生成预警
        GameObject warning = Instantiate(warningPrefab, pos, Quaternion.identity);
        warning.transform.eulerAngles = new Vector3(0, 0, angle);
        warning.transform.localScale = new Vector3(length, width, 1);
        spawnedObjects.Add(warning);

        SpriteRenderer sr = warning.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // 初始透明
            Color c = sr.color;
            c.a = 0f;
            sr.color = c;

            // 淡入时间：应为 warningLeadTime，但不能超过 nextInterval（因为障碍物出现时需达到1）
            float fadeDuration = Mathf.Min(warningLeadTime, nextInterval);
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;
                Color c2 = sr.color;
                c2.a = Mathf.Lerp(0f, 1f, progress);
                sr.color = c2;
                yield return null;
            }
            // 确保达到1
            Color full = sr.color;
            full.a = 1f;
            sr.color = full;
        }

        // 淡入完成，立即销毁预警（因为此时障碍物已经出现）
        Destroy(warning);
    }

    // ---------- 缩回障碍物 ----------
    IEnumerator ShrinkRect(GameObject rect, float delay, float shrinkTime)
    {
        yield return new WaitForSeconds(delay);

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
}