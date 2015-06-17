using UnityEngine;
using System;
using System.Collections;

using VTL.TrendGraph;
using VTL.SolarLunarTracking;
using VTL.SimTimeControls;
using VTL.DailyTemperatureInterpolation;

public class DailyTemperatureInterpolationTrendGraphAdapter : MonoBehaviour
{
    TrendGraphController trendgraphController;
    TimeSlider timeSlider;

    Astral astral_loc;
    public double longitude;
    public double latitude;
    public double UTC_timezone;

    DailyTemperatureSeries dailyTemperatureSeries;
    public string dailyTempDataResourceLocation = "KenaiDailyTemperature";

    public float updateRate = 0.2f;

    // Use this for initialization
    void Start()
    {
        trendgraphController = GetComponent<TrendGraphController>();
        if (trendgraphController == null)
            throw new System.Exception("Gameobject does not have TrendgraphController");

        timeSlider = Transform.FindObjectOfType<TimeSlider>();
        if (timeSlider == null)
            throw new System.Exception("Scene does not contain a TimeSlider");

        astral_loc = new Astral(longitude, latitude, 0, UTC_timezone);

        dailyTemperatureSeries = new DailyTemperatureSeries(dailyTempDataResourceLocation, astral_loc);

        StartCoroutine("SlowUpdate");
    }

    IEnumerator SlowUpdate()
    {
        while (true)
        {
            DateTime date = timeSlider.SimTime;
            float? temperature = dailyTemperatureSeries.Call(date);

            if (temperature != null)
                trendgraphController.Add(date, (float)temperature);
            yield return new WaitForSeconds(updateRate);
        }
    }
}
