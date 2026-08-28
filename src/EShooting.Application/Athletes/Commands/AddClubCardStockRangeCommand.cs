using EShooting.Application.Athletes;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record AddClubCardStockRangeCommand(
    ClubCardType CardType,
    int FromNumber,
    int ToNumber) : IRequest<AddClubCardStockRangeResult>;

public sealed record AddClubCardStockRangeResult(int Added, int Skipped);

public sealed class AddClubCardStockRangeCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<AddClubCardStockRangeCommand, AddClubCardStockRangeResult>
{
    public async Task<AddClubCardStockRangeResult> Handle(
        AddClubCardStockRangeCommand request,
        CancellationToken cancellationToken)
    {
        if (!ClubCardNumberRules.TryParseRange(request.FromNumber, request.ToNumber, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var existing = (await repository.GetClubCardStockNumbersAsync(request.CardType, cancellationToken))
            .Select(n => ClubCardNumberRules.Normalize(n) ?? n.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var toAdd = new List<ClubCardStock>();
        var skipped = 0;
        for (var n = request.FromNumber; n <= request.ToNumber; n++)
        {
            var num = ClubCardNumberRules.Format(n);
            if (existing.Contains(num))
            {
                skipped++;
                continue;
            }

            toAdd.Add(new ClubCardStock
            {
                CardType = request.CardType,
                CardNumber = num,
                CreatedAtUtc = DateTime.UtcNow
            });
            existing.Add(num);
        }

        if (toAdd.Count > 0)
        {
            await repository.AddClubCardStockRangeAsync(toAdd, cancellationToken);
        }

        return new AddClubCardStockRangeResult(toAdd.Count, skipped);
    }
}
