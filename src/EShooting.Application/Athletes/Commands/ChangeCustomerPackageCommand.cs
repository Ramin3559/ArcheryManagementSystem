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
    string DifferenceKind,
    bool IsFixedWeekly,
    bool IsFlexibleMonthly,
    string? DefaultWeeklyDaysCsv,
    int? WeeklyDaysCount,
    int? VisitQuota,
    int SessionDurationMinutes,
    int? ValidityDays,
    /// <summary>true = mövcud paket bitib, yeni ödənişlə yaratmaq lazımdır.</summary>
    bool RequiresNewPayment,
    string LifecycleHint);

public sealed record ChangeCustomerPackageResult(
    Guid? NewPackageRecordId,
    Guid? RefundRecordId,
    string Message);

public sealed record ChangeCustomerPackageCommand(
    Guid AthleteId,
    Guid NewServicePackageId,
    DateTime PeriodStartLocal,
    DateTime PeriodEndLocal,
    int? PeriodMonths,
    decimal DiscountAmount,
    decimal AmountPaidCash,
    decimal AmountPaidCard,
    bool IsComplimentary,
    bool ConfirmDifference,
    bool SkipPayment,
    IReadOnlyList<int>? WeeklyDaysOfWeek,
    TimeSpan? WeeklyStartTimeLocal,
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
        CancellationToken cancellationToken,
        bool justRenew = false)
    {
        var athlete = await repository.GetAthleteByIdAsync(athleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        var newPkg = await repository.GetServicePackageByIdAsync(newServicePackageId, cancellationToken)
            ?? throw new InvalidOperationException("Yeni paket tapılmadı.");
        if (!newPkg.IsActive || newPkg.IsDeleted)
        {
            throw new InvalidOperationException("Seçilmiş paket aktiv deyil.");
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var packageEnded = CustomerPackageLifecycle.IsCurrentPackageEnded(
            athleteId,
            schedules,
            sessions,
            AzerbaijanTime.TodayLocal);
        // «Sadəcə yenilə» — bitib/bitməyib baxılmır, ödənişsiz plan yazılır.
        var requiresNewPayment = !justRenew && packageEnded;

        var oldRecord = PickMeaningfulActivePackageRecord(
            await repository.GetCustomerPackageRecordsAsync(cancellationToken),
            athleteId);

        var oldPaid = oldRecord is null ? 0m : Math.Max(0m, oldRecord.AmountPaid);
        var newList = Math.Max(0m, newPkg.Price);
        var userDiscount = Math.Clamp(Math.Max(0m, discountAmount), 0m, newList);
        var newPayable = Math.Max(0m, newList - userDiscount);
        var appliedCredit = requiresNewPayment ? 0m : Math.Min(oldPaid, newPayable);
        var additionalDue = requiresNewPayment
            ? newPayable
            : Math.Max(0m, newPayable - oldPaid);
        var refundDue = requiresNewPayment
            ? 0m
            : Math.Max(0m, oldPaid - newPayable);

        var kind = !requiresNewPayment
            ? "planOnly"
            : additionalDue > PaymentSettlementRules.Tolerance
                ? "additional"
                : refundDue > PaymentSettlementRules.Tolerance
                    ? "refund"
                    : "even";

        var hint = justRenew
            ? "Sadəcə yenilə — seçilmiş paket və müddət ödənişsiz yazılacaq. Ödəniş tarixçəsi dəyişməyəcək."
            : requiresNewPayment
                ? "Mövcud paket bitmişdir. Yeni paket üçün ödəniş tələb olunur."
                : "Aktiv paket — yalnız plan yenilənəcək (gün/saat/tarix). Ödəniş qeydi dəyişməyəcək.";

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
            kind,
            IsFixedWeeklyPackage(newPkg),
            FlexibleMonthlyRules.IsFlexibleMonthlyPackage(newPkg),
            newPkg.WeeklyDaysCsv,
            newPkg.WeeklyDaysCount is >= 1 and <= 7 ? newPkg.WeeklyDaysCount : null,
            FlexibleMonthlyRules.IsFlexibleMonthlyPackage(newPkg)
                ? FlexibleMonthlyRules.MonthlyQuota(newPkg)
                : null,
            Math.Max(0, newPkg.SessionDurationMinutes),
            newPkg.ValidityDays,
            requiresNewPayment,
            hint);
    }

    public async Task<ChangeCustomerPackageResult> Handle(
        ChangeCustomerPackageCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmDifference)
        {
            throw new InvalidOperationException("Paket dəyişimi üçün təsdiq lazımdır.");
        }

        var preview = await BuildPreviewAsync(
            repository,
            request.AthleteId,
            request.NewServicePackageId,
            request.DiscountAmount,
            cancellationToken,
            justRenew: request.SkipPayment);

        // Aktiv paket və ya «Sadəcə yenilə» (SkipPayment): yalnız plan, ödəniş yox.
        var planOnly = !preview.RequiresNewPayment || request.SkipPayment;
        if (!planOnly)
        {
            PaymentSettlementRules.EnsureDiscountAllowed(request.DiscountAmount, request.CanApplyDiscount);
            ValidatePaymentAgainstPreview(request, preview);
        }

        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");
        var newPkg = await repository.GetServicePackageByIdAsync(request.NewServicePackageId, cancellationToken)
            ?? throw new InvalidOperationException("Yeni paket tapılmadı.");

        var periodStart = request.PeriodStartLocal.Date;
        var fixedWeekly = IsFixedWeeklyPackage(newPkg);
        var flexibleMonthly = FlexibleMonthlyRules.IsFlexibleMonthlyPackage(newPkg);
        var weeklyDays = ResolveWeeklyDays(request.WeeklyDaysOfWeek, newPkg);
        if (fixedWeekly && weeklyDays.Count == 0)
        {
            throw new InvalidOperationException("Aylıq sabit plan üçün həftə günlərini seçin.");
        }

        if (fixedWeekly && newPkg.WeeklyDaysCount is >= 1 and <= 7
            && weeklyDays.Count != newPkg.WeeklyDaysCount.Value)
        {
            throw new InvalidOperationException(
                $"Bu paket həftədə {newPkg.WeeklyDaysCount} gün üçündür — {newPkg.WeeklyDaysCount} gün seçin.");
        }

        DateTime periodEnd;
        if (request.PeriodMonths is int monthsRaw && monthsRaw > 0)
        {
            var months = Math.Clamp(monthsRaw, 1, 12);
            // Limitsiz / Aylıq sərbəst: təqvim +N ay.
            // Sabit həftəlik: N-ci plan günü (qalıq gediş üçün +1 ay yalnız icazədir, tarixə yazılmır).
            periodEnd = fixedWeekly
                ? WeeklyVisitPeriodRules.ComputeEndDate(periodStart, weeklyDays, months)
                : periodStart.AddMonths(months);
        }
        else
        {
            periodEnd = request.PeriodEndLocal.Date;
        }

        if (periodEnd < periodStart)
        {
            throw new InvalidOperationException("Abunə bitmə tarixi başlanğıcdan əvvəl ola bilməz.");
        }

        var weeklyStart = request.WeeklyStartTimeLocal
            ?? TimeSpan.FromHours(18);
        if (fixedWeekly && (weeklyStart < TimeSpan.Zero || weeklyStart.TotalDays >= 1))
        {
            throw new InvalidOperationException("Həftəlik plan saatı HH:mm formatında olmalıdır.");
        }

        // 1) Köhnə abunələri bağla
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        foreach (var schedule in schedules.Where(s => s.AthleteId == athlete.Id && s.IsEnabled))
        {
            schedule.IsEnabled = false;
            await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
        }

        var markVip = ServicePackageRules.IsVipPackage(newPkg);
        athlete.IsSubscriber = true;
        athlete.IsVip = markVip;
        athlete.MembershipType = ResolveMembershipType(newPkg);
        athlete.IsFullPackage = !fixedWeekly;
        await repository.UpdateAthleteAsync(athlete, cancellationToken);

        // 2) Yeni plan(lar)
        Guid? primaryScheduleId = null;
        var newScheduleIds = new List<Guid>();
        if (fixedWeekly)
        {
            var duration = Math.Max(1, newPkg.SessionDurationMinutes);
            var isGym = newPkg.Scope == PackageScope.Gym || newPkg.BillingType == PackageBillingType.Gym;
            var preferred = athlete.Category == CustomerCategory.Amateur
                ? PreferredLaneType.Short
                : PreferredLaneType.Any;

            foreach (var day in weeklyDays.Distinct().OrderBy(d => d))
            {
                var created = await repository.AddSubscriptionScheduleAsync(
                    new SubscriptionSchedule
                    {
                        AthleteId = athlete.Id,
                        LaneNumber = isGym ? GymLaneRules.LaneNumber : 0,
                        DayOfWeek = day,
                        StartTimeLocal = weeklyStart,
                        DurationMinutes = duration,
                        ActiveFromDateLocal = periodStart,
                        ActiveToDateLocal = periodEnd,
                        IsEnabled = true,
                        PreferredLaneType = isGym ? PreferredLaneType.Any : preferred,
                        IsFullPackage = false
                    },
                    cancellationToken);
                newScheduleIds.Add(created.Id);
                primaryScheduleId ??= created.Id;
            }
        }
        else
        {
            var duration = Math.Max(0, newPkg.SessionDurationMinutes);
            if (flexibleMonthly)
            {
                duration = Math.Max(1, duration);
            }

            var visitQuota = flexibleMonthly
                ? FlexibleMonthlyRules.TotalVisitQuota(newPkg, periodStart, periodEnd)
                : (int?)null;
            var created = await repository.AddSubscriptionScheduleAsync(
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
                    IsFullPackage = true,
                    VisitQuota = visitQuota
                },
                cancellationToken);
            newScheduleIds.Add(created.Id);
            primaryScheduleId = created.Id;
        }

        await SubscriptionPlanReplace.CompleteOrphanScheduledAsync(
            repository,
            athlete.Id,
            newScheduleIds,
            DateTime.UtcNow,
            cancellationToken);

        if (planOnly)
        {
            var planMsg = request.SkipPayment
                ? $"Paket sadəcə yeniləndi ({newPkg.Name}). Ödəniş etmədən plan yazıldı."
                : $"Paket planı yeniləndi ({newPkg.Name}). Ödəniş və gəliş tarixçəsi saxlanıldı.";
            return new ChangeCustomerPackageResult(primaryScheduleId, null, planMsg);
        }

        // 3) Köhnə aktiv paket ödənişlərini deaktiv et
        var packageRecords = await repository.GetCustomerPackageRecordsAsync(cancellationToken);
        foreach (var old in packageRecords.Where(r => r.AthleteId == athlete.Id && r.IsActive))
        {
            old.IsActive = false;
            await repository.UpdateCustomerPackageRecordAsync(old, cancellationToken);
        }

        // 4) Yeni paket ödəniş qeydi (yalnız bitmiş paket yenilənməsi)
        var effectiveDiscount = preview.DiscountAmount;
        if (effectiveDiscount > preview.NewListPrice)
        {
            effectiveDiscount = preview.NewListPrice;
        }

        decimal cash;
        decimal card;
        var complimentary = request.IsComplimentary;
        if (complimentary)
        {
            cash = 0m;
            card = 0m;
            effectiveDiscount = preview.NewListPrice;
        }
        else
        {
            cash = Math.Max(0m, request.AmountPaidCash);
            card = Math.Max(0m, request.AmountPaidCard);
            PaymentSettlementRules.Resolve(
                preview.NewListPrice,
                effectiveDiscount,
                cash,
                card,
                false);
        }

        var newRecord = await CustomerBillingService.RecordPackageAsync(
            repository,
            athlete.Id,
            newPkg.Id,
            newPkg.Name,
            "Paket yenilənməsi",
            preview.NewListPrice,
            complimentary ? preview.NewListPrice : effectiveDiscount,
            cash,
            card,
            complimentary,
            null,
            primaryScheduleId,
            request.CreatedByStaffId,
            cancellationToken);

        var msg = complimentary
            ? $"Yeni paket qeydə alındı ({newPkg.Name}). Pulsuz."
            : $"Yeni paket qeydə alındı ({newPkg.Name}). Ödəniş: {preview.NewPayable:0.##} AZN.";

        return new ChangeCustomerPackageResult(newRecord.Id, null, msg);
    }

    private static void ValidatePaymentAgainstPreview(
        ChangeCustomerPackageCommand request,
        ChangeCustomerPackagePreview preview)
    {
        if (request.IsComplimentary)
        {
            if (!request.CanGrantComplimentary)
            {
                throw new InvalidOperationException("Pulsuz paket üçün icazəniz yoxdur.");
            }

            if (request.AmountPaidCash > PaymentSettlementRules.Tolerance
                || request.AmountPaidCard > PaymentSettlementRules.Tolerance)
            {
                throw new InvalidOperationException("Pulsuz seçildikdə ödəniş yazıla bilməz.");
            }

            return;
        }

        PaymentSettlementRules.Resolve(
            preview.NewPayable,
            0m,
            request.AmountPaidCash,
            request.AmountPaidCard,
            false);
    }

    private static CustomerPackageRecord? PickMeaningfulActivePackageRecord(
        IReadOnlyCollection<CustomerPackageRecord> records,
        Guid athleteId)
    {
        var active = records
            .Where(r => r.AthleteId == athleteId && r.IsActive)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();
        if (active.Count == 0)
        {
            return null;
        }

        return active.FirstOrDefault(r => Math.Abs(r.AmountPaid) > PaymentSettlementRules.Tolerance)
               ?? active[0];
    }

    private static bool IsFixedWeeklyPackage(ServicePackage pkg) =>
        ServicePackageRules.IsFixedWeeklyPackage(pkg);

    private static List<int> ResolveWeeklyDays(IReadOnlyList<int>? requested, ServicePackage pkg)
    {
        if (requested is { Count: > 0 })
        {
            return requested.Where(d => d is >= 0 and <= 6).Distinct().OrderBy(d => d).ToList();
        }

        if (string.IsNullOrWhiteSpace(pkg.WeeklyDaysCsv))
        {
            return [];
        }

        return pkg.WeeklyDaysCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : -1)
            .Where(d => d is >= 0 and <= 6)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
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
