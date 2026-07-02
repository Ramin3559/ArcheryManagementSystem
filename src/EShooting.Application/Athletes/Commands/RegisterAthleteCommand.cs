using EShooting.Domain.Enums;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record RegisterAthleteCommand(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Email,
    string IdCardNumber,
    string ClubCardNumber,
    CustomerCategory Category,
    bool IsSubscriber,
    MembershipType MembershipType,
    bool IsVip = false,
    Guid? RegisteredByStaffId = null) : IRequest<Guid>;

public sealed class RegisterAthleteCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<RegisterAthleteCommand, Guid>
{
    public async Task<Guid> Handle(RegisterAthleteCommand request, CancellationToken cancellationToken)
    {
        var first = AthleteRegistrationRules.NormalizeText(request.FirstName);
        var last = AthleteRegistrationRules.NormalizeText(request.LastName);
        var phone = AthleteRegistrationRules.NormalizeDigits(request.PhoneNumber);
        var email = AthleteRegistrationRules.NormalizeEmail(request.Email);
        var idCard = AthleteRegistrationRules.NormalizeText(request.IdCardNumber);
        var clubCard = AthleteRegistrationRules.NormalizeText(request.ClubCardNumber);

        if (!AthleteRegistrationRules.HasRequiredContactFields(first, last, phone, email, idCard, clubCard))
        {
            throw new InvalidOperationException(AthleteRegistrationRules.RequiredFieldsMessage);
        }

        var athlete = new Athlete
        {
            FirstName = first,
            LastName = last,
            PhoneNumber = phone,
            Email = email,
            IdCardNumber = idCard,
            ClubCardNumber = clubCard,
            Category = request.Category,
            FullName = $"{first} {last}".Trim(),
            IsSubscriber = request.IsSubscriber,
            MembershipType = request.MembershipType,
            IsVip = request.IsVip,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RegisteredByStaffId = request.RegisteredByStaffId
        };

        var created = await repository.AddAthleteAsync(athlete, cancellationToken);
        return created.Id;
    }
}
