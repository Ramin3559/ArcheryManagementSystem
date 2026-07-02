using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.StaffPositions.Queries;

public sealed record GetStaffPositionsQuery(bool ActiveOnly = false) : IRequest<IReadOnlyCollection<StaffPositionItem>>;

public sealed class GetStaffPositionsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetStaffPositionsQuery, IReadOnlyCollection<StaffPositionItem>>
{
    public async Task<IReadOnlyCollection<StaffPositionItem>> Handle(
        GetStaffPositionsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetStaffPositionsAsync(request.ActiveOnly, cancellationToken);

        return items
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    internal static StaffPositionItem Map(StaffPosition x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Description = x.Description,
        DefaultAccessProfileId = x.DefaultAccessProfileId,
        IsActive = x.IsActive
    };
}
