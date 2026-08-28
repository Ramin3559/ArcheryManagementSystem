using EShooting.Application.Athletes;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record UpdateClubCardStockCommand(
    ClubCardType CardType,
    string CardNumber,
    string NewCardNumber) : IRequest;

public sealed class UpdateClubCardStockCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpdateClubCardStockCommand>
{
    public async Task Handle(UpdateClubCardStockCommand request, CancellationToken cancellationToken)
    {
        var current = ClubCardNumberRules.Normalize(request.CardNumber)
                      ?? AthleteRegistrationRules.NormalizeText(request.CardNumber);
        var next = ClubCardNumberRules.Normalize(request.NewCardNumber)
                   ?? AthleteRegistrationRules.NormalizeText(request.NewCardNumber);
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next))
        {
            throw new InvalidOperationException("Kart nömrəsi daxil edin.");
        }

        var stock = await repository.FindClubCardStockAsync(request.CardType, current, cancellationToken)
                    ?? throw new InvalidOperationException("Bu kart stokda yoxdur.");
        if (stock.IsDeleted)
        {
            throw new InvalidOperationException(ClubCardTypeLabels.FormatUnavailable(request.CardType, current));
        }

        var holder = await repository.FindAthleteByClubCardAsync(
            request.CardType,
            current,
            excludeAthleteId: null,
            cancellationToken);
        if (holder is not null)
        {
            throw new InvalidOperationException("Kart müştəridədir. Nömrəni dəyişmək üçün əvvəl qaytarın və ya silin.");
        }

        if (ClubCardNumberRules.Same(current, next))
        {
            return;
        }

        if (await repository.ClubCardStockExistsAsync(request.CardType, next, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Kart «{ClubCardTypeLabels.FormatCard(request.CardType, next)}» artıq stokdadır.");
        }

        stock.CardNumber = next;
        await repository.UpdateClubCardStockAsync(stock, cancellationToken);
    }
}
