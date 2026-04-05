using Moq;
using MechanicalCataphract.Services.Calendar;

namespace MechanicalCataphract.Tests.Services;

[TestFixture]
public class CalendarServiceTests
{
    // Default calendar: H=24, W=7, 12 months=365 days/year, epoch (1024, month9=September, day1, weekday0=Monday)
    private static ICalendarService BuildDefault()
    {
        var def = CalendarDefinitionService.CreateHardcodedDefault();
        var mock = new Mock<ICalendarDefinitionService>();
        mock.Setup(s => s.GetCalendarDefinition()).Returns(def);
        return new CalendarService(mock.Object);
    }

    // Custom calendar: H=10, W=5, months 3x10=30 days/year
    private static ICalendarService BuildCustom(
        int hoursPerDay = 10,
        int weekdays = 5,
        int monthCount = 3,
        int daysPerMonth = 10,
        int epochYear = 0,
        int epochMonth = 1,
        int epochDay = 1,
        int epochWeekday = 0)
    {
        var weekdayNames = new List<string>();
        for (int i = 0; i < weekdays; i++) weekdayNames.Add($"Day{i}");

        var months = new List<CalendarMonthDefinition>();
        for (int i = 0; i < monthCount; i++) months.Add(new CalendarMonthDefinition($"Month{i + 1}", daysPerMonth));

        var def = new CalendarDefinition(
            Name: "Test",
            HoursPerDay: hoursPerDay,
            WeekdayNames: weekdayNames,
            Months: months,
            EpochYear: epochYear,
            EpochMonth: epochMonth,
            EpochDay: epochDay,
            EpochWeekday: epochWeekday);

        var mock = new Mock<ICalendarDefinitionService>();
        mock.Setup(s => s.GetCalendarDefinition()).Returns(def);
        return new CalendarService(mock.Object);
    }

    // ── Epoch ────────────────────────────────────────────────────────────────

    [Test]
    public void WorldHour0_Returns_EpochDate_Hour0()
    {
        var svc = BuildDefault();
        var d = svc.GetDate(0);

        Assert.That(d.Year, Is.EqualTo(1024));
        Assert.That(d.MonthNumber, Is.EqualTo(9));
        Assert.That(d.MonthName, Is.EqualTo("September"));
        Assert.That(d.DayOfMonth, Is.EqualTo(1));
        Assert.That(d.DayOfYear, Is.EqualTo(244));
        Assert.That(d.WeekdayIndex, Is.EqualTo(0));
        Assert.That(d.WeekdayName, Is.EqualTo("Monday"));
        Assert.That(d.HourOfDay, Is.EqualTo(0));
        Assert.That(d.AbsoluteDayIndex, Is.EqualTo(0));
    }

    [Test]
    public void FormatDateTime_WorldHour0_ReturnsExpectedString()
    {
        var svc = BuildDefault();
        Assert.That(svc.FormatDateTime(0), Is.EqualTo("Year 1024, September 1, Monday, 00:00"));
    }

    // ── Hour-of-day rollover ─────────────────────────────────────────────────

    [Test]
    public void HourOfDay_RollsOver_At_HoursPerDay()
    {
        var svc = BuildDefault(); // H=24
        Assert.That(svc.GetHourOfDay(23), Is.EqualTo(23));
        Assert.That(svc.GetHourOfDay(24), Is.EqualTo(0));
        Assert.That(svc.GetHourOfDay(25), Is.EqualTo(1));
    }

    [Test]
    public void HourOfDay_NonStandardDay_Custom()
    {
        var svc = BuildCustom(hoursPerDay: 10); // H=10
        Assert.That(svc.GetHourOfDay(9), Is.EqualTo(9));
        Assert.That(svc.GetHourOfDay(10), Is.EqualTo(0));
        Assert.That(svc.GetHourOfDay(11), Is.EqualTo(1));
    }

    // ── Weekday wraps ────────────────────────────────────────────────────────

