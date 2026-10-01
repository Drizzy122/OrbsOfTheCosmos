using System;
using ImprovedTimers;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Material skyboxMaterial;
    // One full Minecraft day: 24000 ticks at 20 ticks/sec = 20 real minutes.
    public float cycleDuration = 1200f;

    [Header("Day")]
    public Color dayZenith = new Color(0.53f, 0.81f, 0.98f);
    public Color dayHorizon = new Color(0.73f, 0.89f, 1f);
    public float dayIntensity = 1f;

    [Header("Night")]
    public Color nightZenith = new Color(0.01f, 0.01f, 0.07f);
    public Color nightHorizon = new Color(0.01f, 0.01f, 0.05f);
    public float nightIntensity = 0.05f;

    CountdownTimer cycleTimer;
    Material skyInstance;

    /// <summary>Full days completed since the cycle started.</summary>
    public int DaysElapsed { get; private set; }

    /// <summary>0 at the start of a cycle, approaching 1 at its end.</summary>
    public float CycleProgress => cycleTimer == null ? 0f : 1f - cycleTimer.Progress;

    /// <summary>True across the dark half of the cycle.</summary>
    public bool IsNight => DarknessAt(CycleProgress) > 0.5f;

    /// <summary>Fires each time a full cycle completes, just before the next one starts.</summary>
    public event Action OnNewDay;

    void Awake()
    {
        if (skyboxMaterial != null)
        {
            skyInstance = new Material(skyboxMaterial);
            RenderSettings.skybox = skyInstance;
        }

        cycleTimer = new CountdownTimer(cycleDuration);
        cycleTimer.OnTimerStop += HandleCycleComplete;
    }

    void OnEnable()
    {
        if (cycleTimer == null || cycleTimer.IsRunning) return;
        if (cycleTimer.CurrentTime <= 0f) cycleTimer.Start();
        else cycleTimer.Resume();
    }
    void OnDisable() => cycleTimer?.Pause();

    void OnDestroy()
    {
        if (cycleTimer != null)
        {
            cycleTimer.OnTimerStop -= HandleCycleComplete;
            cycleTimer.Dispose();
        }
        if (skyInstance != null) Destroy(skyInstance);
    }

    void HandleCycleComplete()
    {
        DaysElapsed++;
        OnNewDay?.Invoke();
        cycleTimer.Start();
    }

    void Update()
    {
        float angle = CycleProgress * 360f;
        ApplyCycle(angle, DarknessAt(CycleProgress));
    }

    /// <summary>0 = full day, 1 = full night. Peaks at the midpoint of the cycle.</summary>
    static float DarknessAt(float progress) => Mathf.PingPong(progress * 2f, 1f);

    void ApplyCycle(float angle, float t)
    {
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        }

        if (skyInstance == null) return;

        skyInstance.SetColor("_ZenithColor", Color.Lerp(dayZenith, nightZenith, t));
        skyInstance.SetColor("_HorizonColor", Color.Lerp(dayHorizon, nightHorizon, t));
        skyInstance.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.5f, 1f, t));
        skyInstance.SetFloat("_EnableStars", Mathf.Lerp(0f, 1f, t));
    }

#if UNITY_EDITOR
    // Lets you retune cycleDuration in play mode without restarting the cycle.
    void OnValidate()
    {
        if (Application.isPlaying && cycleTimer != null && cycleDuration > 0f)
        {
            cycleTimer.Reset(cycleDuration);
        }
    }
#endif
}
