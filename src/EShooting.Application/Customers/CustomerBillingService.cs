using EShooting.Application.Common.Interfaces;
using EShooting.Application.Equipment;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Customers;

public static class CustomerBillingService
{
    public static async Task<CustomerPackageRecord> RecordPackageAsync(
        ITrainingCenterRepository repository,
        Guid athleteId,
        Guid? servicePackageId,
        string? packageNameFallback,
        string? billingTypeFallback,
        decimal? priceOverride,
        decimal discountAmount,
        decimal amountPaidCash,
        decimal amountPaidCard,
        bool isComplimentary,
        Guid? sessionId,
        Guid? subscriptionScheduleId,
        Guid? createdByStaffId,
        CancellationToken cancellationToken)
    {
        decimal listPrice = 0m;
        var name = packageNameFallback ?? "Paket";
        var billingLabel = billingTypeFallback ?? "—";

        if (servicePackageId is Guid pkgId && pkgId != Guid.Empty)
        {
            var pkg = await repository.GetServicePackageByIdAsync(pkgId, cancellationToken);
            if (pkg is not null)
            {
                listPrice = pkg.Price;
                name = pkg.Name;
                billingLabel = pkg.BillingType.ToString();
            }
        }

        if (priceOverride is decimal ov)
        {
            listPrice = ov;
        }

        var settlement = PaymentSettlementRules.Resolve(
            listPrice,
            isComplimentary ? listPrice : discountAmount,
            amountPaidCash,
            amountPaidCard,
            isComplimentary);

        var record = new CustomerPackageRecord
        {
            AthleteId = athleteId,
            ServicePackageId = servicePackageId,
            PackageName = name,
            BillingTypeLabel = billingLabel,
            PriceDue = settlement.ListPrice,
            DiscountAmount = settlement.DiscountAmount,
            AmountPaidCash = settlement.Cash,
            AmountPaidCard = settlement.Card,
            AmountPaid = settlement.TotalPaid,
            IsComplimentary = isComplimentary,
            SessionId = sessionId,
            SubscriptionScheduleId = subscriptionScheduleId,
            CreatedByStaffId = createdByStaffId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        return await repository.AddCustomerPackageRecordAsync(record, cancellationToken);
    }

    public static async Task RecordSessionBookingBillingAsync(
        ITrainingCenterRepository repository,
        Guid athleteId,
        Guid servicePackageId,
        Guid sessionId,
        decimal discountAmount,
        decimal amountPaidCash,
        decimal amountPaidCard,
        bool isComplimentary,
        Guid? createdByStaffId,
        CancellationToken cancellationToken)
    {
        var pkg = await repository.GetServicePackageByIdAsync(servicePackageId, cancellationToken);
        var packageListPrice = pkg?.Price ?? 0m;
        var sessionIssues = (await repository.GetSessionEquipmentIssuesAsync(cancellationToken))
            .Where(i => i.SessionId == sessionId)
            .ToList();
        var saleIssues = sessionIssues.Where(i => i.IssueType == EquipmentIssueType.Sale).ToList();
        var equipmentListPrice = saleIssues.Sum(i => i.UnitPrice * Math.Max(1, i.Quantity));

        if (isComplimentary)
        {
            await RecordPackageAsync(
                repository,
                athleteId,
                servicePackageId,
                null,
                null,
                packageListPrice,
                0m,
                0m,
                0m,
                true,
                sessionId,
                null,
                createdByStaffId,
                cancellationToken);
            return;
        }

        var totalList = packageListPrice + equipmentListPrice;
        PaymentSettlementRules.Resolve(
            totalList,
            discountAmount,
            amountPaidCash,
            amountPaidCard,
            false);

        var split = PaymentSettlementRules.SplitCombinedCheckout(
            packageListPrice,
            equipmentListPrice,
            discountAmount,
            amountPaidCash,
            amountPaidCard);

        await RecordPackageAsync(
            repository,
            athleteId,
            servicePackageId,
            null,
            null,
            packageListPrice,
            split.Package.DiscountAmount,
            split.Package.Cash,
            split.Package.Card,
            false,
            sessionId,
            null,
            createdByStaffId,
            cancellationToken);

        if (equipmentListPrice <= PaymentSettlementRules.Tolerance || saleIssues.Count == 0)
        {
            return;
        }

        var equipmentList = split.Equipment.ListPrice;
        var equipmentDiscount = split.Equipment.DiscountAmount;
        var receiptLines = saleIssues.Select(i =>
        {
            var qty = Math.Max(1, i.Quantity);
            var lineList = i.UnitPrice * qty;
            var lineDiscount = equipmentList > PaymentSettlementRules.Tolerance
                ? Math.Round(equipmentDiscount * (lineList / equipmentList), 2, MidpointRounding.AwayFromZero)
                : 0m;
            return new EquipmentSaleReceiptLine
            {
                ReceiptId = Guid.Empty,
                EquipmentItemId = i.EquipmentItemId,
                Quantity = qty,
                UnitPrice = i.UnitPrice,
                DiscountAmount = lineDiscount
            };
        }).ToList();

        var receipt = new EquipmentSaleReceipt
        {
            AthleteId = athleteId,
            Type = EquipmentSaleReceiptType.Sale,
            TotalAmount = split.Equipment.ListPrice,
            DiscountAmount = split.Equipment.DiscountAmount,
            AmountPaidCash = split.Equipment.Cash,
            AmountPaidCard = split.Equipment.Card,
            CreatedByStaffId = createdByStaffId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddEquipmentSaleReceiptAsync(receipt, receiptLines, cancellationToken);
    }

    public static EquipmentSaleReceipt BuildEquipmentSaleReceipt(
        Guid athleteId,
        decimal totalListPrice,
        decimal discountAmount,
        decimal amountPaidCash,
        decimal amountPaidCard,
        Guid? createdByStaffId)
    {
        var settlement = PaymentSettlementRules.Resolve(
            totalListPrice,
            discountAmount,
            amountPaidCash,
            amountPaidCard,
            false);

        return new EquipmentSaleReceipt
        {
            AthleteId = athleteId,
            Type = EquipmentSaleReceiptType.Sale,
            TotalAmount = settlement.ListPrice,
            DiscountAmount = settlement.DiscountAmount,
            AmountPaidCash = settlement.Cash,
            AmountPaidCard = settlement.Card,
            CreatedByStaffId = createdByStaffId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static decimal SumEquipmentDue(
        Guid athleteId,
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyCollection<SessionEquipmentIssue> issues,
        IReadOnlyCollection<EquipmentItem> equipment)
    {
        var sessionIds = sessions.Where(s => s.AthleteId == athleteId).Select(s => s.Id).ToHashSet();
        if (sessionIds.Count == 0)
        {
            return 0m;
        }

        var priceById = equipment.ToDictionary(
            x => x.Id,
            x => EquipmentIssuanceRules.ResolveUnitPrice(x));
        return issues
            .Where(i => sessionIds.Contains(i.SessionId))
            .Sum(i => priceById.GetValueOrDefault(i.EquipmentItemId));
    }
}
