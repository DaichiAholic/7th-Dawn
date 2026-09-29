using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    /// <summary>
    /// The night as a clock: you head out at dusk (8:00 PM) and the holy light returns at
    /// dawn (6:00 AM). Everything that takes time is measured in minutes - entering a room,
    /// searching a cache, resting - so the HUD can show a real time of night instead of an
    /// abstract budget. Combat doesn't touch the clock.
    /// </summary>
    public class DawnTimer
    {
        public const int DuskHour = 20; // 8:00 PM
        public const int DawnHour = 6;  // 6:00 AM
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

        /// <summary>Turns the clock back (Dawn Tincture). Never earlier than dusk.</summary>
        public void Restore(int minutes)
        {
            MinutesLeft = Math.Min(MaxMinutes, MinutesLeft + minutes);
        }

        /// <summary>Minutes since midnight for a point in the night, given how far into it
        /// you are. Wraps past midnight.</summary>
        public static int ClockMinutesAt(float minutesElapsed) =>
            (int)((DuskHour * 60 + minutesElapsed) % (24 * 60));

        /// <summary>Current time of night, e.g. "11:30 PM".</summary>
        public string ClockLabel => FormatClock(ClockMinutesAt(MinutesElapsed));

        /// <summary>What the clock will read after spending this many more minutes.</summary>
        public string ClockLabelAfter(int minutes) => FormatClock(ClockMinutesAt(Math.Min(MaxMinutes, MinutesElapsed + minutes)));

        public static string FormatClock(int minutesSinceMidnight)
        {
            int hour24 = minutesSinceMidnight / 60 % 24;
            int minute = minutesSinceMidnight % 60;
            int hour12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
            return $"{hour12}:{minute:00} {(hour24 < 12 ? "AM" : "PM")}";
        }

        /// <summary>"45m", "2h", "1h 30m".</summary>
        public static string FormatDuration(int minutes)
        {
            int h = minutes / 60, m = minutes % 60;
            if (h == 0) return $"{m}m";
            return m == 0 ? $"{h}h" : $"{h}h {m}m";
        }
    }
}
