using UnityEngine;
using UnityEngine.UI;

public class EnvironmentClockHud : MonoBehaviour
{
    [SerializeField] private EnvironmentCycle environment;
    [SerializeField] private Text label;
    private int lastMinute = -1;
    private int lastDay = -1;

    private void Update()
    {
        if (environment == null || label == null) return;
        int totalMinutes = Mathf.FloorToInt(environment.Hour * 60f);
        if (lastMinute == totalMinutes && lastDay == environment.Day) return;
        lastMinute = totalMinutes;
        lastDay = environment.Day;
        string phase = environment.IsNight ? "NIGHT" : environment.Hour < 12f ? "MORNING" : environment.Hour < 17f ? "AFTERNOON" : "DUSK";
        label.text = $"DAY {environment.Day}   {totalMinutes / 60:00}:{totalMinutes % 60:00}\n{phase}";
    }
}
