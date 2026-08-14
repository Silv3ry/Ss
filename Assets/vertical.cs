using UnityEngine;
using UnityEngine.UI;

public class SliderProgress : MonoBehaviour
{
    public Slider slider;
    public PlayerController player;
    public float bottomY = 0f;
    public float topY = 10f;

    void Start()
    {
        if (slider == null) slider = GetComponent<Slider>();
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<PlayerController>();
        }
        if (player != null) bottomY = player.transform.position.y;
    }

    void Update()
    {
        if (player == null || slider == null) return;
        float winY = player.GetWinLineY();
        float range = winY - bottomY;
        if (range <= 0) { slider.value = 0; return; }
        float progress = (player.transform.position.y - bottomY) / range;
        slider.value = Mathf.Clamp01(progress);
    }
}