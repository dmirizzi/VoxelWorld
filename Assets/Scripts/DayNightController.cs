using System;
using UnityEngine;

public class DayNightController : MonoBehaviour
{
    private const float SecPerDay = 24 * 60 * 60;

    public float CurrentTimeSec;

    public float TimeFactor = 60.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CurrentTimeSec += TimeFactor * Time.deltaTime;
        if(CurrentTimeSec > SecPerDay) CurrentTimeSec -= SecPerDay;
    }
}
