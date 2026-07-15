using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;

namespace EShooting.Application.Athletes;

public static class ClubCardAssignmentService
{
    public static string FormatHeldByMessage(string cardNumber, Athlete holder)
    {
        var name = string.IsNullOrWhiteSpace(holder.FullName)
            ? $"{holder.FirstName} {holder.LastName}".Trim()
            : holder.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(holder.PhoneNumber) ? "" : $" ({holder.PhoneNumber})";
        return $"Kart «{cardNumber}» hazırda {name}{phone} nəzdindədir. Əvvəlcə kartı qaytarın.";
    }

    public static async Task EnsureCardAvailableAsync(
        ITrainingCenterRepository repository,
        string cardNumber,
        Guid? excludeAthleteId,
        CancellationToken cancellationToken)
    {
        var holder = await repository.FindAthleteByClubCardNumberAsync(
            cardNumber,
            excludeAthleteId,
            cancellationToken);
        if (holder is not null)
        {
            throw new ClubCardHeldException(cardNumber, holder);
        }
    }

    public static async Task SyncAthleteCardAsync(
        ITrainingCenterRepository repository,
        Guid athleteId,
        string? previousCardNumber,
        string? nextCardNumber,
        Guid? staffId,
        CancellationToken cancellationToken)
    {
        var prev = AthleteRegistrationRules.NormalizeText(previousCardNumber);
        var next = AthleteRegistrationRules.NormalizeText(nextCardNumber);

        if (string.Equals(prev, next, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(prev))
        {
            await repository.CloseOpenClubCardAssignmentAsync(athleteId, prev, staffId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(next))
        {
            await EnsureCardAvailableAsync(repository, next, athleteId, cancellationToken);
            await repository.AddClubCardAssignmentAsync(new ClubCardAssignment
            {
                CardNumber = next,
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
        if (string.IsNullOrWhiteSpace(card))
        {
            throw new InvalidOperationException("Bu müştəridə qaytarılacaq kart yoxdur.");
        }

        await repository.CloseOpenClubCardAssignmentAsync(athlete.Id, card, staffId, cancellationToken);
        athlete.ClubCardNumber = null;
        await repository.UpdateAthleteAsync(athlete, cancellationToken);
    }
}

public sealed class ClubCardHeldException : InvalidOperationException
{
    public ClubCardHeldException(string cardNumber, Athlete holder)
        : base(ClubCardAssignmentService.FormatHeldByMessage(cardNumber, holder))
    {
        CardNumber = cardNumber;
        Holder = holder;
    }

    public string CardNumber { get; }
    public Athlete Holder { get; }
}
