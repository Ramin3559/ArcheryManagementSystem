using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

/// <summary>
/// Soft-silinmiş müştərini və bağlı tarixçəni birdəfəlik silir (geri qayıtmaz).
/// </summary>
public sealed record HardDeleteAthleteCommand(Guid AthleteId) : IRequest;

public sealed class HardDeleteAthleteCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<HardDeleteAthleteCommand>
{
    public async Task Handle(HardDeleteAthleteCommand request, CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        if (athlete.IsActive)
        {
            throw new InvalidOperationException(
                "Birdəfəlik silmək üçün əvvəlcə müştərini «Sil» ilə deaktiv edin (Silinmiş siyahısı).");
        }

        await repository.HardDeleteAthleteAsync(athlete.Id, cancellationToken);
    }
}
