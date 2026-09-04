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

public sealed record AddClubCardStockRangeResult(
    int Added,
    IReadOnlyList<string> AlreadyExists,
    IReadOnlyList<string> Deleted);

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

        var byNumber = new Dictionary<string, ClubCardStock>(StringComparer.Ordinal);
        foreach (var row in await repository.GetClubCardStockAsync(cancellationToken))
        {
            if (row.CardType != request.CardType)
            {
                continue;
            }

            var key = ClubCardNumberRules.Normalize(row.CardNumber) ?? row.CardNumber.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            byNumber[key] = row;
        }

        var toAdd = new List<ClubCardStock>();
        var alreadyExists = new List<string>();
        var deleted = new List<string>();
        for (var n = request.FromNumber; n <= request.ToNumber; n++)
        {
            var num = ClubCardNumberRules.Format(n);
            var key = ClubCardNumberRules.Normalize(num) ?? num;
            if (byNumber.TryGetValue(key, out var existing))
            {
                if (existing.IsDeleted)
                {
                    deleted.Add(num);
                }
                else
                {
                    alreadyExists.Add(num);
                }

                continue;
            }

            var stock = new ClubCardStock
            {
                CardType = request.CardType,
                CardNumber = num,
                CreatedAtUtc = DateTime.UtcNow
            };
            toAdd.Add(stock);
            byNumber[key] = stock;
        }

        if (toAdd.Count > 0)
        {
            await repository.AddClubCardStockRangeAsync(toAdd, cancellationToken);
        }

        return new AddClubCardStockRangeResult(toAdd.Count, alreadyExists, deleted);
    }
}
