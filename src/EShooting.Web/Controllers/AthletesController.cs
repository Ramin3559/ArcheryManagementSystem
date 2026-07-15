using EShooting.Web.Contracts.Athletes;
using EShooting.Application.Common;
using EShooting.Application.Athletes;
using EShooting.Application.Athletes.Commands;
using EShooting.Application.Athletes.Queries;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Customers;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("athletes")]
public sealed class AthletesController(IMediator mediator, ITrainingCenterRepository repository) : ControllerBase
{
    /// <summary>
    /// Yeni müştərini qeydiyyatdan keçirir və yaradılan identifikatoru qaytarır.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterAthleteRequest request, CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanRegisterCustomers) is { } denied)
        {
            return denied;
        }

        // Prevent duplicate inserts (phone/email/idCard/clubCard). If exists, return 409 with existing id.
        var phoneQ = NormalizeDigits(request.PhoneNumber);
        var emailQ = NormalizeText(request.Email);
        var idQ = NormalizeText(request.IdCardNumber);
        var clubCardQ = NormalizeText(request.ClubCardNumber);
        if (!string.IsNullOrWhiteSpace(phoneQ) || !string.IsNullOrWhiteSpace(emailQ) || !string.IsNullOrWhiteSpace(idQ) || !string.IsNullOrWhiteSpace(clubCardQ))
        {
            var existing = await repository.FindAthleteForLookupAsync(phoneQ, emailQ, idQ, cancellationToken, includeInactive: true);

            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    return Conflict(new
                    {
                        error = "Bu şəxs əvvəl bazada qeydiyyatda idi, indi deaktiv edilib.",
                        isInactive = true,
                        existingId = existing.Id,
                        existing = MapExistingAthlete(existing)
                    });
                }

