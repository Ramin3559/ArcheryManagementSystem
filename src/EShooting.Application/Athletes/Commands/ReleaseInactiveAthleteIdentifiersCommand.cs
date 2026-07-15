using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

/// <summary>
/// Deaktiv müştərinin telefon/email/FİN-ini azad edir — nömrə başqa şəxsə keçəndə yeni qeydiyyat üçün.</summary>
public sealed record ReleaseInactiveAthleteIdentifiersCommand(Guid AthleteId, Guid? StaffId = null) : IRequest;

public sealed class ReleaseInactiveAthleteIdentifiersCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<ReleaseInactiveAthleteIdentifiersCommand>
{
    public async Task Handle(ReleaseInactiveAthleteIdentifiersCommand request, CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        if (athlete.IsActive)
        {
            throw new InvalidOperationException("Yalnız deaktiv müştərinin identifikatorları azad edilə bilər.");
        }

        var card = AthleteRegistrationRules.NormalizeOptionalText(athlete.ClubCardNumber);
        if (!string.IsNullOrWhiteSpace(card))
        {
            await repository.CloseOpenClubCardAssignmentAsync(
                athlete.Id,
                card,
                request.StaffId,
                cancellationToken);
        }

        athlete.PhoneNumber = $"a{athlete.Id:N}";
        athlete.Email = null;
        athlete.IdCardNumber = null;
        athlete.ClubCardNumber = null;
        await repository.UpdateAthleteAsync(athlete, cancellationToken);
    }
}
