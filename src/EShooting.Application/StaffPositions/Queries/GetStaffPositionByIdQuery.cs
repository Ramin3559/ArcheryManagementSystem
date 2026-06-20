using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using MediatR;

namespace EShooting.Application.StaffPositions.Queries;

public sealed record GetStaffPositionByIdQuery(Guid Id) : IRequest<StaffPositionItem?>;

public sealed class GetStaffPositionByIdQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetStaffPositionByIdQuery, StaffPositionItem?>
{
    public async Task<StaffPositionItem?> Handle(
        GetStaffPositionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetStaffPositionByIdAsync(request.Id, cancellationToken);
        return item is null ? null : GetStaffPositionsQueryHandler.Map(item);
    }
}