    [Test]
    public void Weekday_WrapsCorrectly_At_WeekdayCount()
    {
        var svc = BuildDefault(); // W=7
        // Day 0 = weekday 0 (Firstday), Day 6 = weekday 6 (Seventhday), Day 7 = weekday 0 again
        Assert.That(svc.GetDate(0).WeekdayIndex, Is.EqualTo(0));
        Assert.That(svc.GetDate(6 * 24).WeekdayIndex, Is.EqualTo(6));
        Assert.That(svc.GetDate(7 * 24).WeekdayIndex, Is.EqualTo(0));
    }

    [Test]
    public void Weekday_NonStandard_5DayWeek()
    {
        var svc = BuildCustom(hoursPerDay: 10, weekdays: 5);
        // day 4 → weekday 4, day 5 → weekday 0
        Assert.That(svc.GetDate(4 * 10).WeekdayIndex, Is.EqualTo(4));
        Assert.That(svc.GetDate(5 * 10).WeekdayIndex, Is.EqualTo(0));
    }

    // ── Month rollover ───────────────────────────────────────────────────────

    [Test]
    public void Month_RolloversCorrectly()
    {
        var svc = BuildDefault(); // epoch = Sept 1, September has 30 days
        // Day 30 from epoch = first day of October
        var d = svc.GetDate(30L * 24);
        Assert.That(d.MonthNumber, Is.EqualTo(10));
        Assert.That(d.MonthName, Is.EqualTo("October"));
        Assert.That(d.DayOfMonth, Is.EqualTo(1));
    }

    [Test]
    public void Month_LastDayOfMonth1_Correct()
    {
        var svc = BuildDefault();
        var d = svc.GetDate(29L * 24); // day index 29 = last day of September (30 days)
        Assert.That(d.MonthNumber, Is.EqualTo(9));
        Assert.That(d.DayOfMonth, Is.EqualTo(30));
    }

    // ── Year rollover ────────────────────────────────────────────────────────

    [Test]
    public void Year_RolloversAfterFullYear()
    {
        var svc = BuildDefault(); // 365 days/year, epoch = Sept 1
        // 122 days from Sept 1 = Jan 1, 1025
        var d = svc.GetDate(122L * 24);
        Assert.That(d.Year, Is.EqualTo(1025));
        Assert.That(d.MonthNumber, Is.EqualTo(1));
        Assert.That(d.DayOfMonth, Is.EqualTo(1));
    }

    [Test]
    public void Year_LastDayOfYear_Correct()
    {
        var svc = BuildDefault();
        // 121 days from Sept 1 = Dec 31, 1024
        var d = svc.GetDate(121L * 24);
        Assert.That(d.Year, Is.EqualTo(1024));
        Assert.That(d.MonthNumber, Is.EqualTo(12));
        Assert.That(d.DayOfMonth, Is.EqualTo(31));
    }

    // ── Uneven month lengths ─────────────────────────────────────────────────

    [Test]
    public void UnevenMonths_CalculateCorrectly()
    {
        var def = new CalendarDefinition(
            Name: "Uneven",
            HoursPerDay: 24,
            WeekdayNames: new List<string> { "A", "B" },
            Months: new List<CalendarMonthDefinition>
            {
                new("Short", 5), new("Long", 15), new("Medium", 10)
            },
            EpochYear: 1, EpochMonth: 1, EpochDay: 1, EpochWeekday: 0);

        var mock = new Mock<ICalendarDefinitionService>();
        mock.Setup(s => s.GetCalendarDefinition()).Returns(def);
        var svc = new CalendarService(mock.Object);

        // day 5 = first day of Long
        var d = svc.GetDate(5L * 24);
        Assert.That(d.MonthNumber, Is.EqualTo(2));
        Assert.That(d.MonthName, Is.EqualTo("Long"));
        Assert.That(d.DayOfMonth, Is.EqualTo(1));

        // day 20 = first day of Medium
        var d2 = svc.GetDate(20L * 24);
        Assert.That(d2.MonthNumber, Is.EqualTo(3));
        Assert.That(d2.DayOfMonth, Is.EqualTo(1));
    }

