using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class DawnTimer
    {
        public const int DuskHour = 20;
        public const int DawnHour = 6;
        public const int NightMinutes = ((24 - DuskHour) + DawnHour) * 60;

        public int MaxMinutes { get; }
        public int MinutesLeft { get; private set; }
        public int MinutesElapsed => MaxMinutes - MinutesLeft;

        public bool HasTimeRemaining => MinutesLeft > 0;

        public DawnTimer(int nightMinutes = NightMinutes)
        {
            MaxMinutes = nightMinutes;
            MinutesLeft = nightMinutes;
        }

        public bool CanAfford(int minutes) => minutes <= MinutesLeft;

        public void Spend(int minutes)
        {
            MinutesLeft = Math.Max(0, MinutesLeft - minutes);
        }

        public void Restore(int minutes)
        {
            MinutesLeft = Math.Min(MaxMinutes, MinutesLeft + minutes);
        }

        public static int ClockMinutesAt(float minutesElapsed) =>
            (int)((DuskHour * 60 + minutesElapsed) % (24 * 60));

        public string ClockLabel => FormatClock(ClockMinutesAt(MinutesElapsed));

        public string ClockLabelAfter(int minutes) => FormatClock(ClockMinutesAt(Math.Min(MaxMinutes, MinutesElapsed + minutes)));

        public static string FormatClock(int minutesSinceMidnight)
        {
            int hour24 = minutesSinceMidnight / 60 % 24;
            int minute = minutesSinceMidnight % 60;
            int hour12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
            return $"{hour12}:{minute:00} {(hour24 < 12 ? "AM" : "PM")}";
        }

        public static string FormatDuration(int minutes)
        {
            int h = minutes / 60, m = minutes % 60;
            if (h == 0) return $"{m}m";
            return m == 0 ? $"{h}h" : $"{h}h {m}m";
        }
    }
}
