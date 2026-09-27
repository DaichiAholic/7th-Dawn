namespace DuskAndDawn
{
    /// <summary>
    /// The run's goal and pacing: hold the house together until the seventh dawn. Every day
    /// the ruins get more dangerous and the dread weighs a little heavier, so the base you
    /// build has to keep ahead of the curve. Night 7 is the last - the Knight waits in it.
    /// </summary>
    public static class DayInfo
    {
        public const int FinalDay = 7;

        public static bool IsFinalNight(int day) => day >= FinalDay;

        public static string Label(int day) => $"Day {day} of {FinalDay}";

        // ---- Enemies ----
        // Day 1 is the tuned baseline; by day 7 enemies have ~48% more health and hit 3 harder.
        public static float EnemyHealthMultiplier(int day) => 1f + 0.08f * (day - 1);
        public static int EnemyAttackBonus(int day) => (day - 1) / 2;

        /// <summary>% chance a Wretch winds up a heavy blow instead of a normal hit.</summary>
        public static int WretchChargeChance(int day) => 20 + 3 * day;

        /// <summary>Added to the maze's encounter weight - more of the ruins stir each night.</summary>
        public static int EncounterWeightBonus(int day) => 4 * (day - 1);

        // ---- Hope ----
        /// <summary>Hope lost to dread at the dawn that begins `day` - the longer this goes
        /// on, the harder it is to believe it will end.</summary>
        public static int DawnDread(int day) => 1 + day;
    }
}
