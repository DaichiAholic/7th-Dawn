namespace DuskAndDawn
{
    public static class DayInfo
    {
        public const int FinalDay = 7;

        public static bool IsFinalNight(int day) => day >= FinalDay;

        public static string Label(int day) => $"Day {day} of {FinalDay}";

        public static int EncounterWeightBonus(int day) => 4 * (day - 1);

        public static int DawnDread(int day) => 1 + day;
    }
}
