using EShooting.Application.Athletes;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EShooting.Application.Athletes.Commands;

public sealed record QuickRegisterAthleteCommand(
    string FirstName,
    string LastName,
    string PhoneNumber,
    Guid? RegisteredByStaffId = null) : IRequest<QuickRegisterAthleteResult>;

public sealed record QuickRegisterAthleteResult(Guid AthleteId, bool IsNewCustomer);

public sealed class QuickRegisterAthleteCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<QuickRegisterAthleteCommand, QuickRegisterAthleteResult>
{
    public async Task<QuickRegisterAthleteResult> Handle(
        QuickRegisterAthleteCommand request,
        CancellationToken cancellationToken)
    {
        var first = AthleteRegistrationRules.NormalizeText(request.FirstName);
        var last = AthleteRegistrationRules.NormalizeText(request.LastName);
        var phone = AthleteRegistrationRules.NormalizeDigits(request.PhoneNumber);

        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(phone))
        {
            throw new InvalidOperationException("Ad, Soyad və Telefon mütləqdir.");
        }

        var existing = await repository.FindAthleteByExactPhoneAsync(phone, cancellationToken, includeInactive: true);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                throw new InvalidOperationException("Bu şəxs əvvəl bazada qeydiyyatda idi, indi deaktiv edilib.");
            }

            return new QuickRegisterAthleteResult(existing.Id, false);
        }

        var athlete = new Athlete
        {
            FirstName = first,
            LastName = last,
            FullName = $"{first} {last}".Trim(),
            PhoneNumber = phone,
            Email = null,
            IdCardNumber = null,
            ClubCardNumber = null,
            Category = CustomerCategory.Amateur,
            IsSubscriber = false,
            MembershipType = MembershipType.FullCombo,
            IsVip = false,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RegisteredByStaffId = request.RegisteredByStaffId
        };

        try
        {
            var created = await repository.AddAthleteAsync(athlete, cancellationToken);
            return new QuickRegisterAthleteResult(created.Id, true);
        }
        catch (DbUpdateException)
        {
            var raced = await repository.FindAthleteByExactPhoneAsync(phone, cancellationToken, includeInactive: true);
            if (raced is null)
            {
                throw new InvalidOperationException("Müştəri qeydiyyatı zamanı xəta baş verdi.");
            }

            if (!raced.IsActive)
            {
                throw new InvalidOperationException("Bu şəxs əvvəl bazada qeydiyyatda idi, indi deaktiv edilib.");
            }

            return new QuickRegisterAthleteResult(raced.Id, false);
        }
    }
}
