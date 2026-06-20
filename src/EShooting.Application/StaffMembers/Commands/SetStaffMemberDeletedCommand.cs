using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.StaffMembers.Commands;

public sealed record SetStaffMemberDeletedCommand(Guid Id, bool IsDeleted) : IRequest;

public sealed class SetStaffMemberDeletedCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetStaffMemberDeletedCommand>
{
    public async Task Handle(SetStaffMemberDeletedCommand request, CancellationToken cancellationToken)
    {
        var member = await repository.GetStaffMemberByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("İşçi tapılmadı.");

        member.IsDeleted = request.IsDeleted;
        if (request.IsDeleted)
        {
            member.IsActive = false;
        }

        member.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateStaffMemberAsync(member, cancellationToken);
    }
}
