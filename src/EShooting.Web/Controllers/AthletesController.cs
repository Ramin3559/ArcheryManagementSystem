using EShooting.Web.Contracts.Athletes;
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
            var athletes = await repository.GetAthletesAsync(cancellationToken);
            var existing = athletes.FirstOrDefault(a =>
                (!string.IsNullOrWhiteSpace(phoneQ) && NormalizeDigits(a.PhoneNumber) == phoneQ)
                || (!string.IsNullOrWhiteSpace(emailQ) && NormalizeText(a.Email) == emailQ)
                || (!string.IsNullOrWhiteSpace(idQ) && NormalizeText(a.IdCardNumber) == idQ));

            if (existing is not null)
            {
                return Conflict(new
                {
                    error = "Bu şəxs artıq sistemdə qeydiyyatdadır.",
                    existingId = existing.Id
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
                    request.MembershipType),
                cancellationToken);

            return Ok(new { id });
        }
        catch (DbUpdateException)
        {
            // Unique index violation or other DB constraint; return a user-friendly message.
            return Conflict(new { error = "Bu məlumatlarla artıq qeydiyyat mövcuddur." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var result = athletes
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
                x.IsFullPackage
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
                IsFullPackage = existing.IsFullPackage
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

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string? phone, [FromQuery] string? email, [FromQuery] string? idCardNumber, CancellationToken cancellationToken)
    {
        var phoneQ = NormalizeDigits(phone);
        var emailQ = NormalizeText(email);
        var idQ = NormalizeText(idCardNumber);

        var hasQuery =
            (!string.IsNullOrWhiteSpace(phoneQ) && phoneQ.Length >= 4)
            || (!string.IsNullOrWhiteSpace(emailQ) && emailQ.Length >= 4)
            || (!string.IsNullOrWhiteSpace(idQ) && idQ.Length >= 3);

        if (!hasQuery)
        {
            return BadRequest(new { message = "Lookup requires at least phone/email/idCardNumber (min lengths: phone>=4, email>=4, id>=3)." });
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var candidates = athletes
            .Where(a =>
            {
                var p = NormalizeDigits(a.PhoneNumber);
                var e = NormalizeText(a.Email);
                var id = NormalizeText(a.IdCardNumber);

                var phoneOk = string.IsNullOrWhiteSpace(phoneQ) || (!string.IsNullOrWhiteSpace(p) && p.Contains(phoneQ));
                var emailOk = string.IsNullOrWhiteSpace(emailQ) || (!string.IsNullOrWhiteSpace(e) && e.Contains(emailQ));
                var idOk = string.IsNullOrWhiteSpace(idQ) || (!string.IsNullOrWhiteSpace(id) && id.Contains(idQ));
                return phoneOk && emailOk && idOk;
            })
            .ToList();

        if (candidates.Count == 0)
        {
            return NotFound();
        }

        var best = candidates
            .OrderByDescending(a => ScoreCandidate(a, phoneQ, emailQ, idQ))
            .First();

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
            best.IsFullPackage
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
