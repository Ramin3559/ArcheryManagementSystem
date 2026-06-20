using EShooting.Application.Common.Interfaces;
using EShooting.Application.StaffMembers;
using EShooting.Domain.Entities;
using MediatR;
using System.Text.RegularExpressions;

namespace EShooting.Application.StaffMembers.Commands;

public sealed record UpsertStaffMemberCommand(
    Guid? Id,
    string FirstName,
    string LastName,
    Guid StaffPositionId,
    Guid AccessProfileId,
    string? PhoneNumber,
    string? Pin,
    bool IsActive) : IRequest<Guid>;

public sealed class UpsertStaffMemberCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpsertStaffMemberCommand, Guid>
{
    private static readonly Regex PinPattern = new(@"^\d{4,6}$", RegexOptions.Compiled);

    public async Task<Guid> Handle(UpsertStaffMemberCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var phone = request.PhoneNumber!.Trim();

        var position = await repository.GetStaffPositionByIdAsync(request.StaffPositionId, cancellationToken)
            ?? throw new InvalidOperationException("Vəzifə tapılmadı.");
        if (!position.IsActive)
        {
            throw new InvalidOperationException("Seçilmiş vəzifə deaktivdir.");
        }

        var profile = await repository.GetAccessProfileByIdAsync(request.AccessProfileId, cancellationToken)
            ?? throw new InvalidOperationException("İcazə profili tapılmadı.");
        if (!profile.IsActive)
        {
            throw new InvalidOperationException("Seçilmiş icazə profili deaktivdir.");
        }

        if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            var pinInUse = await repository.IsStaffPinInUseAsync(request.Pin, request.Id, cancellationToken);
            if (pinInUse)
            {
                throw new InvalidOperationException("Bu giriş kodu artıq başqa işçidə istifadə olunur.");
            }
        }

        var phoneInUse = await repository.IsStaffPhoneInUseAsync(phone, request.Id, cancellationToken);
        if (phoneInUse)
        {
            throw new InvalidOperationException("Bu telefon nömrəsi artıq başqa işçidə istifadə olunur.");
        }

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var pin = request.Pin!.Trim();
            var created = await repository.AddStaffMemberAsync(new StaffMember
            {
                FirstName = firstName,
                LastName = lastName,
                StaffPositionId = request.StaffPositionId,
                AccessProfileId = request.AccessProfileId,
                PhoneNumber = phone,
                PinHash = StaffPinHasher.Hash(pin),
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            return created.Id;
        }

        var existing = await repository.GetStaffMemberByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("İşçi tapılmadı.");

        existing.FirstName = firstName;
        existing.LastName = lastName;
        existing.StaffPositionId = request.StaffPositionId;
        existing.AccessProfileId = request.AccessProfileId;
        existing.PhoneNumber = phone;
        existing.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            existing.PinHash = StaffPinHasher.Hash(request.Pin.Trim());
        }

        existing.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateStaffMemberAsync(existing, cancellationToken);
        return existing.Id;
    }

    private static void Validate(UpsertStaffMemberCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            throw new InvalidOperationException("Ad mütləqdir.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new InvalidOperationException("Soyad mütləqdir.");
        }

        if (request.StaffPositionId == Guid.Empty)
        {
            throw new InvalidOperationException("Vəzifə seçilməlidir.");
        }

        if (request.AccessProfileId == Guid.Empty)
        {
            throw new InvalidOperationException("İcazə profili seçilməlidir.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new InvalidOperationException("Telefon mütləqdir.");
        }

        var isCreate = request.Id is null || request.Id == Guid.Empty;
        if (isCreate && string.IsNullOrWhiteSpace(request.Pin))
        {
            throw new InvalidOperationException("Yeni işçi üçün giriş kodu (PIN) mütləqdir.");
        }

        if (!string.IsNullOrWhiteSpace(request.Pin) && !PinPattern.IsMatch(request.Pin.Trim()))
        {
            throw new InvalidOperationException("Giriş kodu 4–6 rəqəmdən ibarət olmalıdır.");
        }
    }
}
