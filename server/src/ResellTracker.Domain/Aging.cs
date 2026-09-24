namespace ResellTracker.Domain;

public static class Aging
{
    /// <summary>
    /// How long an item has been sitting: from listed (or acquired, if never
    /// listed) to the sale date if sold, otherwise to today. Never negative; null
    /// with no start date. Dates are calendar dates, so time zones never shift it.
    /// </summary>
    public static int? DaysListed(
        ItemStatus status, DateOnly? acquiredDate, DateOnly? listedDate, DateOnly? saleDate, DateOnly today)
    {
        if ((listedDate ?? acquiredDate) is not { } start)
        {
            return null;
        }

        var end = status is ItemStatus.Sold && saleDate is { } sold ? sold : today;
        return Math.Max(0, end.DayNumber - start.DayNumber);
    }
}