    // ── AbsoluteDayIndex ─────────────────────────────────────────────────────

    [Test]
    public void GetAbsoluteDayIndex_DividesCorrectly()
    {
        var svc = BuildDefault(); // H=24
        Assert.That(svc.GetAbsoluteDayIndex(0), Is.EqualTo(0));
        Assert.That(svc.GetAbsoluteDayIndex(23), Is.EqualTo(0));
        Assert.That(svc.GetAbsoluteDayIndex(24), Is.EqualTo(1));
        Assert.That(svc.GetAbsoluteDayIndex(47), Is.EqualTo(1));
        Assert.That(svc.GetAbsoluteDayIndex(48), Is.EqualTo(2));
    }

    // ── CrossedDayBoundary ───────────────────────────────────────────────────

    [Test]
    public void CrossedDayBoundary_FalseWithinSameDay()
    {
        var svc = BuildDefault();
        Assert.That(svc.CrossedDayBoundary(0, 23), Is.False);
        Assert.That(svc.CrossedDayBoundary(5, 10), Is.False);
    }

    [Test]
    public void CrossedDayBoundary_TrueOnDayChange()
    {
        var svc = BuildDefault();
        Assert.That(svc.CrossedDayBoundary(23, 24), Is.True);
        Assert.That(svc.CrossedDayBoundary(0, 24), Is.True);
    }

    // ── CrossedHourOfDay ─────────────────────────────────────────────────────

    [Test]
    public void CrossedHourOfDay_FalseWhenNotCrossed()
    {
        var svc = BuildDefault(); // H=24
        // Advance from hour 5 to 10 — does not cross hour 21
        Assert.That(svc.CrossedHourOfDay(5, 10, 21), Is.False);
    }

    [Test]
    public void CrossedHourOfDay_TrueWhenExactHourCrossed()
    {
        var svc = BuildDefault();
        // Advance from hour 20 to 22 — crosses hour 21
        Assert.That(svc.CrossedHourOfDay(20, 22, 21), Is.True);
    }

    [Test]
    public void CrossedHourOfDay_TrueWhenNewEqualsTargetHour()
    {
        var svc = BuildDefault();
        // Advance from hour 20 to exactly 21 — crosses hour 21
        Assert.That(svc.CrossedHourOfDay(20, 21, 21), Is.True);
    }

    [Test]
    public void CrossedHourOfDay_FalseWhenOldEqualsTargetHour()
    {
        var svc = BuildDefault();
        // old=21 already past target, only [22..22] checked
        Assert.That(svc.CrossedHourOfDay(21, 22, 21), Is.False);
    }

    [Test]
    public void CrossedHourOfDay_TrueAcrossDayBoundary()
    {
        var svc = BuildDefault();
        // Advance from hour 22 to hour 26 (next day hour 2)
        // Crosses hour 23, 0, 1, 2 — targeting hour 0
        Assert.That(svc.CrossedHourOfDay(22, 26, 0), Is.True);
        // Targeting hour 6 — not yet reached
        Assert.That(svc.CrossedHourOfDay(22, 26, 6), Is.False);
    }

    [Test]
    public void CrossedHourOfDay_TrueForMultiDayAdvance()
    {
        var svc = BuildDefault();
        // Advance 72 hours (3 days) — spans more than H, hits every hour
        Assert.That(svc.CrossedHourOfDay(0, 72, 6), Is.True);
        Assert.That(svc.CrossedHourOfDay(0, 72, 21), Is.True);
    }

    [Test]
    public void CrossedHourOfDay_FalseWhenNewEqualsOld()
    {
        var svc = BuildDefault();
        Assert.That(svc.CrossedHourOfDay(10, 10, 10), Is.False);
    }

