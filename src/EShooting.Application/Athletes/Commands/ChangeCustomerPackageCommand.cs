using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Customers;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record ChangeCustomerPackagePreview(
    Guid AthleteId,
    string AthleteName,
    string? OldPackageName,
    decimal OldAmountPaid,
    Guid NewServicePackageId,
    string NewPackageName,
    decimal NewListPrice,
    decimal DiscountAmount,
    decimal NewPayable,
    decimal AppliedCredit,
    decimal AdditionalDue,
    decimal RefundDue,
    string DifferenceKind);

public sealed record ChangeCustomerPackageResult(
    Guid NewPackageRecordId,
    Guid? RefundRecordId,
    string Message);

public sealed record ChangeCustomerPackageCommand(
    Guid AthleteId,
    Guid NewServicePackageId,
    DateTime PeriodStartLocal,
    DateTime PeriodEndLocal,
    decimal DiscountAmount,
    decimal AmountPaidCash,
    decimal AmountPaidCard,
    bool IsComplimentary,
    bool ConfirmDifference,
    Guid? CreatedByStaffId,
    bool CanApplyDiscount,
    bool CanGrantComplimentary) : IRequest<ChangeCustomerPackageResult>;

public sealed class ChangeCustomerPackageCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<ChangeCustomerPackageCommand, ChangeCustomerPackageResult>
{
    public static async Task<ChangeCustomerPackagePreview> BuildPreviewAsync(
        ITrainingCenterRepository repository,
        Guid athleteId,
        Guid newServicePackageId,
        decimal discountAmount,
        CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(athleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        var newPkg = await repository.GetServicePackageByIdAsync(newServicePackageId, cancellationToken)
            ?? throw new InvalidOperationException("Yeni paket tapılmadı.");
        if (!newPkg.IsActive || newPkg.IsDeleted)
        {
            throw new InvalidOperationException("Seçilmiş paket aktiv deyil.");
        }

        var oldRecord = (await repository.GetCustomerPackageRecordsAsync(cancellationToken))
            .Where(r => r.AthleteId == athleteId && r.IsActive)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();

        var oldPaid = oldRecord?.AmountPaid ?? 0m;
        var newList = Math.Max(0m, newPkg.Price);
        var userDiscount = Math.Clamp(Math.Max(0m, discountAmount), 0m, newList);
        var newPayable = Math.Max(0m, newList - userDiscount);
        var appliedCredit = Math.Min(oldPaid, newPayable);
        var additionalDue = Math.Max(0m, newPayable - oldPaid);
        var refundDue = Math.Max(0m, oldPaid - newPayable);

        var kind = additionalDue > PaymentSettlementRules.Tolerance
            ? "additional"
            : refundDue > PaymentSettlementRules.Tolerance
                ? "refund"
                : "even";

        return new ChangeCustomerPackagePreview(
            athlete.Id,
            athlete.FullName,
            oldRecord?.PackageName,
            oldPaid,
            newPkg.Id,
            newPkg.Name,
            newList,
            userDiscount,
            newPayable,
            appliedCredit,
            additionalDue,
            refundDue,
            kind);
    }

    public async Task<ChangeCustomerPackageResult> Handle(
        ChangeCustomerPackageCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmDifference)
        {
            throw new InvalidOperationException("Paket dəyişimi üçün ödəniş fərqini təsdiq edin.");
        }

        PaymentSettlementRules.EnsureDiscountAllowed(request.DiscountAmount, request.CanApplyDiscount);

        var preview = await BuildPreviewAsync(
            repository,
            request.AthleteId,
            request.NewServicePackageId,
            request.DiscountAmount,
            cancellationToken);

        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");
        var newPkg = await repository.GetServicePackageByIdAsync(request.NewServicePackageId, cancellationToken)
            ?? throw new InvalidOperationException("Yeni paket tapılmadı.");

        var periodStart = request.PeriodStartLocal.Date;
        var periodEnd = request.PeriodEndLocal.Date;
        if (periodEnd < periodStart)
        {
            throw new InvalidOperationException("Abunə bitmə tarixi başlanğıcdan əvvəl ola bilməz.");
        }

        if (request.IsComplimentary)
        {
            if (!request.CanGrantComplimentary)
            {
                throw new InvalidOperationException("Pulsuz paket dəyişimi üçün icazəniz yoxdur.");
            }

            if (request.AmountPaidCash > PaymentSettlementRules.Tolerance
                || request.AmountPaidCard > PaymentSettlementRules.Tolerance)
            {
                throw new InvalidOperationException("Pulsuz seçildikdə əlavə ödəniş yazıla bilməz.");
            }

            if (preview.DifferenceKind == "refund")
            {
                throw new InvalidOperationException("Qaytarma olan dəyişimdə «Pulsuz» seçilə bilməz — qaytarma məbləğini yazın.");
            }
        }
        else if (preview.DifferenceKind == "additional")
        {
            PaymentSettlementRules.Resolve(
                preview.AdditionalDue,
                0m,
                request.AmountPaidCash,
                request.AmountPaidCard,
                false);
        }
        else if (preview.DifferenceKind == "refund")
        {
            PaymentSettlementRules.EnsureRefundSplitMatches(
                preview.RefundDue,
                request.AmountPaidCash,
                request.AmountPaidCard);
        }
        else
        {
            if (request.AmountPaidCash > PaymentSettlementRules.Tolerance
                || request.AmountPaidCard > PaymentSettlementRules.Tolerance)
            {
                throw new InvalidOperationException("Ödəniş fərqi yoxdur; nağd/kart məbləği daxil edilməməlidir.");
            }
        }

        // 1) Köhnə abunələri bağla
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        foreach (var schedule in schedules.Where(s => s.AthleteId == athlete.Id && s.IsEnabled))
        {
            schedule.IsEnabled = false;
            await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
        }

        // 2) Köhnə aktiv paket ödənişlərini deaktiv et
        var packageRecords = await repository.GetCustomerPackageRecordsAsync(cancellationToken);
        foreach (var old in packageRecords.Where(r => r.AthleteId == athlete.Id && r.IsActive))
        {
            old.IsActive = false;
            await repository.UpdateCustomerPackageRecordAsync(old, cancellationToken);
        }

        // 3) Yeni walk-in / full abunə
        var duration = Math.Max(0, newPkg.SessionDurationMinutes);
        var markVip = ServicePackageRules.IsVipPackage(newPkg);
        athlete.IsSubscriber = true;
        athlete.IsFullPackage = true;
        athlete.IsVip = markVip;
        athlete.MembershipType = ResolveMembershipType(newPkg);
        await repository.UpdateAthleteAsync(athlete, cancellationToken);

        var newSchedule = await repository.AddSubscriptionScheduleAsync(
            new SubscriptionSchedule
            {
                AthleteId = athlete.Id,
                LaneNumber = newPkg.Scope == PackageScope.Gym || newPkg.BillingType == PackageBillingType.Gym
                    ? GymLaneRules.LaneNumber
                    : 0,
                DayOfWeek = 0,
                StartTimeLocal = TimeSpan.Zero,
                DurationMinutes = duration,
                ActiveFromDateLocal = periodStart,
                ActiveToDateLocal = periodEnd,
                IsEnabled = true,
                PreferredLaneType = PreferredLaneType.Any,
                IsFullPackage = true
            },
            cancellationToken);

        // 4) Yeni paket ödəniş qeydi
        var effectiveDiscount = preview.DiscountAmount + preview.AppliedCredit;
        if (effectiveDiscount > preview.NewListPrice)
        {
            effectiveDiscount = preview.NewListPrice;
        }

        decimal cash;
        decimal card;
        var complimentary = request.IsComplimentary && preview.DifferenceKind == "additional";
        if (complimentary)
        {
            cash = 0m;
            card = 0m;
            effectiveDiscount = preview.NewListPrice;
        }
        else if (preview.DifferenceKind == "additional")
        {
            cash = Math.Max(0m, request.AmountPaidCash);
            card = Math.Max(0m, request.AmountPaidCard);
            // Kredit endirim kimi, əlavə nağd/kart qalanı ödəyir — Resolve yoxlanıb
            PaymentSettlementRules.Resolve(
                preview.NewListPrice,
                effectiveDiscount,
                cash,
                card,
                false);
        }
        else
        {
            // even / refund: kredit yeni paketi tam örtür
            cash = 0m;
            card = 0m;
            effectiveDiscount = preview.NewListPrice;
        }

        var newRecord = await CustomerBillingService.RecordPackageAsync(
            repository,
            athlete.Id,
            newPkg.Id,
            newPkg.Name,
            "Paket dəyişimi",
            preview.NewListPrice,
            complimentary ? preview.NewListPrice : effectiveDiscount,
            cash,
            card,
            complimentary,
            null,
            newSchedule.Id,
            request.CreatedByStaffId,
            cancellationToken);

        Guid? refundId = null;
        if (preview.DifferenceKind == "refund")
        {
            var refundCash = Math.Max(0m, request.AmountPaidCash);
            var refundCard = Math.Max(0m, request.AmountPaidCard);
            var refundTotal = refundCash + refundCard;
            var refundRecord = new CustomerPackageRecord
            {
                AthleteId = athlete.Id,
                ServicePackageId = newPkg.Id,
                PackageName = $"Qaytarma · {preview.OldPackageName ?? "köhnə paket"}",
                BillingTypeLabel = "Paket dəyişimi (qaytarma)",
                PriceDue = 0m,
                DiscountAmount = 0m,
                AmountPaidCash = -refundCash,
                AmountPaidCard = -refundCard,
                AmountPaid = -refundTotal,
                IsComplimentary = false,
                SubscriptionScheduleId = newSchedule.Id,
                CreatedByStaffId = request.CreatedByStaffId,
                CreatedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            refundRecord = await repository.AddCustomerPackageRecordAsync(refundRecord, cancellationToken);
            refundId = refundRecord.Id;
        }

        var msg = preview.DifferenceKind switch
        {
            "additional" => complimentary
                ? $"Paket dəyişildi ({newPkg.Name}). Əlavə ödəniş pulsuz qeyd olundu."
                : $"Paket dəyişildi ({newPkg.Name}). Əlavə ödəniş: {preview.AdditionalDue:0.##} AZN.",
            "refund" => $"Paket dəyişildi ({newPkg.Name}). Qaytarma: {preview.RefundDue:0.##} AZN.",
            _ => $"Paket dəyişildi ({newPkg.Name}). Ödəniş fərqi yoxdur."
        };

        return new ChangeCustomerPackageResult(newRecord.Id, refundId, msg);
    }

    private static MembershipType ResolveMembershipType(ServicePackage pkg)
    {
        if (pkg.Scope == PackageScope.Gym || pkg.BillingType == PackageBillingType.Gym)
        {
            return MembershipType.GymOnly;
        }

        if (pkg.Scope is PackageScope.Full or PackageScope.Vip
            || pkg.UnlimitedGym
            || pkg.BillingType is PackageBillingType.Monthly or PackageBillingType.Yearly or PackageBillingType.Unlimited or PackageBillingType.Vip)
        {
            return MembershipType.FullCombo;
        }

        return MembershipType.ArcheryOnly;
    }
}
