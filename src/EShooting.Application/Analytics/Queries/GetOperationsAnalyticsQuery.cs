using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Application.Customers;
using EShooting.Application.Equipment;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Analytics.Queries;

public sealed record GetOperationsAnalyticsQuery(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    string Mode = "range") : IRequest<OperationsAnalyticsResult>;

public sealed class GetOperationsAnalyticsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetOperationsAnalyticsQuery, OperationsAnalyticsResult>
{
    public async Task<OperationsAnalyticsResult> Handle(
        GetOperationsAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromLocalDate.Date;
        var to = request.ToLocalDate.Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var issues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        var equipmentItems = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var packageRecords = await repository.GetCustomerPackageRecordsAsync(cancellationToken);
        var receipts = await repository.GetEquipmentSaleReceiptsAsync(cancellationToken);
        var receiptLines = await repository.GetEquipmentSaleReceiptLinesAsync(cancellationToken);
        var staffMembers = await repository.GetStaffMembersAsync(activeOnly: false, cancellationToken);

        var equipmentById = equipmentItems.ToDictionary(x => x.Id);
        var laneNoById = lanes.ToDictionary(x => x.Id, x => x.Number);
        var athletesById = athletes.ToDictionary(x => x.Id);
        var staffNameById = staffMembers.ToDictionary(
            x => x.Id,
            x => $"{x.FirstName} {x.LastName}".Trim());
        var laneNumbers = lanes.Select(x => x.Number).Distinct().OrderBy(x => x).ToList();
        if (laneNumbers.Count == 0)
        {
            laneNumbers = Enumerable.Range(1, 11).ToList();
        }

        var firstSessionDayByAthlete = BuildFirstSessionDayByAthlete(sessions);

        var rangeSessions = sessions
            .Where(s => IsSessionInLocalRange(s, from, to))
            .ToList();

        var rangeSubscriptions = schedules
            .Where(s => IsUtcInLocalRange(s.CreatedAtUtc, from, to))
            .ToList();

        var rangePackages = packageRecords
            .Where(r => IsUtcInLocalRange(r.CreatedAtUtc, from, to))
            .ToList();

        var rangeReceipts = receipts
            .Where(r => IsUtcInLocalRange(r.CreatedAtUtc, from, to))
            .ToList();

        var rangeReceiptLines = receiptLines
            .Where(l => rangeReceipts.Any(r => r.Id == l.ReceiptId))
            .ToList();

        var standaloneSaleReceipts = rangeReceipts
            .Where(r => r.Type == EquipmentSaleReceiptType.Sale)
            .ToList();
        var standaloneReturnReceipts = rangeReceipts
            .Where(r => r.Type == EquipmentSaleReceiptType.Return)
            .ToList();

        var standaloneSaleCount = rangeReceiptLines
            .Where(l => standaloneSaleReceipts.Any(r => r.Id == l.ReceiptId))
            .Sum(l => Math.Max(1, l.Quantity));
        var standaloneReturnCount = rangeReceiptLines
            .Where(l => standaloneReturnReceipts.Any(r => r.Id == l.ReceiptId))
            .Sum(l => Math.Max(1, l.Quantity));

        var packageMetrics = SummarizePackages(rangePackages);
        var standaloneMetrics = SummarizeStandaloneReceipts(standaloneSaleReceipts, standaloneReturnReceipts);

        var allRentalIssues = issues
            .Where(x => x.IssueType == EquipmentIssueType.Rental)
            .ToList();
        var rangeRentalIssued = allRentalIssues
            .Where(i => IsUtcInLocalRange(i.CreatedAtUtc, from, to))
            .ToList();
        var rangeRentalReturned = allRentalIssues
            .Where(i => i.ReturnedAtUtc is DateTime returned && IsUtcInLocalRange(returned, from, to))
            .ToList();
        var outstandingRentals = allRentalIssues
            .Where(i => i.ReturnedAtUtc is null)
            .ToList();

        var equipmentSaleCount = Math.Max(0, standaloneSaleCount - standaloneReturnCount);
        var equipmentSaleRevenue = standaloneMetrics.PaidTotal;

        var equipmentSaleDetails = BuildEquipmentSaleDetails(
            issues,
            sessions,
            athletesById,
            equipmentById,
            receiptLines,
            receipts,
            staffNameById,
            from,
            to);

        var laneActivity = BuildLaneActivity(rangeSessions, laneNoById, laneNumbers);
        var dailyBreakdown = BuildDailyBreakdown(
            from,
            to,
            rangeSessions,
            issues,
            rangeSubscriptions,
            rangePackages,
            rangeReceipts,
            receiptLines,
            firstSessionDayByAthlete);

        var uniqueAthleteIds = rangeSessions
            .Select(s => s.AthleteId)
            .Distinct()
            .Count();

        var newCustomerCount = rangeSessions
            .Select(s => s.AthleteId)
            .Distinct()
            .Count(athleteId =>
            {
                if (!firstSessionDayByAthlete.TryGetValue(athleteId, out var firstDay))
                {
                    return false;
                }

                return firstDay >= from && firstDay <= to;
            });

        var totalLaneHours = RoundHours(laneActivity.Sum(x => x.TotalHours));
        var busiestLane = laneActivity
            .Where(x => x.SessionCount > 0 || x.TotalHours > 0)
            .OrderByDescending(x => x.TotalHours)
            .ThenByDescending(x => x.SessionCount)
            .FirstOrDefault();

        var totalPriceDue = packageMetrics.PriceDue + standaloneMetrics.SaleDue;
        var totalPaidCash = packageMetrics.PaidCash + standaloneMetrics.PaidCash;
        var totalPaidCard = packageMetrics.PaidCard + standaloneMetrics.PaidCard;
        var totalPaid = packageMetrics.PaidTotal + standaloneMetrics.PaidTotal;
        var totalRemaining = packageMetrics.Remaining + standaloneMetrics.Remaining;

        var dailyTotals = BuildDailyTotals(dailyBreakdown, uniqueAthleteIds, newCustomerCount);

        var customerVisitDetails = BuildCustomerPaymentDetails(
            rangePackages,
            rangeReceipts,
            issues,
            athletesById,
            staffNameById);

        return new OperationsAnalyticsResult
        {
            Mode = request.Mode,
            FromLocal = from.ToString("yyyy-MM-dd"),
            ToLocal = to.ToString("yyyy-MM-dd"),
            Label = from == to ? from.ToString("yyyy-MM-dd") : $"{from:yyyy-MM-dd} — {to:yyyy-MM-dd}",
            SessionCount = rangeSessions.Count,
            UniqueCustomerCount = uniqueAthleteIds,
            NewCustomerCount = newCustomerCount,
            SubscriptionCreatedCount = rangeSubscriptions.Count,
            EquipmentSaleCount = Math.Max(0, equipmentSaleCount),
            EquipmentRentalIssuedCount = rangeRentalIssued.Sum(x => Math.Max(1, x.Quantity)),
            EquipmentRentalReturnedCount = rangeRentalReturned.Sum(x => Math.Max(1, x.Quantity)),
            EquipmentRentalOutstandingCount = outstandingRentals.Sum(x => Math.Max(1, x.Quantity)),
            EquipmentSaleRevenue = equipmentSaleRevenue,
            TotalLaneHours = totalLaneHours,
            BusiestLaneNumber = busiestLane?.LaneNumber,
            PackageRecordCount = packageMetrics.RecordCount,
            ComplimentaryCount = packageMetrics.ComplimentaryCount,
            PackagePriceDue = packageMetrics.PriceDue,
            PackagePaidCash = packageMetrics.PaidCash,
            PackagePaidCard = packageMetrics.PaidCard,
            PackagePaidTotal = packageMetrics.PaidTotal,
            PackageRemaining = packageMetrics.Remaining,
            StandaloneEquipmentSaleCount = Math.Max(0, standaloneSaleCount - standaloneReturnCount),
            StandaloneEquipmentSaleDue = standaloneMetrics.SaleDue,
            StandaloneEquipmentPaidCash = standaloneMetrics.PaidCash,
            StandaloneEquipmentPaidCard = standaloneMetrics.PaidCard,
            StandaloneEquipmentPaidTotal = standaloneMetrics.PaidTotal,
            StandaloneEquipmentRemaining = standaloneMetrics.Remaining,
            TotalPriceDue = totalPriceDue,
            TotalPaidCash = totalPaidCash,
            TotalPaidCard = totalPaidCard,
            TotalPaid = totalPaid,
            TotalRemaining = totalRemaining,
            DailyBreakdown = dailyBreakdown,
            DailyTotals = dailyTotals,
            LaneActivity = laneActivity,
            EquipmentSaleDetails = equipmentSaleDetails,
            CustomerVisitDetails = customerVisitDetails
        };
    }

