using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DayNightController : MonoBehaviour
{
    private const float SecPerDay = 24 * 60 * 60;

    public float CurrentTimeSec;

    public float TimeFactor = 60.0f;

    public float SunLightIntensityFactor = 1.5f;

    public Light SunLight;

    public Texture2D SkyGradient;

    public Texture2D SunLightGradient;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        CurrentTimeSec += TimeFactor * Time.deltaTime;
        if(CurrentTimeSec >= SecPerDay) CurrentTimeSec -= SecPerDay;

        var currentSkyBoxColor = GetColorForTime(SkyGradient);
        RenderSettings.skybox.SetColor("_Tint", currentSkyBoxColor);

        var skyBoxBrightness = (currentSkyBoxColor.r + currentSkyBoxColor.g + currentSkyBoxColor.b) / 3.0f;
        RenderSettings.skybox.SetFloat("_Exposure", skyBoxBrightness);
        
        var sunLightColor = GetColorForTime(SunLightGradient);
        SunLight.color = sunLightColor;

        var sunLightBrightness = (sunLightColor.r + sunLightColor.g + sunLightColor.b) / 3.0f;
        SunLight.intensity = sunLightBrightness * SunLightIntensityFactor;
    }

    private Color GetColorForTime(Texture2D gradient)
    {
        var currentTimeInHrs = CurrentTimeSec / 3600f;

        int hr1 = (int)currentTimeInHrs;
        int hr2 = (hr1 + 1) % 24;
        float lerp = GetTimeDiff(hr1, currentTimeInHrs) / GetTimeDiff(hr1, hr2);

        int pixelsPerHr = SkyGradient.width / 24;

        return Color.Lerp(
            gradient.GetPixel(hr1 * pixelsPerHr, 0),
            gradient.GetPixel(hr2 * pixelsPerHr, 0),
            lerp
        );  
    }

    private float GetTimeDiff(float hr1, float hr2)
    {
        if(hr1 > hr2)
        {
            hr1 -= 24;
        }
        return hr2 - hr1;
    }
}
