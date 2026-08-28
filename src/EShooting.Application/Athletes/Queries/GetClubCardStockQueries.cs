using EShooting.Application.Athletes;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Queries;

public sealed record GetClubCardStockSummaryQuery : IRequest<IReadOnlyList<ClubCardStockTypeSummary>>;

public sealed class GetClubCardStockSummaryQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetClubCardStockSummaryQuery, IReadOnlyList<ClubCardStockTypeSummary>>
{
    public async Task<IReadOnlyList<ClubCardStockTypeSummary>> Handle(
        GetClubCardStockSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await repository.GetClubCardStockAsync(cancellationToken);
        var heldAthletes = await repository.GetAthletesWithClubCardAsync(cancellationToken);
        var heldKeys = heldAthletes
            .Where(a => a.ClubCardType is ClubCardType)
            .Select(a => (
                Type: a.ClubCardType!.Value,
                Number: ClubCardNumberRules.Normalize(a.ClubCardNumber) ?? (a.ClubCardNumber ?? "")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Number))
            .ToHashSet();

        var types = new[] { ClubCardType.Boz, ClubCardType.Qirmizi, ClubCardType.Qara, ClubCardType.VipQizili };
        var result = new List<ClubCardStockTypeSummary>();
        foreach (var type in types)
        {
            var ofType = stock.Where(s => s.CardType == type).ToList();
            var active = ofType.Where(s => !s.IsDeleted).ToList();
            var total = active.Count;
            var issued = active.Count(s =>
                heldKeys.Contains((type, ClubCardNumberRules.Normalize(s.CardNumber) ?? s.CardNumber)));
            result.Add(new ClubCardStockTypeSummary
            {
                CardType = type,
                TypeLabel = ClubCardTypeLabels.Get(type),
                Total = total,
                Issued = issued,
                Available = Math.Max(0, total - issued),
                Deleted = ofType.Count(s => s.IsDeleted)
            });
        }

        return result;
    }
}

public sealed record GetHeldClubCardsQuery : IRequest<IReadOnlyList<HeldClubCardItem>>;

public sealed class GetHeldClubCardsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetHeldClubCardsQuery, IReadOnlyList<HeldClubCardItem>>
{
    public async Task<IReadOnlyList<HeldClubCardItem>> Handle(
        GetHeldClubCardsQuery request,
        CancellationToken cancellationToken)
        => (await repository.GetAthletesWithClubCardAsync(cancellationToken))
            .Where(a => a.ClubCardType is ClubCardType && !string.IsNullOrWhiteSpace(a.ClubCardNumber))
            .Select(a => new HeldClubCardItem
            {
                AthleteId = a.Id,
                AthleteFullName = a.FullName,
                PhoneNumber = a.PhoneNumber,
                CardType = a.ClubCardType!.Value,
                TypeLabel = ClubCardTypeLabels.Get(a.ClubCardType.Value),
                CardNumber = ClubCardNumberRules.Normalize(a.ClubCardNumber) ?? a.ClubCardNumber!.Trim()
            })
            .OrderBy(x => x.TypeLabel)
            .ThenBy(x => int.TryParse(x.CardNumber, out var n) ? n : int.MaxValue)
            .ToList();
}

public sealed record GetAvailableClubCardNumbersQuery(
    ClubCardType CardType,
    string? Query,
    int Limit = 20) : IRequest<IReadOnlyList<string>>;

public sealed class GetAvailableClubCardNumbersQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetAvailableClubCardNumbersQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetAvailableClubCardNumbersQuery request,
        CancellationToken cancellationToken)
    {
        var needle = AthleteRegistrationRules.NormalizeDigits(request.Query);
        if (string.IsNullOrWhiteSpace(needle))
        {
            return [];
        }

        var take = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 40);
        var numbers = (await repository.GetClubCardStockAsync(cancellationToken))
            .Where(s => s.CardType == request.CardType && !s.IsDeleted)
            .Select(s => s.CardNumber);
        var held = (await repository.GetAthletesWithClubCardAsync(cancellationToken))
            .Where(a => a.ClubCardType == request.CardType)
            .Select(a => ClubCardNumberRules.Normalize(a.ClubCardNumber) ?? "")
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        return numbers
            .Select(n => ClubCardNumberRules.Normalize(n) ?? n)
            .Where(n => !held.Contains(n))
            .Where(n => n.Contains(needle, StringComparison.Ordinal)
                        || n.TrimStart('0').StartsWith(needle.TrimStart('0'), StringComparison.Ordinal))
            .OrderBy(n => int.TryParse(n, out var v) ? v : int.MaxValue)
            .Take(take)
            .ToList();
    }
}

public sealed record GetClubCardLookupQuery(ClubCardType CardType, string CardNumber)
    : IRequest<ClubCardLookupItem?>;

