/*
 * Copyright (c) 2015, Roger Lew (rogerlew.gmail.com)
 * Date: 6/16/2015
 * License: BSD (3-clause license)
 * 
 * The project described was supported by NSF award number IIA-1301792
 * from the NSF Idaho EPSCoR Program and by the National Science Foundation.
 * 
 * This module provides functionality for reading daily temperature timeseries
 * obtained from the NOAA National Climatic Data Center (NCDC).
 *    https://www.ncdc.noaa.gov/data-access/land-based-station-data
 * 
 * The module interpolates daily minimum and maximum temperature values using the
 * same algorithm as the interp.T package for R [1]. Some of the assumptions that
 * weren't explicitly listed in their paper [2] were taken from the original 
 * algorithm [3].
 * 
 * 
 * References:
 * [1] Eccel, E. & Cordano, C. (2013). Interpol.T: Hourly interpolation of multiple
 *     temperature daily series.
 *     http://cran.r-project.org/web/packages/Interpol.T/index.html
 *     
 * [2] Eccel, E. (2010). What we can ask to hourly temperature recording. Part II:
 *     Hourly interpolation of temperatures for climatology and modelling. 
 *     http://www.agrometeorologia.it/documenti/Rivista2010_2/AIAM%202-2010_pag45.pdf
 * [3] Cesaraccio, C., Spano, D., Duce, P., Snyder, R. L. (2001). An improved model
 *     for determining degree-day values from daily temperature data. Int. J. 
 *     Biometeorol, 45, 161-169.
 *     http://download.springer.com/static/pdf/789/art%253A10.1007%252Fs004840100104.pdf?originUrl=http%3A%2F%2Flink.springer.com%2Farticle%2F10.1007%2Fs004840100104&token2=exp=1434487094~acl=%2Fstatic%2Fpdf%2F789%2Fart%25253A10.1007%25252Fs004840100104.pdf%3ForiginUrl%3Dhttp%253A%252F%252Flink.springer.com%252Farticle%252F10.1007%252Fs004840100104*~hmac=efc7b39f531ab587adda01b525aa189cf9b449a49b2a50b4f1b48d811cc72cc9
 */

using UnityEngine;
using System;
using System.Collections.Generic;

using VTL.IO;
using VTL.SolarLunarTracking;

namespace VTL.DailyTemperatureInterpolation
{

    class DailyTemperature
    {       
        // this was developed in Python and ported to C#, hence all the public variables

        public float tmin;
        public float tmax;
        public DateTime date;
        public Solar solar;
        public float c;
        public float z; //  clear day -> 0.5, cloudy -> 1
        public float HxHsInterval;
        public float HnHsInterval;
        public DailyTemperature nextDay;
        public DailyTemperature prevDay;

        TimeSpan _24hr = new TimeSpan(24, 0, 0);
        const float pi = Mathf.PI;

        // Using Properties to calculate things on the fly
        // and translate from climate variables (e.g. tmax, tmin) to model variables (Tp, Tn) etc.
        // Yes, this isn't the most efficient way of doing this

        float? TpProperty;
        public float? Tp
        {
            get
            {
                if (nextDay == null)
                    return null;

                return nextDay.tmin;
            }
            set {  }
        }

        float? TsProperty;
        public float? Ts
        {
            get { return tmax - c * (tmax - tmin); }
            set { }
        }

        float? TnProperty;
        public float? Tn
        {
            get { return tmin; }
            set { }
        }

        float? TxProperty;
        public float? Tx
        {
            get { return tmax; }
            set { }
        }

        DateTime? HsProperty;
        public DateTime? Hs
        {
            get { return solar.sunset; }
            set { }
        }

        DateTime? HxProperty;
        public DateTime? Hx
        {
            get { return solar.sunset - new TimeSpan(0, 0, (int)(HxHsInterval * 3600f)); }
            set { }
        }

        DateTime? HnProperty;
        public DateTime? Hn
        {
            get { return solar.sunrise - new TimeSpan(0, 0, (int)(HnHsInterval * 3600f)); }
            set { }
        }

        DateTime? HpProperty;
        public DateTime? Hp
        {
            get
            {
                if (nextDay == null)
                    return null;

                return nextDay.solar.sunrise;
            }
            set { }
        }

        DateTime? H0Property;
        public DateTime? H0
        {
            get { return date; }
            set { }
        }

