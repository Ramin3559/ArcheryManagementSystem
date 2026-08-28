using EShooting.Application.Athletes;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record RestoreClubCardStockCommand(
    ClubCardType CardType,
    string CardNumber) : IRequest;

public sealed class RestoreClubCardStockCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<RestoreClubCardStockCommand>
{
    public async Task Handle(RestoreClubCardStockCommand request, CancellationToken cancellationToken)
    {
        var number = ClubCardNumberRules.Normalize(request.CardNumber)
                     ?? AthleteRegistrationRules.NormalizeText(request.CardNumber);
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new InvalidOperationException("Kart nömrəsi daxil edin.");
        }

        var stock = await repository.FindClubCardStockAsync(request.CardType, number, cancellationToken)
                    ?? throw new InvalidOperationException("Bu kart stokda yoxdur.");
        if (!stock.IsDeleted)
        {
            throw new InvalidOperationException(
                $"Kart «{ClubCardTypeLabels.FormatCard(request.CardType, number)}» artıq mövcuddur.");
        }

        stock.IsDeleted = false;
        stock.DeletedAtUtc = null;
        await repository.UpdateClubCardStockAsync(stock, cancellationToken);
    }
}
