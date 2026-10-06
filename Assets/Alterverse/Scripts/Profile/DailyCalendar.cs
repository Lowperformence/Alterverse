using System;

namespace Alterverse.Profile
{
    public static class DailyCalendar
    {
        public static DateTime BerlinDate(DateTime utc)
        {
            utc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
            int year = utc.Year;
            DateTime start = LastSunday(year, 3).AddHours(1);
            DateTime end = LastSunday(year, 10).AddHours(1);
            return utc.AddHours(utc >= start && utc < end ? 2 : 1).Date;
        }

        private static DateTime LastSunday(int year, int month)
        {
            DateTime last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
            return last.AddDays(-(int)last.DayOfWeek);
        }

        public static int Stamp(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;
        public static int Today => Stamp(BerlinDate(DateTime.UtcNow));
        public static int Yesterday => Stamp(BerlinDate(DateTime.UtcNow).AddDays(-1));
    }
}
