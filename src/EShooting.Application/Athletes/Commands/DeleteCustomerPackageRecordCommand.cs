using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record DeleteCustomerPackageRecordCommand(Guid RecordId) : IRequest;

public sealed class DeleteCustomerPackageRecordCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<DeleteCustomerPackageRecordCommand>
{
    public async Task Handle(DeleteCustomerPackageRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await repository.GetCustomerPackageRecordByIdAsync(request.RecordId, cancellationToken)
            ?? throw new InvalidOperationException("Ödəniş qeydi tapılmadı.");

        await repository.DeleteCustomerPackageRecordAsync(record.Id, cancellationToken);
    }
}
