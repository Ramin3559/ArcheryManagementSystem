using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.StaffMembers.Commands;

public sealed record SetStaffMemberActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetStaffMemberActiveCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetStaffMemberActiveCommand>
{
    public async Task Handle(SetStaffMemberActiveCommand request, CancellationToken cancellationToken)
    {
        var member = await repository.GetStaffMemberByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("İşçi tapılmadı.");

        member.IsActive = request.IsActive;
        member.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateStaffMemberAsync(member, cancellationToken);
    }
}