                return Conflict(new
                {
                    error = "Bu şəxs artıq sistemdə qeydiyyatdadır.",
                    existingId = existing.Id,
                    existing = MapExistingAthlete(existing)
                });
            }
        }

        try
        {
            var id = await mediator.Send(
                new RegisterAthleteCommand(
                    request.FirstName,
                    request.LastName,
                    request.PhoneNumber,
                    request.Email,
                    request.IdCardNumber,
                    request.ClubCardNumber,
                    request.Category,
                    request.IsSubscriber,
                    request.MembershipType,
                    request.IsVip,
                    User.GetStaffMemberId()),
                cancellationToken);

            return Ok(new { id });
        }
        catch (ClubCardHeldException ex)
        {
            return Conflict(ClubCardHeldPayload(ex));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            if (!string.IsNullOrWhiteSpace(clubCardQ))
            {
                var cardHolder = await repository.FindAthleteByClubCardNumberAsync(clubCardQ, null, cancellationToken);
                if (cardHolder is not null)
                {
                    return Conflict(ClubCardHeldPayload(clubCardQ, cardHolder));
                }
            }

            var existingAfterError = await repository.FindAthleteForLookupAsync(phoneQ, emailQ, idQ, cancellationToken, includeInactive: true);
            if (existingAfterError is not null)
            {
                return Conflict(new
                {
                    error = existingAfterError.IsActive
                        ? "Bu şəxs artıq sistemdə qeydiyyatdadır."
                        : "Bu şəxs əvvəl bazada qeydiyyatda idi, indi deaktiv edilib.",
                    isInactive = !existingAfterError.IsActive,
                    existingId = existingAfterError.Id,
                    existing = MapExistingAthlete(existingAfterError)
                });
            }

            return Conflict(new { error = "Bu məlumatlarla artıq qeydiyyat mövcuddur." });
        }
    }

    /// <summary>
    /// Bütün axtarıla bilən müştəriləri qaytarır.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var result = athletes
            .Where(AthleteSearchRules.IsSearchable)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.FirstName,
                x.LastName,
                x.PhoneNumber,
                x.Email,
                x.IdCardNumber,
                x.ClubCardNumber,
                x.IsSubscriber,
                x.MembershipType,
                x.Category,
                x.IsFullPackage,
                x.IsVip
            });

        return Ok(result);
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] string? vip,
        [FromQuery] string? packageType,
        [FromQuery] string? customerType,
        [FromQuery] string? sessionRental,
        [FromQuery] string? equipment,
        [FromQuery] string? active,
        [FromQuery] int? category,
        [FromQuery] DateTime? registeredFrom,
        [FromQuery] DateTime? registeredTo,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        ApplyDefaultRegistrationDates(ref registeredFrom, ref registeredTo);
        CustomerCategory? cat = category is >= 0 and <= 2 ? (CustomerCategory)category.Value : null;
        var rentalFilter = string.IsNullOrWhiteSpace(sessionRental) ? equipment : sessionRental;
        var activeKey = (active ?? "").Trim();
        if (string.IsNullOrEmpty(activeKey)
            || activeKey.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            includeInactive = true;
        }

        var result = await mediator.Send(
            new GetCustomersListQuery(
                search,
                vip,
                packageType,
                customerType,
                rentalFilter,
                active,
                cat,
                registeredFrom,
                registeredTo,
                includeInactive),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("list/export.xlsx")]
    public async Task<IActionResult> ExportList(
        [FromQuery] string? search,
        [FromQuery] string? vip,
        [FromQuery] string? packageType,
        [FromQuery] string? customerType,
        [FromQuery] string? sessionRental,
        [FromQuery] string? equipment,
        [FromQuery] string? active,
        [FromQuery] int? category,
        [FromQuery] DateTime? registeredFrom,
        [FromQuery] DateTime? registeredTo,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        ApplyDefaultRegistrationDates(ref registeredFrom, ref registeredTo);
        CustomerCategory? cat = category is >= 0 and <= 2 ? (CustomerCategory)category.Value : null;
        var rentalFilter = string.IsNullOrWhiteSpace(sessionRental) ? equipment : sessionRental;
        var activeKey = (active ?? "").Trim();
        if (string.IsNullOrEmpty(activeKey)
            || activeKey.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            includeInactive = true;
        }

        var result = await mediator.Send(
            new GetCustomersListQuery(search, vip, packageType, customerType, rentalFilter, active, cat, registeredFrom, registeredTo, includeInactive),
            cancellationToken);
        var bytes = Admin.AdminCustomersExcelExporter.Export(result.Items);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"musteriler-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpPost("{id:guid}/package-billing")]
    public async Task<IActionResult> RecordPackageBilling(
        [FromRoute] Guid id,
        [FromBody] RecordPackageBillingRequest request,
        CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanManageSubscriptions) is { } denied)
        {
            return denied;
        }

        var athlete = await repository.GetAthleteByIdAsync(id, cancellationToken);
        if (athlete is null || !AthleteSearchRules.IsSearchable(athlete))
        {
            return NotFound(new { error = "Müştəri tapılmadı." });
        }

        if (request.IsComplimentary)
        {
            if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanGrantComplimentarySession) is { } compDenied)
            {
                return compDenied;
            }

            if (request.AmountPaidCash > 0m || request.AmountPaidCard > 0m || request.DiscountAmount > 0m)
            {
                return BadRequest(new { error = "Ödənişsiz seçildikdə məbləğ və endirim daxil edilə bilməz." });
            }
        }
        else
        {
            try
            {
                PaymentSettlementRules.EnsureDiscountAllowed(
                    request.DiscountAmount,
                    User.HasReceptionPermission(ReceptionStaffClaims.CanApplyDiscount));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
        }

        var pkgId = request.ServicePackageId is Guid pid && pid != Guid.Empty ? pid : (Guid?)null;
        if (pkgId is null)
        {
            return BadRequest(new { error = "Paket seçilməyib." });
        }

        try
        {
            await CustomerBillingService.RecordPackageAsync(
                repository,
                id,
                pkgId,
                null,
                null,
                null,
                request.DiscountAmount,
                request.AmountPaidCash,
                request.AmountPaidCard,
                request.IsComplimentary,
                request.SessionId,
                request.SubscriptionScheduleId,
                User.GetStaffMemberId(),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { recorded = true });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!User.HasAnyReceptionPermission(
                ReceptionStaffClaims.CanViewCustomerDetails,
                ReceptionStaffClaims.CanRegisterCustomers))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu əməliyyat üçün icazəniz yoxdur." });
        }

        var athlete = await repository.GetAthleteByIdAsync(id, cancellationToken);
        if (athlete is null || !AthleteSearchRules.IsSearchable(athlete))
        {
            return NotFound();
        }

        return Ok(MapExistingAthlete(athlete));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateAthleteRequest request, CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanEditCustomerDetails) is { } denied)
        {
            return denied;
        }

        var first = (request.FirstName ?? "").Trim();
        var last = (request.LastName ?? "").Trim();
        var phone = NormalizeDigits(request.PhoneNumber);
        var email = AthleteRegistrationRules.NormalizeOptionalEmail(request.Email);
        var idCard = AthleteRegistrationRules.NormalizeText(request.IdCardNumber);
        var clubCard = AthleteRegistrationRules.NormalizeOptionalText(request.ClubCardNumber);
        if (!AthleteRegistrationRules.HasRequiredContactFields(first, last, phone, idCard))
        {
            return BadRequest(new { error = AthleteRegistrationRules.RequiredFieldsMessage });
        }

        try
        {
            var athletes = await repository.GetAthletesAsync(cancellationToken);
            var existing = athletes.FirstOrDefault(x => x.Id == id);
            if (existing is null)
            {
                return NotFound(new { error = "Müştəri tapılmadı." });
            }

            var previousCard = existing.ClubCardNumber;
            if (!string.IsNullOrWhiteSpace(clubCard)
                && !string.Equals(
                    AthleteRegistrationRules.NormalizeText(previousCard),
                    clubCard,
                    StringComparison.OrdinalIgnoreCase))
            {
                await ClubCardAssignmentService.EnsureCardAvailableAsync(
                    repository,
                    clubCard,
                    id,
                    cancellationToken);
            }

            await repository.UpdateAthleteAsync(new Domain.Entities.Athlete
            {
                Id = id,
                FirstName = first,
                LastName = last,
                FullName = $"{first} {last}".Trim(),
                PhoneNumber = phone,
                Email = email,
                IdCardNumber = string.IsNullOrWhiteSpace(idCard) ? null : idCard,
                ClubCardNumber = clubCard,
                Category = request.Category,
                IsSubscriber = request.IsSubscriber,
                MembershipType = request.MembershipType,
                IsFullPackage = existing.IsFullPackage,
                IsVip = request.IsVip,
                IsActive = existing.IsActive,
                RegisteredByStaffId = existing.RegisteredByStaffId,
                CreatedAtUtc = existing.CreatedAtUtc
            }, cancellationToken);

            await ClubCardAssignmentService.SyncAthleteCardAsync(
                repository,
                id,
                previousCard,
                clubCard,
                User.GetStaffMemberId(),
                cancellationToken);

            return Ok(new { message = "Məlumatlar uğurla yeniləndi." });
        }
        catch (ClubCardHeldException ex)
        {
            return Conflict(ClubCardHeldPayload(ex));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            if (!string.IsNullOrWhiteSpace(clubCard))
            {
                var cardHolder = await repository.FindAthleteByClubCardNumberAsync(clubCard, id, cancellationToken);
                if (cardHolder is not null)
                {
                    return Conflict(ClubCardHeldPayload(clubCard, cardHolder));
                }
            }

            return Conflict(new { error = "Bu telefon/email/FİN/kart nömrəsi artıq başqa bir şəxsə aiddir." });
        }
    }

    [HttpPost("{id:guid}/return-club-card")]
    public async Task<IActionResult> ReturnClubCard([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!User.HasAnyReceptionPermission(
                ReceptionStaffClaims.CanEditCustomerDetails,
                ReceptionStaffClaims.CanRegisterCustomers))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu əməliyyat üçün icazəniz yoxdur." });
        }

        try
        {
            await mediator.Send(new ReturnClubCardCommand(id, User.GetStaffMemberId()), cancellationToken);
            return Ok(new { message = "Kart qaytarıldı — nömrə azad edildi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("club-card-holder")]
    public async Task<IActionResult> GetClubCardHolder([FromQuery] string? cardNumber, CancellationToken cancellationToken)
    {
        var card = AthleteRegistrationRules.NormalizeText(cardNumber);
        if (string.IsNullOrWhiteSpace(card) || card.Length < 1)
        {
            return BadRequest(new { error = "Kart nömrəsi daxil edin." });
        }

        var holder = await repository.FindAthleteByClubCardNumberAsync(card, null, cancellationToken);
        if (holder is null)
        {
            return Ok(new { held = false });
        }

        return Ok(new
        {
            held = true,
            error = ClubCardAssignmentService.FormatHeldByMessage(card, holder),
            holder = MapExistingAthlete(holder)
        });
    }

    /// <summary>
    /// Yazdıqca avtomatik tamamlama üçün ad/soyad/telefon/email/FİN üzrə sürətli axtarış.
    /// Verilmiş `q` ən azı 2 simvol olduqda işə düşür.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit, CancellationToken cancellationToken)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length < 2)
        {
            return Ok(Array.Empty<object>());
        }

        var take = limit <= 0 ? 10 : Math.Min(limit, 25);

        var qLower = query.ToLowerInvariant();
        var qDigits = NormalizeDigits(query);

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var matches = athletes
            .Where(AthleteSearchRules.IsSearchable)
            .Select(a =>
            {
                var firstLower = (a.FirstName ?? string.Empty).ToLowerInvariant();
                var lastLower = (a.LastName ?? string.Empty).ToLowerInvariant();
                var fullLower = (a.FullName ?? string.Empty).ToLowerInvariant();
                var emailLower = (a.Email ?? string.Empty).ToLowerInvariant();
                var idLower = (a.IdCardNumber ?? string.Empty).ToLowerInvariant();
                var phoneDigits = NormalizeDigits(a.PhoneNumber);

                var score = 0;
                if (!string.IsNullOrEmpty(qDigits) && phoneDigits.Length > 0)
                {
                    if (phoneDigits == qDigits) score += 200;
                    else if (phoneDigits.StartsWith(qDigits, StringComparison.Ordinal)) score += 120;
                    else if (phoneDigits.Contains(qDigits, StringComparison.Ordinal)) score += 60;
                }

                if (firstLower.StartsWith(qLower, StringComparison.Ordinal)) score += 90;
                else if (firstLower.Contains(qLower, StringComparison.Ordinal)) score += 30;

                if (lastLower.StartsWith(qLower, StringComparison.Ordinal)) score += 90;
                else if (lastLower.Contains(qLower, StringComparison.Ordinal)) score += 30;

                if (fullLower.StartsWith(qLower, StringComparison.Ordinal)) score += 70;
                else if (fullLower.Contains(qLower, StringComparison.Ordinal)) score += 25;

                if (!string.IsNullOrEmpty(emailLower))
                {
                    if (emailLower.StartsWith(qLower, StringComparison.Ordinal)) score += 80;
                    else if (emailLower.Contains(qLower, StringComparison.Ordinal)) score += 25;
                }

                if (!string.IsNullOrEmpty(idLower))
                {
                    if (idLower == qLower) score += 150;
                    else if (idLower.StartsWith(qLower, StringComparison.Ordinal)) score += 80;
                    else if (idLower.Contains(qLower, StringComparison.Ordinal)) score += 30;
                }

                return new { Athlete = a, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Athlete.FullName)
            .Take(take)
            .Select(x => new
            {
                x.Athlete.Id,
                x.Athlete.FullName,
                x.Athlete.FirstName,
                x.Athlete.LastName,
                x.Athlete.PhoneNumber,
                x.Athlete.Email,
                x.Athlete.IdCardNumber,
                x.Athlete.ClubCardNumber,
                x.Athlete.Category,
                x.Athlete.MembershipType,
                x.Athlete.IsSubscriber,
                x.Athlete.IsFullPackage,
                x.Athlete.IsVip
            })
            .ToList();

        return Ok(matches);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string? phone, [FromQuery] string? email, [FromQuery] string? idCardNumber, CancellationToken cancellationToken)
    {
        var phoneQ = NormalizeDigits(phone);
        var emailQ = NormalizeText(email);
        var idQ = NormalizeText(idCardNumber);

        var hasQuery =
            (!string.IsNullOrWhiteSpace(phoneQ) && phoneQ.Length >= 3)
            || (!string.IsNullOrWhiteSpace(emailQ) && emailQ.Length >= 4)
            || (!string.IsNullOrWhiteSpace(idQ) && idQ.Length >= 3);

        if (!hasQuery)
        {
            return BadRequest(new { message = "Lookup requires at least phone/email/idCardNumber (min lengths: phone>=4, email>=4, id>=3)." });
        }

        var best = await repository.FindAthleteForLookupAsync(phoneQ, emailQ, idQ, cancellationToken);
        if (best is null || !AthleteSearchRules.IsSearchable(best))
        {
            return NotFound();
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var activeVip = WalkInSubscriptionRules.GetActiveVipSchedule(schedules, best.Id, DateTime.Now);

        return Ok(new
        {
            best.Id,
            best.FullName,
            best.FirstName,
            best.LastName,
            best.PhoneNumber,
            best.Email,
            best.IdCardNumber,
            best.ClubCardNumber,
            best.Category,
            best.MembershipType,
            best.IsSubscriber,
            best.IsFullPackage,
            best.IsVip,
            hasActiveWalkIn = activeVip is not null,
            walkInExpiresLocal = activeVip is null ? null : DateDisplayFormats.FormatDate(activeVip.ActiveToDateLocal),
            walkInSessionDurationMinutes = 0
        });
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(
                new SetAthleteActiveCommand(
                    id,
                    IsActive: false,
                    DeletedByStaffId: User.GetStaffMemberId()),
                cancellationToken);
            return Ok(new { message = "Müştəri silindi." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new SetAthleteActiveCommand(id, IsActive: true), cancellationToken);
            return Ok(new { message = "Müştəri bərpa edildi." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deaktiv müştərinin telefon/email/FİN-ini azad edir — eyni nömrə başqa şəxsə keçəndə yeni qeydiyyat üçün.
    /// </summary>
    [HttpPost("{id:guid}/release-identifiers")]
    public async Task<IActionResult> ReleaseIdentifiers(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new ReleaseInactiveAthleteIdentifiersCommand(id), cancellationToken);
            return Ok(new { message = "Köhnə qeydiyyatın telefonu azad edildi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object MapExistingAthlete(Domain.Entities.Athlete existing) => new
    {
        existing.Id,
        existing.FullName,
        existing.FirstName,
        existing.LastName,
        existing.PhoneNumber,
        existing.Email,
        existing.IdCardNumber,
        existing.ClubCardNumber,
        existing.Category,
        existing.MembershipType,
        existing.IsSubscriber,
        existing.IsFullPackage,
        existing.IsVip,
        existing.IsActive
    };

    private static object ClubCardHeldPayload(ClubCardHeldException ex) => ClubCardHeldPayload(ex.CardNumber, ex.Holder);

    private static object ClubCardHeldPayload(string cardNumber, Domain.Entities.Athlete holder) => new
    {
        error = ClubCardAssignmentService.FormatHeldByMessage(cardNumber, holder),
        clubCardHeld = true,
        clubCardNumber = cardNumber,
        holder = MapExistingAthlete(holder)
    };

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Regex.Replace(value, "\\D+", "");
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static int ScoreCandidate(Domain.Entities.Athlete a, string phoneQ, string emailQ, string idQ)
    {
        var score = 0;
        var phone = NormalizeDigits(a.PhoneNumber);
        var email = NormalizeText(a.Email);
        var id = NormalizeText(a.IdCardNumber);

        if (!string.IsNullOrWhiteSpace(phoneQ) && phone == phoneQ) score += 100;
        if (!string.IsNullOrWhiteSpace(idQ) && id == idQ) score += 90;
        if (!string.IsNullOrWhiteSpace(emailQ) && email == emailQ) score += 80;

        if (!string.IsNullOrWhiteSpace(phoneQ) && !string.IsNullOrWhiteSpace(phone) && phone.Contains(phoneQ)) score += Math.Min(30, phoneQ.Length);
        if (!string.IsNullOrWhiteSpace(idQ) && !string.IsNullOrWhiteSpace(id) && id.Contains(idQ)) score += Math.Min(20, idQ.Length);
        if (!string.IsNullOrWhiteSpace(emailQ) && !string.IsNullOrWhiteSpace(email) && email.Contains(emailQ)) score += Math.Min(25, emailQ.Length);

        return score;
    }

    private void ApplyDefaultRegistrationDates(ref DateTime? registeredFrom, ref DateTime? registeredTo)
    {
        if (!Request.Query.ContainsKey("registeredFrom") && !Request.Query.ContainsKey("registeredTo"))
        {
            var today = AzerbaijanTime.TodayLocal;
            registeredFrom = today;
            registeredTo = today;
        }
    }
}