public sealed class GetClubCardLookupQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetClubCardLookupQuery, ClubCardLookupItem?>
{
    public async Task<ClubCardLookupItem?> Handle(
        GetClubCardLookupQuery request,
        CancellationToken cancellationToken)
    {
        var number = ClubCardNumberRules.Normalize(request.CardNumber)
                     ?? AthleteRegistrationRules.NormalizeText(request.CardNumber);
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var stock = await repository.FindClubCardStockAsync(request.CardType, number, cancellationToken);
        if (stock is null)
        {
            return null;
        }

        var shown = ClubCardNumberRules.Normalize(stock.CardNumber) ?? stock.CardNumber.Trim();
        var holder = stock.IsDeleted
            ? null
            : await repository.FindAthleteByClubCardAsync(
                request.CardType,
                shown,
                excludeAthleteId: null,
                cancellationToken);

        return new ClubCardLookupItem
        {
            StockId = stock.Id,
            CardType = stock.CardType,
            TypeLabel = ClubCardTypeLabels.Get(stock.CardType),
            CardNumber = shown,
            IsHeld = holder is not null,
            IsDeleted = stock.IsDeleted,
            HolderAthleteId = holder?.Id,
            HolderName = holder is null
                ? null
                : (string.IsNullOrWhiteSpace(holder.FullName)
                    ? $"{holder.FirstName} {holder.LastName}".Trim()
                    : holder.FullName.Trim()),
            HolderPhone = holder?.PhoneNumber
        };
    }
}

public sealed record GetClubCardLookupsQuery(ClubCardType CardType, IReadOnlyList<string> CardNumbers)
    : IRequest<IReadOnlyList<ClubCardLookupItem>>;

public sealed class GetClubCardLookupsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetClubCardLookupsQuery, IReadOnlyList<ClubCardLookupItem>>
{
    public async Task<IReadOnlyList<ClubCardLookupItem>> Handle(
        GetClubCardLookupsQuery request,
        CancellationToken cancellationToken)
    {
        var inner = new GetClubCardLookupQueryHandler(repository);
        var result = new List<ClubCardLookupItem>();
        foreach (var number in request.CardNumbers)
        {
            var item = await inner.Handle(new GetClubCardLookupQuery(request.CardType, number), cancellationToken);
            if (item is not null)
            {
                result.Add(item);
            }
            else
            {
                var shown = ClubCardNumberRules.Normalize(number) ?? number.Trim();
                result.Add(new ClubCardLookupItem
                {
                    CardType = request.CardType,
                    TypeLabel = ClubCardTypeLabels.Get(request.CardType),
                    CardNumber = shown,
                    IsHeld = false,
                    IsDeleted = false,
                    Missing = true
                });
            }
        }

        return result;
    }
}

public sealed class ClubCardCatalogItem
{
    public ClubCardType CardType { get; init; }
    public string TypeLabel { get; init; } = "";
    public string CardNumber { get; init; } = "";
    public string Status { get; init; } = "";
    public string StatusLabel { get; init; } = "";
    public string HolderName { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
}

public sealed record GetClubCardCatalogQuery(
    ClubCardType? CardType,
    string? Status) : IRequest<IReadOnlyList<ClubCardCatalogItem>>;

public sealed class GetClubCardCatalogQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetClubCardCatalogQuery, IReadOnlyList<ClubCardCatalogItem>>
{
    public async Task<IReadOnlyList<ClubCardCatalogItem>> Handle(
        GetClubCardCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await repository.GetClubCardStockAsync(cancellationToken);
        var heldAthletes = await repository.GetAthletesWithClubCardAsync(cancellationToken);
        var holders = heldAthletes
            .Where(a => a.ClubCardType is ClubCardType && !string.IsNullOrWhiteSpace(a.ClubCardNumber))
            .GroupBy(a => (
                Type: a.ClubCardType!.Value,
                Number: ClubCardNumberRules.Normalize(a.ClubCardNumber) ?? a.ClubCardNumber!.Trim()))
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                EqualityComparer<(ClubCardType Type, string Number)>.Default);

        var status = (request.Status ?? "all").Trim().ToLowerInvariant();
        var rows = new List<ClubCardCatalogItem>();
        foreach (var s in stock)
        {
            if (request.CardType is ClubCardType filterType && s.CardType != filterType)
            {
                continue;
            }

            var number = ClubCardNumberRules.Normalize(s.CardNumber) ?? s.CardNumber.Trim();
            holders.TryGetValue((s.CardType, number), out var holder);
            var isHeld = !s.IsDeleted && holder is not null;
            var rowStatus = s.IsDeleted ? "deleted" : isHeld ? "held" : "free";
            if (status is "held" or "free" or "deleted" && rowStatus != status)
            {
                continue;
            }

            var typeLabel = ClubCardTypeLabels.Get(s.CardType);
            rows.Add(new ClubCardCatalogItem
            {
                CardType = s.CardType,
                TypeLabel = typeLabel,
                CardNumber = number,
                Status = rowStatus,
                StatusLabel = s.IsDeleted
                    ? "Mövcud deyil"
                    : isHeld
                        ? "Müştəridə olan"
                        : "Müştəridə olmayan",
                HolderName = isHeld
                    ? (string.IsNullOrWhiteSpace(holder!.FullName)
                        ? $"{holder.FirstName} {holder.LastName}".Trim()
                        : holder.FullName.Trim())
                    : "",
                PhoneNumber = isHeld ? holder!.PhoneNumber ?? "" : ""
            });
        }

        return rows
            .OrderBy(x => x.TypeLabel)
            .ThenBy(x => int.TryParse(x.CardNumber, out var n) ? n : int.MaxValue)
            .ToList();
    }
}
