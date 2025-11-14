using System;
using System.Data;
using System.Globalization;

public static class HebrewDateHelper
{
    private static readonly HebrewCalendar hebrewCalendar = new HebrewCalendar();

    private static readonly string[] torahMonths = {
        "", "Nisan", "Iyar", "Sivan", "Tamuz", "Av", "Elul",
        "Tishrei", "Cheshvan", "Kislev", "Tevet", "Shevat", "Adar"
    };

    public static string ToHebrewDateString(DateTime gregorianDate)
    {
        int civilMonth = hebrewCalendar.GetMonth(gregorianDate);
        int day = hebrewCalendar.GetDayOfMonth(gregorianDate);
        int year = hebrewCalendar.GetYear(gregorianDate);

        // Torah-based month number
        int torahMonth = MapToTorahMonth(civilMonth, year);

        // Handle leap year Adar I / II
        if (torahMonth == 12 && hebrewCalendar.IsLeapYear(year))
        {
            if (civilMonth == 6) return $"{day} Adar I {year}";
            if (civilMonth == 7) return $"{day} Adar II {year}";
        }

        return $"{day} {torahMonths[torahMonth]} {year}";
    }

    public static bool IsYomTov(DateTime gregorianDate)
    {
        int civilMonth = hebrewCalendar.GetMonth(gregorianDate);
        int day = hebrewCalendar.GetDayOfMonth(gregorianDate);
        int year = hebrewCalendar.GetYear(gregorianDate);

        int torahMonth = MapToTorahMonth(civilMonth, year);

        // Yom Tovim (Israel custom, Torah month numbering)
        return
            // Pesach (15 Nisan, 21 Nisan)
            (torahMonth == 1 && (day == 15 || day == 21)) ||
            // Shavuot (6 Sivan)
            (torahMonth == 3 && day == 6) ||
            // Rosh Hashana (1,2 Tishrei = month 7)
            (torahMonth == 7 && (day == 1 || day == 2)) ||
            // Yom Kippur (10 Tishrei)
            (torahMonth == 7 && day == 10) ||
            // Sukkot (15 Tishrei + Shemini Atzeret 22 Tishrei)
            (torahMonth == 7 && (day == 15 || day == 22)) ||
            // Shabbos
            DateTime.Today.DayOfWeek == DayOfWeek.Saturday;
    }

    public static bool IsErevShabbosOrYomTov(DateTime gregorianDate)
    {
        DateTime today = gregorianDate;
        DateTime nextDay = gregorianDate.AddDays(1);
        return today.DayOfWeek == DayOfWeek.Friday && !IsYomTov(gregorianDate) || IsYomTov(nextDay) && !IsYomTov(gregorianDate);
    }
    private static int MapToTorahMonth(int civilMonth, int year)
    {
        // Civil months: Tishrei=1, Cheshvan=2, ..., Elul=12 (Adar I=6, Adar II=7 in leap year)
        // Torah months: Nisan=1, ..., Adar=12
        if (civilMonth >= 7) // Nisan to Elul
            return civilMonth - 6;
        else // Tishrei to Adar
            return civilMonth + 6;
    }
}