        DateTime? HendProperty;
        public DateTime? Hend
        {
            get { return H0 + _24hr; }
            set { }
        }

        public DailyTemperature(float tmin, float tmax, DateTime date, Solar solarDict, 
                              float c = 0.39f, float z = 0.5f, 
                              float HxHsInterval = 4, float HnHsInterval = 1)
        {
            this.tmin = tmin;
            this.tmax = tmax;
            this.date = date;
            this.solar = solarDict;
            this.c = c;
            this.z = z;
            this.HxHsInterval = HxHsInterval;
            this.HnHsInterval = HnHsInterval;

        }

        static float total_hours(TimeSpan? tdelta)
        {
            return (float)((TimeSpan)tdelta).TotalHours;
        }

        public float? Call(DateTime h)
        {

            if (Hn < h && h <= Hx) // sunrise to max temp (4 hours before sunset)
            {
                float t_frac = total_hours(h - Hn) / total_hours(Hx - Hn);
                return Tn + ((Tx - Tn) / 2.0f) * (1f + Mathf.Sin(pi * t_frac - (pi/2f)));
            }
            else if (Hx < h && h <= Hs) // max temp to sunset
            {
                float t_frac = total_hours(h - Hx) / total_hours(Hs - Hx);
                return Ts + (Tx - Ts) * Mathf.Sin((pi / 2) * (1 + t_frac));
            }
            else if (Hs < h && h <= Hend) // sunset to midnight
            {
                var Tn_next = nextDay.Tn;
                var deltaII = (Tn_next - Ts) / Mathf.Pow(total_hours(Hn + _24hr - Hs), z);
                return Ts + deltaII * Mathf.Pow(total_hours(h - Hs), z);
            }
            else // midnight previous day to sunrise
            {
                var Ts_prev = prevDay.Ts;
                var deltaI = (Tn - Ts_prev) / Mathf.Pow(total_hours(Hn + _24hr - Hs), z);
                return Ts_prev + deltaI * Mathf.Pow(total_hours(h + _24hr - Hs), z);
            }
        }
    }

    class DailyTemperatureSeries
    {
        Dictionary<DateTime, DailyTemperature> series;
        
        public DailyTemperatureSeries(string fname, Astral astral_location)
        {
            series = new Dictionary<DateTime, DailyTemperature>();

            DictReader dictReader = new DictReader(fname);

            DateTime? last = null;
            //int i = 0;
            foreach (var row in dictReader)
            {
                var date_str = row["DATE"];
                int year = System.Convert.ToInt32(date_str.Substring(0,4));
                int month = System.Convert.ToInt32(date_str.Substring(4,2));
                int day = System.Convert.ToInt32(date_str.Substring(6,2));
                DateTime date = new DateTime(year, month, day);

                var tmin = System.Convert.ToSingle(row["TMIN"]) * 0.1f;
                var tmax = System.Convert.ToSingle(row["TMAX"]) * 0.1f;

                var utcOffset = astral_location.utcOffsetProperty;

                // adjust time for utc_offset and set to noon
                var solar = astral_location.solar(date - utcOffset + new TimeSpan(12, 0, 0));
                series.Add(date, new DailyTemperature(tmin, tmax, date, solar));
                
                if (series.Count > 1)
                {
                    series[(DateTime)last].nextDay = series[date];
                    series[date].prevDay = series[(DateTime)last];
                }
                last = date;

                //if (i < 10)
                //{
                //    Debug.Log("\nH0: " + series[date].H0);
                //    Debug.Log("Hn: " + series[date].Hn);
                //    Debug.Log("Hx: " + series[date].Hx);
                //    Debug.Log("Hs: " + series[date].Hs);
                //    Debug.Log("Hend: " + series[date].Hend);
                //}
                //i++;
            }
        }

        void Add(DateTime date, DailyTemperature dt)
        {
            series.Add(date, dt);
        }

        static DateTime Datekey(DateTime h)
        {
            return new DateTime(h.Year, h.Month, h.Day);
        }

        public float? Call(DateTime h)
        {
            var date = Datekey(h) ;

            if (!series.ContainsKey(date))
            {
                Debug.LogWarning("Series does not contain date" + date.ToString());
                return null;
            }
            return series[date].Call(h);
        }
    }

}
