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
    ClubCardType? ClubCardType,
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
        var email = AthleteRegistrationRules.NormalizeOptionalEmail(request.Email);
        var idCard = AthleteRegistrationRules.NormalizeText(request.IdCardNumber);
        var clubCard = AthleteRegistrationRules.NormalizeOptionalText(request.ClubCardNumber);
        ClubCardType? clubCardType = string.IsNullOrWhiteSpace(clubCard) ? null : request.ClubCardType;

        if (!AthleteRegistrationRules.HasRequiredContactFields(first, last, phone, idCard))
        {
            throw new InvalidOperationException(AthleteRegistrationRules.RequiredFieldsMessage);
        }

        if (!string.IsNullOrWhiteSpace(clubCard))
        {
            if (clubCardType is not ClubCardType type)
            {
                throw new InvalidOperationException("Kart növü seçin.");
            }

            await ClubCardAssignmentService.EnsureCardAvailableAsync(
                repository,
                type,
                clubCard,
                excludeAthleteId: null,
                cancellationToken);
        }

        var athlete = new Athlete
        {
            FirstName = first,
            LastName = last,
            PhoneNumber = phone,
            Email = email,
            IdCardNumber = idCard,
            ClubCardNumber = clubCard,
            ClubCardType = clubCardType,
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

        if (!string.IsNullOrWhiteSpace(clubCard) && clubCardType is ClubCardType issuedType)
        {
            await repository.AddClubCardAssignmentAsync(new ClubCardAssignment
            {
                CardNumber = clubCard,
                CardType = issuedType,
                AthleteId = created.Id,
                IssuedAtUtc = DateTime.UtcNow,
                IssuedByStaffId = request.RegisteredByStaffId
            }, cancellationToken);
        }

        return created.Id;
    }
}
