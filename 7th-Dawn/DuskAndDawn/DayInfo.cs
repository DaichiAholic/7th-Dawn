namespace DuskAndDawn
{
    public static class DayInfo
    {
        public const int FinalDay = 7;

        public static bool IsFinalNight(int day) => day >= FinalDay;

        public static string Label(int day) => $"Day {day} of {FinalDay}";

        public static float EnemyHealthMultiplier(int day) => 1f + 0.08f * (day - 1);
        public static int EnemyAttackBonus(int day) => (day - 1) / 2;

        public static int WretchChargeChance(int day) => 20 + 3 * day;

        public static int EncounterWeightBonus(int day) => 4 * (day - 1);

        public static int DawnDread(int day) => 1 + day;
    }
}
