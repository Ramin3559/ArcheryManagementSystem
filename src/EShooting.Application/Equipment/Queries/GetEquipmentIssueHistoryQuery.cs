using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Application.Equipment;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Equipment.Queries;

public sealed record GetEquipmentIssueHistoryQuery(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    Guid? EquipmentItemId = null,
    EquipmentIssueType? IssueType = null,
    Guid? IssuedByStaffId = null) : IRequest<EquipmentIssueHistoryResult>;

public sealed class GetEquipmentIssueHistoryQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetEquipmentIssueHistoryQuery, EquipmentIssueHistoryResult>
{
    public async Task<EquipmentIssueHistoryResult> Handle(
        GetEquipmentIssueHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromLocalDate.Date;
        var to = request.ToLocalDate.Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var issues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        var equipment = (await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken))
            .ToDictionary(x => x.Id);
        var sessions = (await repository.GetSessionsLightAsync(cancellationToken))
            .ToDictionary(x => x.Id);
        var athletes = (await repository.GetAthletesAsync(cancellationToken))
            .ToDictionary(x => x.Id);
        var staff = (await repository.GetStaffMembersAsync(activeOnly: false, cancellationToken))
            .ToDictionary(x => x.Id);
        var lanes = (await repository.GetLanesAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.Number);

        var searchItemId = request.EquipmentItemId;

        var rows = new List<EquipmentIssueHistoryRow>();

        foreach (var issue in issues.OrderByDescending(x => x.CreatedAtUtc))
        {
            var issuedLocal = AzerbaijanTime.UtcToLocalDate(issue.CreatedAtUtc);
            if (issuedLocal < from || issuedLocal > to)
            {
                continue;
            }

            if (!equipment.TryGetValue(issue.EquipmentItemId, out var item))
            {
                continue;
            }

            if (searchItemId is Guid filterItemId && issue.EquipmentItemId != filterItemId)
            {
                continue;
            }

            if (request.IssueType is not null && issue.IssueType != request.IssueType)
            {
                continue;
            }

            if (request.IssuedByStaffId is Guid staffFilter
                && issue.IssuedByStaffId != staffFilter)
            {
                continue;
            }

            sessions.TryGetValue(issue.SessionId, out var session);
            var athleteName = session is not null && athletes.TryGetValue(session.AthleteId, out var athlete)
                ? athlete.FullName
                : "—";
            int? laneNumber = session is not null && lanes.TryGetValue(session.LaneId, out var ln)
                ? ln
                : null;

            var issuedBy = issue.IssuedByStaffId is Guid issuerId && staff.TryGetValue(issuerId, out var issuer)
                ? FormatStaffName(issuer)
                : "—";
            var returnedBy = issue.ReturnedByStaffId is Guid returnerId && staff.TryGetValue(returnerId, out var returner)
                ? FormatStaffName(returner)
                : null;

            var qty = Math.Max(1, issue.Quantity);
            var isRental = issue.IssueType == EquipmentIssueType.Rental;
            var unitPrice = isRental
                ? 0m
                : issue.UnitPrice > 0
                    ? issue.UnitPrice
                    : EquipmentIssuanceRules.ResolveUnitPrice(item, issue.IssueType);
            var lineTotal = isRental ? 0m : unitPrice * qty;

            rows.Add(new EquipmentIssueHistoryRow
            {
                IssueId = issue.Id,
                IssuedAtLocal = AzerbaijanTime.UtcToLocalDateTime(issue.CreatedAtUtc).ToString("yyyy-MM-dd HH:mm"),
                EquipmentName = item.Name,
                Category = item.Category,
                IssueType = issue.IssueType,
                IssueTypeLabel = FormatIssueType(issue.IssueType),
                Quantity = qty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                CustomerName = athleteName,
                LaneNumber = laneNumber,
                IssuedByStaffName = issuedBy,
                ReturnedAtLocal = issue.ReturnedAtUtc is null
                    ? null
                    : AzerbaijanTime.UtcToLocalDateTime(issue.ReturnedAtUtc.Value).ToString("yyyy-MM-dd HH:mm"),
                ReturnedByStaffName = returnedBy
            });
        }

        var sales = rows.Where(x => x.IssueType == EquipmentIssueType.Sale).ToList();
        var rentals = rows.Where(x => x.IssueType == EquipmentIssueType.Rental).ToList();

        return new EquipmentIssueHistoryResult
        {
            Items = rows,
            SaleQuantityTotal = sales.Sum(x => x.Quantity),
            RentalQuantityTotal = rentals.Sum(x => x.Quantity),
            SaleRevenueTotal = sales.Sum(x => x.LineTotal),
            RentalRevenueTotal = 0m,
            GrandTotal = sales.Sum(x => x.LineTotal)
        };
    }

    private static string FormatIssueType(EquipmentIssueType type) => type switch
    {
        EquipmentIssueType.Sale => "Satış",
        EquipmentIssueType.Rental => "İcarə (zal)",
        _ => type.ToString()
    };

    private static string FormatStaffName(StaffMember member) =>
        $"{member.FirstName} {member.LastName}".Trim();
}
