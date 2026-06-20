using EShooting.Domain.Enums;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record RegisterAthleteCommand(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    string? IdCardNumber,
    CustomerCategory Category,
    bool IsSubscriber,
    MembershipType MembershipType,
    bool IsVip = false) : IRequest<Guid>;

public sealed class RegisterAthleteCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<RegisterAthleteCommand, Guid>
{
    public async Task<Guid> Handle(RegisterAthleteCommand request, CancellationToken cancellationToken)
    {
        var first = request.FirstName.Trim();
        var last = request.LastName.Trim();
        var phone = NormalizeDigits(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(phone))
        {
            throw new InvalidOperationException("FirstName, LastName and PhoneNumber are required.");
        }

        var athlete = new Athlete
        {
            FirstName = first,
            LastName = last,
            PhoneNumber = phone,
            Email = NormalizeOptionalEmail(request.Email),
            IdCardNumber = NormalizeOptionalText(request.IdCardNumber),
            Category = request.Category,
            FullName = $"{first} {last}".Trim(),
            IsSubscriber = request.IsSubscriber,
            MembershipType = request.MembershipType,
            IsVip = request.IsVip
        };

        var created = await repository.AddAthleteAsync(athlete, cancellationToken);
        return created.Id;
    }

    private static string NormalizeDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string? NormalizeOptionalEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}
