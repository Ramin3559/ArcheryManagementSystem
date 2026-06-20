using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using MediatR;

namespace EShooting.Application.AccessProfiles.Queries;

public sealed record GetAccessProfileByIdQuery(Guid Id) : IRequest<AccessProfileItem?>;

public sealed class GetAccessProfileByIdQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetAccessProfileByIdQuery, AccessProfileItem?>
{
    public async Task<AccessProfileItem?> Handle(
        GetAccessProfileByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetAccessProfileByIdAsync(request.Id, cancellationToken);
        return item is null ? null : GetAccessProfilesQueryHandler.Map(item);
    }
}
