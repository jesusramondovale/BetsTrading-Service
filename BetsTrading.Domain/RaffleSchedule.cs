namespace BetsTrading.Domain;

/// <summary>Helpers for raffle draw dates (always 1st of month 00:00 UTC).</summary>
public static class RaffleSchedule
{
    public const int RaffleItemCount = 6;

    public static DateTimeOffset NextMonthFirstUtc(DateTime utcNow)
    {
        var year = utcNow.Year;
        var month = utcNow.Month;
        if (month == 12)
        {
            year++;
            month = 1;
        }
        else
        {
            month++;
        }

        return new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
