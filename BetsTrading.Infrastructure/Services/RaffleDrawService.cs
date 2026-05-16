using System.Security.Cryptography;
using System.Text;
using BetsTrading.Application.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public class RaffleDrawService : IRaffleDrawService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFirebaseNotificationService _firebase;
    private readonly ILocalizationService _localization;
    private readonly IApplicationLogger _logger;

    public RaffleDrawService(
        IUnitOfWork unitOfWork,
        IFirebaseNotificationService firebase,
        ILocalizationService localization,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _firebase = firebase;
        _localization = localization;
        _logger = logger;
    }

    public async Task ProcessDueRafflesAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.RaffleItems.EnsureDefaultItemsAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var dueItems = await _unitOfWork.RaffleItems.GetDueItemsAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (dueItems.Count == 0)
            return;

        foreach (var item in dueItems)
        {
            await ProcessSingleItemAsync(item, utcNow, cancellationToken);
        }
    }

    private async Task ProcessSingleItemAsync(RaffleItem item, DateTime utcNow, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var fresh = await _unitOfWork.RaffleItems.GetByIdAsync(item.Id, cancellationToken);
            if (fresh == null || fresh.RaffleDate > DateTimeOffset.UtcNow)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return;
            }

            var entries = await _unitOfWork.Raffles.GetEntriesByItemIdAsync(fresh.Id, cancellationToken);

            if (entries.Count > 0)
            {
                var winnerEntry = entries[RandomNumberGenerator.GetInt32(entries.Count)];
                var winner = await _unitOfWork.Users.GetByIdAsync(winnerEntry.UserId, cancellationToken);
                if (winner != null)
                {
                    await WriteWinnerFileAsync(fresh, winner, entries.Count, utcNow, cancellationToken);
                    await SendWinnerNotificationAsync(fresh, winner, cancellationToken);
                    _logger.Information(
                        "[RaffleDraw] :: Item {ItemId} won by user {UserId} ({Entries} entries)",
                        fresh.Id,
                        winner.Id,
                        entries.Count);
                }
            }
            else
            {
                _logger.Information("[RaffleDraw] :: Item {ItemId} had no participants", fresh.Id);
            }

            await _unitOfWork.Raffles.DeleteEntriesByItemIdAsync(fresh.Id, cancellationToken);
            fresh.ResetAfterDraw(utcNow);
            _unitOfWork.RaffleItems.Update(fresh);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Error(ex, "[RaffleDraw] :: Error processing item {ItemId}", item.Id);
        }
    }

    private async Task WriteWinnerFileAsync(
        RaffleItem item,
        User winner,
        int totalEntries,
        DateTime drawnAtUtc,
        CancellationToken cancellationToken)
    {
        var dir = Path.Combine(AppContext.BaseDirectory ?? ".", "raffle_winners");
        Directory.CreateDirectory(dir);
        var fileName = $"raffle_item_{item.Id}_{drawnAtUtc:yyyyMMdd_HHmmss}Z.txt";
        var path = Path.Combine(dir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine($"drawn_at_utc={drawnAtUtc:O}");
        sb.AppendLine($"raffle_item_id={item.Id}");
        sb.AppendLine($"prize_name={item.Name}");
        sb.AppendLine($"prize_short_name={item.ShortName}");
        sb.AppendLine($"coins_cost={item.Coins}");
        sb.AppendLine($"total_entries={totalEntries}");
        sb.AppendLine($"winner_user_id={winner.Id}");
        sb.AppendLine($"winner_fullname={winner.Fullname}");
        sb.AppendLine($"winner_email={winner.Email}");
        sb.AppendLine($"winner_country={winner.Country}");

        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken);
        _logger.Information("[RaffleDraw] :: Winner file written: {Path}", path);
    }

    private async Task SendWinnerNotificationAsync(RaffleItem item, User winner, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(winner.Fcm))
            return;

        var country = string.IsNullOrWhiteSpace(winner.Country) ? "GB" : winner.Country;
        var title = _localization.GetTranslationByCountry(country, "raffleWinnerTitle");
        var bodyTemplate = _localization.GetTranslationByCountry(country, "raffleWinnerBody");
        var body = string.Format(bodyTemplate, item.Name, item.ShortName);

        var data = new Dictionary<string, string>
        {
            { "type", "RAFFLE_WIN" },
            { "raffleItemId", item.Id.ToString() }
        };

        try
        {
            await _firebase.SendNotificationToUserAsync(winner.Fcm, title, body, data);
        }
        catch (Exception ex)
        {
            _logger.Warning("[RaffleDraw] :: Firebase notification failed for user {UserId}: {Message}", winner.Id, ex.Message);
        }
    }
}
