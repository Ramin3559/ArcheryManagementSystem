using EShooting.Web.Contracts.Athletes;
using EShooting.Application.Athletes;
using EShooting.Application.Athletes.Commands;
using EShooting.Application.Common.Interfaces;
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
    /// Yeni idmancini qeydiyyatdan kecirir ve yaradilan identifikatoru qaytarir.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterAthleteRequest request, CancellationToken cancellationToken)
    {
        // Prevent duplicate inserts (phone/email/idCard). If exists, return 409 with existing id.
        var phoneQ = NormalizeDigits(request.PhoneNumber);
        var emailQ = NormalizeText(request.Email);
        var idQ = NormalizeText(request.IdCardNumber);
        if (!string.IsNullOrWhiteSpace(phoneQ) || !string.IsNullOrWhiteSpace(emailQ) || !string.IsNullOrWhiteSpace(idQ))
        {
            var existing = await repository.FindAthleteForLookupAsync(phoneQ, emailQ, idQ, cancellationToken);

            if (existing is not null)
            {
                return Conflict(new
                {
                    error = "Bu şəxs artıq sistemdə qeydiyyatdadır.",
                    existingId = existing.Id,
                    existing = new
                    {
                        existing.Id,
                        existing.FullName,
                        existing.FirstName,
                        existing.LastName,
                        existing.PhoneNumber,
                        existing.Email,
                        existing.IdCardNumber,
                        existing.Category,
                        existing.MembershipType,
                        existing.IsSubscriber,
                        existing.IsFullPackage,
                        existing.IsVip
                    }
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
                    request.Category,
                    request.IsSubscriber,
                    request.MembershipType,
                    request.IsVip),
                cancellationToken);

            return Ok(new { id });
        }
        catch (DbUpdateException)
        {
            var existingAfterError = await repository.FindAthleteForLookupAsync(phoneQ, emailQ, idQ, cancellationToken);
            if (existingAfterError is not null)
            {
                return Conflict(new
                {
                    error = "Bu şəxs artıq sistemdə qeydiyyatdadır.",
                    existingId = existingAfterError.Id,
                    existing = new
                    {
                        existingAfterError.Id,
                        existingAfterError.FullName,
                        existingAfterError.FirstName,
                        existingAfterError.LastName,
                        existingAfterError.PhoneNumber,
                        existingAfterError.Email,
                        existingAfterError.IdCardNumber,
                        existingAfterError.Category,
                        existingAfterError.MembershipType,
                        existingAfterError.IsSubscriber,
                        existingAfterError.IsFullPackage,
                        existingAfterError.IsVip
                    }
                });
            }

            return Conflict(new { error = "Bu məlumatlarla artıq qeydiyyat mövcuddur." });
        }
    }

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
                x.IsSubscriber,
                x.MembershipType,
                x.Category,
                x.IsFullPackage,
                x.IsVip
            });

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateAthleteRequest request, CancellationToken cancellationToken)
    {
        var first = (request.FirstName ?? "").Trim();
        var last = (request.LastName ?? "").Trim();
        var phone = NormalizeDigits(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { error = "Ad, Soyad və Telefon mütləqdir." });
        }

        try
        {
            var athletes = await repository.GetAthletesAsync(cancellationToken);
            var existing = athletes.FirstOrDefault(x => x.Id == id);
            if (existing is null)
            {
                return NotFound(new { error = "İdmançı tapılmadı." });
            }

            await repository.UpdateAthleteAsync(new Domain.Entities.Athlete
            {
                Id = id,
                FirstName = first,
                LastName = last,
                FullName = $"{first} {last}".Trim(),
                PhoneNumber = phone,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                IdCardNumber = string.IsNullOrWhiteSpace(request.IdCardNumber) ? null : request.IdCardNumber.Trim(),
                Category = request.Category,
                IsSubscriber = request.IsSubscriber,
                MembershipType = request.MembershipType,
                IsFullPackage = existing.IsFullPackage,
                IsVip = request.IsVip
            }, cancellationToken);

            return Ok(new { message = "Məlumatlar uğurla yeniləndi." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "Bu telefon/email/FİN artıq başqa bir şəxsə aiddir." });
        }
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

        return Ok(new
        {
            best.Id,
            best.FullName,
            best.FirstName,
            best.LastName,
            best.PhoneNumber,
            best.Email,
            best.IdCardNumber,
            best.Category,
            best.MembershipType,
            best.IsSubscriber,
            best.IsFullPackage,
            best.IsVip
        });
    }

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
}