    private sealed class PackageMetrics
    {
        public int RecordCount { get; init; }
        public int ComplimentaryCount { get; init; }
        public decimal PriceDue { get; init; }
        public decimal PaidCash { get; init; }
        public decimal PaidCard { get; init; }
        public decimal PaidTotal { get; init; }
        public decimal Remaining { get; init; }
    }

    private sealed class StandaloneReceiptMetrics
    {
        public decimal SaleDue { get; init; }
        public decimal PaidCash { get; init; }
        public decimal PaidCard { get; init; }
        public decimal PaidTotal { get; init; }
        public decimal Remaining { get; init; }
    }

    private static PackageMetrics SummarizePackages(IReadOnlyCollection<CustomerPackageRecord> records)
    {
        return new PackageMetrics
        {
            RecordCount = records.Count,
            ComplimentaryCount = records.Count(r => r.IsComplimentary),
            PriceDue = records.Sum(r => r.PriceDue),
            PaidCash = records.Sum(r => r.AmountPaidCash),
            PaidCard = records.Sum(r => r.AmountPaidCard),
            PaidTotal = records.Sum(r => r.AmountPaid),
            Remaining = records.Sum(r => r.DiscountAmount)
        };
    }

    private static StandaloneReceiptMetrics SummarizeStandaloneReceipts(
        IReadOnlyCollection<EquipmentSaleReceipt> sales,
        IReadOnlyCollection<EquipmentSaleReceipt> returns)
    {
        var saleDue = sales.Sum(r => r.TotalAmount) - returns.Sum(r => r.TotalAmount);
        var paidCash = sales.Sum(r => r.AmountPaidCash) - returns.Sum(r => r.AmountPaidCash);
        var paidCard = sales.Sum(r => r.AmountPaidCard) - returns.Sum(r => r.AmountPaidCard);
        var paidTotal = sales.Sum(r => r.AmountPaid) - returns.Sum(r => r.AmountPaid);
        var remaining = sales.Sum(r => r.DiscountAmount);

        return new StandaloneReceiptMetrics
        {
            SaleDue = saleDue,
            PaidCash = paidCash,
            PaidCard = paidCard,
            PaidTotal = paidTotal,
            Remaining = Math.Max(0m, remaining)
        };
    }

