using EShooting.Domain.Enums;

namespace EShooting.Application.Athletes.Queries;

public sealed class ClubCardStockTypeSummary
{
    public ClubCardType CardType { get; init; }
    public string TypeLabel { get; init; } = "";
    public int Total { get; init; }
    public int Issued { get; init; }
    public int Available { get; init; }
    public int Deleted { get; init; }
}

public sealed class HeldClubCardItem
{
    public Guid AthleteId { get; init; }
    public string AthleteFullName { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public ClubCardType CardType { get; init; }
    public string TypeLabel { get; init; } = "";
    public string CardNumber { get; init; } = "";
}

public sealed class ClubCardLookupItem
{
    public Guid StockId { get; init; }
    public ClubCardType CardType { get; init; }
    public string TypeLabel { get; init; } = "";
    public string CardNumber { get; init; } = "";
    public bool IsHeld { get; init; }
    public bool IsDeleted { get; init; }
    public bool Missing { get; init; }
    public Guid? HolderAthleteId { get; init; }
    public string? HolderName { get; init; }
    public string? HolderPhone { get; init; }
}
