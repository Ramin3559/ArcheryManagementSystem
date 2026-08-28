using EShooting.Application.Athletes;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record DeleteClubCardStockCommand(
    ClubCardType CardType,
    string CardNumber,
    bool ReturnIfHeld) : IRequest;

public sealed class DeleteClubCardStockCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<DeleteClubCardStockCommand>
{
    public async Task Handle(DeleteClubCardStockCommand request, CancellationToken cancellationToken)
    {
        var number = ClubCardNumberRules.Normalize(request.CardNumber)
                     ?? AthleteRegistrationRules.NormalizeText(request.CardNumber);
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new InvalidOperationException("Kart nömrəsi daxil edin.");
        }

        var stock = await repository.FindClubCardStockAsync(request.CardType, number, cancellationToken)
                    ?? throw new InvalidOperationException("Bu kart stokda yoxdur.");
        if (stock.IsDeleted)
        {
            throw new InvalidOperationException(ClubCardTypeLabels.FormatUnavailable(request.CardType, number));
        }

        var holder = await repository.FindAthleteByClubCardAsync(
            request.CardType,
            number,
            excludeAthleteId: null,
            cancellationToken);
        if (holder is not null)
        {
            if (!request.ReturnIfHeld)
            {
                throw new ClubCardHeldException(request.CardType, number, holder);
            }

            await ClubCardAssignmentService.ReturnCardAsync(repository, holder, staffId: null, cancellationToken);
        }

        stock.IsDeleted = true;
        stock.DeletedAtUtc = DateTime.UtcNow;
        await repository.UpdateClubCardStockAsync(stock, cancellationToken);
    }
}