    private static List<DailyOperationsRow> BuildDailyBreakdown(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<TrainingSession> rangeSessions,
        IReadOnlyCollection<SessionEquipmentIssue> allIssues,
        IReadOnlyCollection<SubscriptionSchedule> rangeSubscriptions,
        IReadOnlyCollection<CustomerPackageRecord> rangePackages,
        IReadOnlyCollection<EquipmentSaleReceipt> rangeReceipts,
        IReadOnlyCollection<EquipmentSaleReceiptLine> allReceiptLines,
        IReadOnlyDictionary<Guid, DateTime> firstSessionDayByAthlete)
    {
        var allRentalIssues = allIssues
            .Where(x => x.IssueType == EquipmentIssueType.Rental)
            .ToList();

        var rows = new List<DailyOperationsRow>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var daySessions = rangeSessions
                .Where(s => ToLocalDate(StartUtc(s)) == day)
                .ToList();
            var daySubscriptions = rangeSubscriptions
                .Where(s => ToLocalDate(AssumedUtc(s.CreatedAtUtc)) == day)
                .ToList();
            var dayPackages = rangePackages
                .Where(r => ToLocalDate(AssumedUtc(r.CreatedAtUtc)) == day)
                .ToList();
            var dayReceipts = rangeReceipts
                .Where(r => ToLocalDate(AssumedUtc(r.CreatedAtUtc)) == day)
                .ToList();
            var daySaleReceipts = dayReceipts.Where(r => r.Type == EquipmentSaleReceiptType.Sale).ToList();
            var dayReturnReceipts = dayReceipts.Where(r => r.Type == EquipmentSaleReceiptType.Return).ToList();
            var dayReceiptIds = dayReceipts.Select(r => r.Id).ToHashSet();
            var dayReceiptLines = allReceiptLines.Where(l => dayReceiptIds.Contains(l.ReceiptId)).ToList();

            var uniqueAthletes = daySessions.Select(s => s.AthleteId).Distinct().ToList();
            var newCustomers = uniqueAthletes.Count(athleteId =>
                firstSessionDayByAthlete.TryGetValue(athleteId, out var firstDay) && firstDay == day);

            var laneHours = daySessions.Sum(s => SessionDurationHours(s));

            var dayRentalIssued = allRentalIssues
                .Where(i => ToLocalDate(AssumedUtc(i.CreatedAtUtc)) == day)
                .Sum(x => Math.Max(1, x.Quantity));
            var dayRentalReturned = allRentalIssues
                .Where(i => i.ReturnedAtUtc is DateTime returned && ToLocalDate(AssumedUtc(returned)) == day)
                .Sum(x => Math.Max(1, x.Quantity));

            var packageMetrics = SummarizePackages(dayPackages);
            var standaloneMetrics = SummarizeStandaloneReceipts(daySaleReceipts, dayReturnReceipts);
            var standaloneSaleCount = dayReceiptLines
                .Where(l => daySaleReceipts.Any(r => r.Id == l.ReceiptId))
                .Sum(l => Math.Max(1, l.Quantity));
            var standaloneReturnCount = dayReceiptLines
                .Where(l => dayReturnReceipts.Any(r => r.Id == l.ReceiptId))
                .Sum(l => Math.Max(1, l.Quantity));

            var equipmentSaleCount = Math.Max(0, standaloneSaleCount - standaloneReturnCount);
            var equipmentSaleRevenue = standaloneMetrics.PaidTotal;

            var totalPriceDue = packageMetrics.PriceDue + standaloneMetrics.SaleDue;
            var totalPaidCash = packageMetrics.PaidCash + standaloneMetrics.PaidCash;
            var totalPaidCard = packageMetrics.PaidCard + standaloneMetrics.PaidCard;
            var totalPaid = packageMetrics.PaidTotal + standaloneMetrics.PaidTotal;
            var totalRemaining = packageMetrics.Remaining + standaloneMetrics.Remaining;

            rows.Add(new DailyOperationsRow
            {
                DateLocal = day.ToString("yyyy-MM-dd"),
                SessionCount = daySessions.Count,
                UniqueCustomerCount = uniqueAthletes.Count,
                NewCustomerCount = newCustomers,
                SubscriptionCreatedCount = daySubscriptions.Count,
                EquipmentSaleCount = equipmentSaleCount,
                EquipmentRentalIssuedCount = dayRentalIssued,
                EquipmentRentalReturnedCount = dayRentalReturned,
                EquipmentSaleRevenue = equipmentSaleRevenue,
                LaneHoursTotal = RoundHours(laneHours),
                PackageRecordCount = packageMetrics.RecordCount,
                ComplimentaryCount = packageMetrics.ComplimentaryCount,
                PackagePriceDue = packageMetrics.PriceDue,
                PackagePaidCash = packageMetrics.PaidCash,
                PackagePaidCard = packageMetrics.PaidCard,
                PackagePaidTotal = packageMetrics.PaidTotal,
                PackageRemaining = packageMetrics.Remaining,
                StandaloneEquipmentSaleCount = Math.Max(0, standaloneSaleCount - standaloneReturnCount),
                StandaloneEquipmentSaleDue = standaloneMetrics.SaleDue,
                StandaloneEquipmentPaidCash = standaloneMetrics.PaidCash,
                StandaloneEquipmentPaidCard = standaloneMetrics.PaidCard,
                StandaloneEquipmentPaidTotal = standaloneMetrics.PaidTotal,
                StandaloneEquipmentRemaining = standaloneMetrics.Remaining,
                TotalPriceDue = totalPriceDue,
                TotalPaidCash = totalPaidCash,
                TotalPaidCard = totalPaidCard,
                TotalPaid = totalPaid,
                TotalRemaining = totalRemaining
            });
        }

