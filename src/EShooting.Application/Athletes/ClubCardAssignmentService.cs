using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Athletes;

public static class ClubCardAssignmentService
{
    public static string FormatHeldByMessage(ClubCardType cardType, string cardNumber, Athlete holder)
    {
        var name = string.IsNullOrWhiteSpace(holder.FullName)
            ? $"{holder.FirstName} {holder.LastName}".Trim()
            : holder.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(holder.PhoneNumber) ? "" : $" ({holder.PhoneNumber})";
        var label = ClubCardTypeLabels.FormatCard(cardType, cardNumber);
        return $"Kart «{label}» hazırda {name}{phone} adlı müştəridədir";
    }

    public static async Task EnsureCardAvailableAsync(
        ITrainingCenterRepository repository,
        ClubCardType cardType,
        string cardNumber,
        Guid? excludeAthleteId,
        CancellationToken cancellationToken)
    {
        var number = ClubCardNumberRules.Normalize(cardNumber)
                     ?? AthleteRegistrationRules.NormalizeText(cardNumber);
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new InvalidOperationException("Kart nömrəsi daxil edin.");
        }

        if (await repository.HasAnyClubCardStockAsync(cancellationToken))
        {
            var stock = await repository.FindClubCardStockAsync(cardType, number, cancellationToken);
            if (stock is null)
            {
                throw new InvalidOperationException(
                    $"Kart «{ClubCardTypeLabels.FormatCard(cardType, number)}» stokda yoxdur. Admin əlavə etsin.");
            }

            if (stock.IsDeleted)
            {
                throw new InvalidOperationException(
                    ClubCardTypeLabels.FormatUnavailable(cardType, number));
            }
        }

        var holder = await repository.FindAthleteByClubCardAsync(
            cardType,
            number,
            excludeAthleteId,
            cancellationToken);
        if (holder is not null)
        {
            throw new ClubCardHeldException(cardType, number, holder);
        }
    }

    public static async Task SyncAthleteCardAsync(
        ITrainingCenterRepository repository,
        Guid athleteId,
        string? previousCardNumber,
        ClubCardType? previousCardType,
        string? nextCardNumber,
        ClubCardType? nextCardType,
        Guid? staffId,
        CancellationToken cancellationToken)
    {
        var prev = ClubCardNumberRules.Normalize(previousCardNumber)
                   ?? AthleteRegistrationRules.NormalizeText(previousCardNumber);
        var next = ClubCardNumberRules.Normalize(nextCardNumber)
                   ?? AthleteRegistrationRules.NormalizeText(nextCardNumber);

        var same =
            string.Equals(prev, next, StringComparison.OrdinalIgnoreCase)
            && previousCardType == nextCardType;
        if (same)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(prev) && previousCardType is ClubCardType prevType)
        {
            await repository.CloseOpenClubCardAssignmentAsync(
                athleteId,
                prevType,
                prev,
                staffId,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(next))
        {
            if (nextCardType is not ClubCardType type)
            {
                throw new InvalidOperationException("Kart növü seçin.");
            }

            await EnsureCardAvailableAsync(repository, type, next, athleteId, cancellationToken);
            await repository.AddClubCardAssignmentAsync(new ClubCardAssignment
            {
                CardNumber = next,
                CardType = type,
                AthleteId = athleteId,
                IssuedAtUtc = DateTime.UtcNow,
                IssuedByStaffId = staffId
            }, cancellationToken);
        }
    }

    public static async Task ReturnCardAsync(
        ITrainingCenterRepository repository,
        Athlete athlete,
        Guid? staffId,
        CancellationToken cancellationToken)
    {
        var card = ClubCardNumberRules.Normalize(athlete.ClubCardNumber)
                   ?? AthleteRegistrationRules.NormalizeText(athlete.ClubCardNumber);
        if (string.IsNullOrWhiteSpace(card) || athlete.ClubCardType is not ClubCardType type)
        {
            throw new InvalidOperationException("Bu müştəridə qaytarılacaq kart yoxdur.");
        }

        await repository.CloseOpenClubCardAssignmentAsync(athlete.Id, type, card, staffId, cancellationToken);
        athlete.ClubCardNumber = null;
        athlete.ClubCardType = null;
        await repository.UpdateAthleteAsync(athlete, cancellationToken);
    }
}

public sealed class ClubCardHeldException : InvalidOperationException
{
    public ClubCardHeldException(ClubCardType cardType, string cardNumber, Athlete holder)
        : base(ClubCardAssignmentService.FormatHeldByMessage(cardType, cardNumber, holder))
    {
        CardType = cardType;
        CardNumber = cardNumber;
        Holder = holder;
    }

    public ClubCardType CardType { get; }
    public string CardNumber { get; }
    public Athlete Holder { get; }
}
