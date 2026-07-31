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
        return $"Kart «{label}» hazırda {name}{phone} nəzdindədir. Əvvəlcə kartı qaytarın.";
    }

    public static async Task EnsureCardAvailableAsync(
        ITrainingCenterRepository repository,
        ClubCardType cardType,
        string cardNumber,
        Guid? excludeAthleteId,
        CancellationToken cancellationToken)
    {
        var holder = await repository.FindAthleteByClubCardAsync(
            cardType,
            cardNumber,
            excludeAthleteId,
            cancellationToken);
        if (holder is not null)
        {
            throw new ClubCardHeldException(cardType, cardNumber, holder);
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
        var prev = AthleteRegistrationRules.NormalizeText(previousCardNumber);
        var next = AthleteRegistrationRules.NormalizeText(nextCardNumber);

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
        var card = AthleteRegistrationRules.NormalizeText(athlete.ClubCardNumber);
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
