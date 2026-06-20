using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.StaffPositions.Commands;

public sealed record UpsertStaffPositionCommand(
    Guid? Id,
    string Name,
    string? Description,
    bool IsActive) : IRequest<Guid>;

public sealed class UpsertStaffPositionCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpsertStaffPositionCommand, Guid>
{
    public async Task<Guid> Handle(UpsertStaffPositionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Vəzifə adı mütləqdir.");
        }

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var created = await repository.AddStaffPositionAsync(new StaffPosition
            {
                Name = name,
                Description = description,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            return created.Id;
        }

        var existing = await repository.GetStaffPositionByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("Vəzifə tapılmadı.");

        existing.Name = name;
        existing.Description = description;
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateStaffPositionAsync(existing, cancellationToken);
        return existing.Id;
    }
}