    [Test]
    public void CrossedHourOfDay_NonStandardDay_Custom()
    {
        // H=10, target hour 0 (start of each day)
        var svc = BuildCustom(hoursPerDay: 10);
        // From worldHour 8 to 12: crosses day boundary at 10, so hour 0 at worldHour 10
        Assert.That(svc.CrossedHourOfDay(8, 12, 0), Is.True);
        // hour 5 is within new day (10..14), crosses at worldHour 15
        Assert.That(svc.CrossedHourOfDay(8, 12, 5), Is.False);
        // hour 5 crossed at worldHour 15
        Assert.That(svc.CrossedHourOfDay(8, 15, 5), Is.True);
    }

    // ── GetWorldHour (reverse conversion) ────────────────────────────────────

    [Test]
    public void GetWorldHour_AtEpoch_ReturnsZero()
    {
        var svc = BuildDefault();
        Assert.That(svc.GetWorldHour(1024, 9, 1, 0), Is.EqualTo(0));
    }

    [Test]
    public void GetWorldHour_RoundTrips_WithGetDate()
    {
        var svc = BuildDefault();
        for (long wh = 0; wh < 10000; wh += 137)
        {
            var date = svc.GetDate(wh);
            long computed = svc.GetWorldHour(date.Year, date.MonthNumber, date.DayOfMonth, date.HourOfDay);
            Assert.That(computed, Is.EqualTo(wh), $"Round-trip failed for worldHour={wh}");
        }
    }

    [Test]
    public void GetWorldHour_CustomCalendar_RoundTrips()
    {
        var svc = BuildCustom();
        for (long wh = 0; wh < 500; wh += 17)
        {
            var date = svc.GetDate(wh);
            long computed = svc.GetWorldHour(date.Year, date.MonthNumber, date.DayOfMonth, date.HourOfDay);
            Assert.That(computed, Is.EqualTo(wh), $"Round-trip failed for worldHour={wh}");
        }
    }

    // ── Negative world hours ─────────────────────────────────────────────────

    [Test]
    public void GetDate_NegativeWorldHour_GivesPositiveHourOfDay()
    {
        var svc = BuildDefault(); // H=24, epoch = 1024 Sept 1
        // worldHour -1 should be hour 23 of the previous day
        var d = svc.GetDate(-1);
        Assert.That(d.HourOfDay, Is.EqualTo(23));
        Assert.That(d.AbsoluteDayIndex, Is.EqualTo(-1));
    }

    [Test]
    public void GetDate_NegativeWorldHour_GivesCorrectDate()
    {
        var svc = BuildDefault();
        // worldHour -24 = exactly one day before epoch = August 31, 1024
        var d = svc.GetDate(-24);
        Assert.That(d.Year, Is.EqualTo(1024));
        Assert.That(d.MonthName, Is.EqualTo("August"));
        Assert.That(d.DayOfMonth, Is.EqualTo(31));
        Assert.That(d.HourOfDay, Is.EqualTo(0));
    }

    [Test]
    public void GetHourOfDay_Negative_ReturnsPositive()
    {
        var svc = BuildDefault();
        Assert.That(svc.GetHourOfDay(-1), Is.EqualTo(23));
        Assert.That(svc.GetHourOfDay(-16), Is.EqualTo(8));
    }

    [Test]
    public void GetWorldHour_NegativeRange_RoundTrips()
    {
        var svc = BuildDefault();
        for (long wh = -10000; wh < 0; wh += 137)
        {
            var date = svc.GetDate(wh);
            long computed = svc.GetWorldHour(date.Year, date.MonthNumber, date.DayOfMonth, date.HourOfDay);
            Assert.That(computed, Is.EqualTo(wh), $"Round-trip failed for worldHour={wh}");
        }
    }

    [Test]
    public void FormatDateTime_NegativeWorldHour_ShowsPositiveHour()
    {
        var svc = BuildDefault();
        var formatted = svc.FormatDateTime(-16);
        Assert.That(formatted, Does.Contain("08:00"));
        Assert.That(formatted, Does.Not.Contain("-"));
    }
}