        return rows;
    }

    private static DailyBreakdownTotals BuildDailyTotals(
        IReadOnlyCollection<DailyOperationsRow> rows,
        int periodUniqueCustomers,
        int periodNewCustomers)
    {
        return new DailyBreakdownTotals
        {
            SessionCount = rows.Sum(x => x.SessionCount),
            UniqueCustomerCount = periodUniqueCustomers,
            NewCustomerCount = periodNewCustomers,
            SubscriptionCreatedCount = rows.Sum(x => x.SubscriptionCreatedCount),
            EquipmentSaleCount = rows.Sum(x => x.EquipmentSaleCount),
            EquipmentRentalIssuedCount = rows.Sum(x => x.EquipmentRentalIssuedCount),
            EquipmentRentalReturnedCount = rows.Sum(x => x.EquipmentRentalReturnedCount),
            EquipmentSaleRevenue = rows.Sum(x => x.EquipmentSaleRevenue),
            LaneHoursTotal = RoundHours(rows.Sum(x => x.LaneHoursTotal)),
            PackageRecordCount = rows.Sum(x => x.PackageRecordCount),
            ComplimentaryCount = rows.Sum(x => x.ComplimentaryCount),
            PackagePriceDue = rows.Sum(x => x.PackagePriceDue),
            PackagePaidCash = rows.Sum(x => x.PackagePaidCash),
            PackagePaidCard = rows.Sum(x => x.PackagePaidCard),
            PackagePaidTotal = rows.Sum(x => x.PackagePaidTotal),
            PackageRemaining = rows.Sum(x => x.PackageRemaining),
            StandaloneEquipmentSaleCount = rows.Sum(x => x.StandaloneEquipmentSaleCount),
            StandaloneEquipmentSaleDue = rows.Sum(x => x.StandaloneEquipmentSaleDue),
            StandaloneEquipmentPaidCash = rows.Sum(x => x.StandaloneEquipmentPaidCash),
            StandaloneEquipmentPaidCard = rows.Sum(x => x.StandaloneEquipmentPaidCard),
            StandaloneEquipmentPaidTotal = rows.Sum(x => x.StandaloneEquipmentPaidTotal),
            StandaloneEquipmentRemaining = rows.Sum(x => x.StandaloneEquipmentRemaining),
            TotalPriceDue = rows.Sum(x => x.TotalPriceDue),
            TotalPaidCash = rows.Sum(x => x.TotalPaidCash),
            TotalPaidCard = rows.Sum(x => x.TotalPaidCard),
            TotalPaid = rows.Sum(x => x.TotalPaid),
            TotalRemaining = rows.Sum(x => x.TotalRemaining)
        };
    }

    private static List<EquipmentSaleDetailRow> BuildEquipmentSaleDetails(
        IReadOnlyCollection<SessionEquipmentIssue> allIssues,
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyDictionary<Guid, Athlete> athletesById,
        IReadOnlyDictionary<Guid, EquipmentItem> equipmentById,
        IReadOnlyCollection<EquipmentSaleReceiptLine> allReceiptLines,
        IReadOnlyCollection<EquipmentSaleReceipt> allReceipts,
        IReadOnlyDictionary<Guid, string> staffNameById,
        DateTime from,
        DateTime to)
    {
        string StaffName(Guid? id) =>
            id is Guid gid && staffNameById.TryGetValue(gid, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "—";

        var outstandingByItem = allIssues
            .Where(x => x.IssueType == EquipmentIssueType.Rental && x.ReturnedAtUtc is null)
            .GroupBy(x => x.EquipmentItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(1, x.Quantity)));

        (int Total, int InHall, int ForSale) InventoryFor(Guid equipmentId)
        {
            if (!equipmentById.TryGetValue(equipmentId, out var item))
            {
                return (0, 0, 0);
            }

            var inHall = outstandingByItem.GetValueOrDefault(equipmentId, 0);
            var forSale = Math.Max(0, item.SaleQuantity);
            var forRental = Math.Max(0, item.RentalQuantity);
            return (forSale + forRental + inHall, inHall, forSale);
        }

        var sessionsById = sessions.ToDictionary(x => x.Id);
        var rows = new List<EquipmentSaleDetailRow>();

        foreach (var issue in allIssues.Where(x => x.IssueType == EquipmentIssueType.Sale))
        {
            if (!IsUtcInLocalRange(issue.CreatedAtUtc, from, to))
            {
                continue;
            }

            if (!equipmentById.TryGetValue(issue.EquipmentItemId, out var item))
            {
                continue;
            }

            sessionsById.TryGetValue(issue.SessionId ?? Guid.Empty, out var session);
            Athlete? athlete = session is not null && athletesById.TryGetValue(session.AthleteId, out var a)
                ? a
                : null;
            var qty = Math.Max(1, issue.Quantity);
            var unitPrice = issue.UnitPrice > 0
                ? issue.UnitPrice
                : EquipmentIssuanceRules.ResolveUnitPrice(item, issue.IssueType);
            var inv = InventoryFor(issue.EquipmentItemId);
            var local = ToLocalDateTime(issue.CreatedAtUtc);

            rows.Add(new EquipmentSaleDetailRow
            {
                DateLocal = local.ToString("yyyy-MM-dd"),
                TimeLocal = local.ToString("HH:mm"),
                EquipmentName = item.Name,
                TotalQuantity = inv.Total,
                InHallQuantity = inv.InHall,
                ForSaleQuantity = inv.ForSale,
                SoldQuantity = qty,
                UnitPrice = unitPrice,
                LineTotal = 0m,
                PaidCash = 0m,
                PaidCard = 0m,
                DiscountAmount = 0m,
                CustomerName = athlete?.FullName ?? "—",
                SoldByStaffName = StaffName(issue.IssuedByStaffId),
                SaleSource = "Zal (seans)"
            });
        }

        foreach (var receipt in allReceipts.Where(r => IsUtcInLocalRange(r.CreatedAtUtc, from, to)))
        {
            var isReturn = receipt.Type == EquipmentSaleReceiptType.Return;
            var sign = isReturn ? -1 : 1;
            var lines = allReceiptLines.Where(l => l.ReceiptId == receipt.Id).ToList();
            athletesById.TryGetValue(receipt.AthleteId, out var athlete);
            var receiptTotal = Math.Abs(receipt.TotalAmount);
            var receiptDiscount = Math.Abs(receipt.DiscountAmount);
            var local = ToLocalDateTime(receipt.CreatedAtUtc);

            foreach (var line in lines)
            {
                if (!equipmentById.TryGetValue(line.EquipmentItemId, out var item))
                {
                    continue;
                }

                var qty = Math.Max(1, line.Quantity) * sign;
                var unitPrice = line.UnitPrice;
                var lineList = unitPrice * Math.Max(1, line.Quantity);
                var lineTotal = lineList * sign;
                var lineDiscountRaw = Math.Max(0m, line.DiscountAmount);
                if (lineDiscountRaw <= 0m && receiptDiscount > 0m && receiptTotal > 0m)
                {
                    lineDiscountRaw = receiptDiscount * (lineList / receiptTotal);
                }

                var discount = lineDiscountRaw * sign;
                var lineNet = Math.Max(0m, lineList - lineDiscountRaw);
                var share = receiptTotal > 0 ? lineNet / receiptTotal : 1m;
                var cash = receipt.AmountPaidCash * share * sign;
                var card = receipt.AmountPaidCard * share * sign;
                var inv = InventoryFor(line.EquipmentItemId);

                rows.Add(new EquipmentSaleDetailRow
                {
                    DateLocal = local.ToString("yyyy-MM-dd"),
                    TimeLocal = local.ToString("HH:mm"),
                    EquipmentName = item.Name,
                    TotalQuantity = inv.Total,
                    InHallQuantity = inv.InHall,
                    ForSaleQuantity = inv.ForSale,
                    SoldQuantity = qty,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                    PaidCash = cash,
                    PaidCard = card,
                    DiscountAmount = discount,
                    CustomerName = athlete?.FullName ?? "—",
                    SoldByStaffName = StaffName(receipt.CreatedByStaffId),
                    SaleSource = isReturn ? "Qaytarma (resepsiya)" : "Resepsiya satışı"
                });
            }
        }

        return rows
            .OrderByDescending(r => r.DateLocal)
            .ThenByDescending(r => r.TimeLocal)
            .ThenBy(r => r.EquipmentName)
            .ToList();
    }

    private static List<CustomerVisitDetailRow> BuildCustomerPaymentDetails(
        IReadOnlyCollection<CustomerPackageRecord> rangePackages,
        IReadOnlyCollection<EquipmentSaleReceipt> rangeReceipts,
        IReadOnlyCollection<SessionEquipmentIssue> allIssues,
        IReadOnlyDictionary<Guid, Athlete> athletesById,
        IReadOnlyDictionary<Guid, string> staffNameById)
    {
        string StaffName(Guid? id) =>
            id is Guid gid && staffNameById.TryGetValue(gid, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "—";

        var saleReceipts = rangeReceipts
            .Where(r => r.Type == EquipmentSaleReceiptType.Sale)
            .OrderBy(r => AssumedUtc(r.CreatedAtUtc))
            .ToList();
        var usedReceiptIds = new HashSet<Guid>();
        var rows = new List<CustomerVisitDetailRow>();

        foreach (var package in rangePackages.OrderByDescending(p => AssumedUtc(p.CreatedAtUtc)))
        {
            athletesById.TryGetValue(package.AthleteId, out var athlete);
            var packageUtc = AssumedUtc(package.CreatedAtUtc);
            var recordedLocal = ToLocalDateTime(packageUtc);

            EquipmentSaleReceipt? linked = null;
            var equipmentAmount = 0m;

            if (package.SessionId is Guid sessionId)
            {
                var sessionSaleTotal = allIssues
                    .Where(i => i.SessionId == sessionId && i.IssueType == EquipmentIssueType.Sale)
                    .Sum(i => i.UnitPrice * Math.Max(1, i.Quantity));

                if (sessionSaleTotal > PaymentSettlementRules.Tolerance)
                {
                    linked = FindLinkedEquipmentReceipt(
                        saleReceipts,
                        usedReceiptIds,
                        package.AthleteId,
                        packageUtc,
                        sessionSaleTotal);
                    equipmentAmount = linked?.TotalAmount ?? sessionSaleTotal;
                }
            }

            if (linked is not null)
            {
                usedReceiptIds.Add(linked.Id);
            }

            var cash = package.AmountPaidCash + (linked?.AmountPaidCash ?? 0m);
            var card = package.AmountPaidCard + (linked?.AmountPaidCard ?? 0m);
            var discount = package.DiscountAmount + (linked?.DiscountAmount ?? 0m);

            rows.Add(new CustomerVisitDetailRow
            {
                DateLocal = recordedLocal.ToString("yyyy-MM-dd"),
                RecordedAtLocal = recordedLocal.ToString("yyyy-MM-dd HH:mm"),
                CustomerName = athlete?.FullName ?? "—",
                Phone = athlete?.PhoneNumber ?? "—",
                ReceptionStaffName = StaffName(package.CreatedByStaffId),
                PackageName = string.IsNullOrWhiteSpace(package.PackageName) ? "—" : package.PackageName,
                PriceDue = package.PriceDue,
                EquipmentAmount = equipmentAmount,
                DiscountAmount = discount,
                AmountPaidCash = cash,
                AmountPaidCard = card,
                AmountPaid = cash + card,
                IsComplimentary = package.IsComplimentary
            });
        }

        foreach (var receipt in saleReceipts
                     .Where(r => !usedReceiptIds.Contains(r.Id))
                     .OrderByDescending(r => AssumedUtc(r.CreatedAtUtc)))
        {
            athletesById.TryGetValue(receipt.AthleteId, out var athlete);
            var recordedLocal = ToLocalDateTime(AssumedUtc(receipt.CreatedAtUtc));
            rows.Add(new CustomerVisitDetailRow
            {
                DateLocal = recordedLocal.ToString("yyyy-MM-dd"),
                RecordedAtLocal = recordedLocal.ToString("yyyy-MM-dd HH:mm"),
                CustomerName = athlete?.FullName ?? "—",
                Phone = athlete?.PhoneNumber ?? "—",
                ReceptionStaffName = StaffName(receipt.CreatedByStaffId),
                PackageName = "Yalnız avadanlıq",
                PriceDue = 0m,
                EquipmentAmount = receipt.TotalAmount,
                DiscountAmount = receipt.DiscountAmount,
                AmountPaidCash = receipt.AmountPaidCash,
                AmountPaidCard = receipt.AmountPaidCard,
                AmountPaid = receipt.AmountPaid,
                IsComplimentary = false
            });
        }

        return rows
            .OrderByDescending(r => r.RecordedAtLocal)
            .ThenBy(r => r.CustomerName)
            .ToList();
    }

    private static EquipmentSaleReceipt? FindLinkedEquipmentReceipt(
        IReadOnlyList<EquipmentSaleReceipt> saleReceipts,
        HashSet<Guid> usedReceiptIds,
        Guid athleteId,
        DateTime packageUtc,
        decimal sessionSaleTotal)
    {
        var candidates = saleReceipts
            .Where(r => r.AthleteId == athleteId && !usedReceiptIds.Contains(r.Id))
            .Where(r => Math.Abs((AssumedUtc(r.CreatedAtUtc) - packageUtc).TotalMinutes) <= 5)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var amountMatch = candidates
            .Where(r => Math.Abs(r.TotalAmount - sessionSaleTotal) <= 0.05m)
            .OrderBy(r => Math.Abs((AssumedUtc(r.CreatedAtUtc) - packageUtc).TotalSeconds))
            .FirstOrDefault();
        if (amountMatch is not null)
        {
            return amountMatch;
        }

        return candidates
            .Where(r => Math.Abs((AssumedUtc(r.CreatedAtUtc) - packageUtc).TotalMinutes) <= 2)
            .OrderBy(r => Math.Abs((AssumedUtc(r.CreatedAtUtc) - packageUtc).TotalSeconds))
            .FirstOrDefault();
    }

    private static List<LaneActivityRow> BuildLaneActivity(
        IReadOnlyCollection<TrainingSession> rangeSessions,
        IReadOnlyDictionary<Guid, int> laneNoById,
        IReadOnlyList<int> laneNumbers)
    {
        var count = laneNumbers.ToDictionary(n => n, _ => 0);
        var hours = laneNumbers.ToDictionary(n => n, _ => 0.0);

        foreach (var session in rangeSessions)
        {
            if (!laneNoById.TryGetValue(session.LaneId, out var laneNo) || laneNo <= 0)
            {
                continue;
            }

            if (!count.ContainsKey(laneNo))
            {
                count[laneNo] = 0;
                hours[laneNo] = 0;
            }

            count[laneNo]++;
            hours[laneNo] += SessionDurationHours(session);
        }

        return laneNumbers
            .Select(n => new LaneActivityRow
            {
                LaneNumber = n,
                SessionCount = count.GetValueOrDefault(n),
                TotalHours = RoundHours(hours.GetValueOrDefault(n))
            })
            .ToList();
    }

    private static Dictionary<Guid, DateTime> BuildFirstSessionDayByAthlete(
        IReadOnlyCollection<TrainingSession> sessions)
    {
        var map = new Dictionary<Guid, DateTime>();
        foreach (var session in sessions)
        {
            var day = ToLocalDate(StartUtc(session));
            if (!map.TryGetValue(session.AthleteId, out var existing) || day < existing)
            {
                map[session.AthleteId] = day;
            }
        }

        return map;
    }

    private static bool IsSessionInLocalRange(
        TrainingSession session,
        DateTime fromLocalDate,
        DateTime toLocalDate)
    {
        var day = ToLocalDate(StartUtc(session));
        return day >= fromLocalDate && day <= toLocalDate;
    }

    private static bool IsUtcInLocalRange(DateTime utcValue, DateTime fromLocalDate, DateTime toLocalDate)
    {
        var day = ToLocalDate(AssumedUtc(utcValue));
        return day >= fromLocalDate && day <= toLocalDate;
    }

    private static double SessionDurationHours(TrainingSession session)
    {
        var start = StartUtc(session);
        var end = AssumedUtc(session.EndTimeUtc);
        if (end <= start)
        {
            return 0;
        }

        return Math.Max(0, (end - start).TotalHours);
    }

    private static DateTime StartUtc(TrainingSession session)
        => AssumedUtc(session.StartTimeUtc);

    private static DateTime AssumedUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime ToLocalDate(DateTime utc) => utc.ToLocalTime().Date;

    private static DateTime ToLocalDateTime(DateTime utc) => AssumedUtc(utc).ToLocalTime();

    private static double RoundHours(double h) => Math.Round(h, 2);
}
