using EShooting.Application.Common.Interfaces;
using EShooting.Application.Customers;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record UpdateCustomerPackageBillingCommand(
    Guid RecordId,
    decimal PriceDue,
    decimal DiscountAmount,
    decimal AmountPaidCash,
    decimal AmountPaidCard,
    bool IsComplimentary) : IRequest;

public sealed class UpdateCustomerPackageBillingCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpdateCustomerPackageBillingCommand>
{
    public async Task Handle(UpdateCustomerPackageBillingCommand request, CancellationToken cancellationToken)
    {
        var record = await repository.GetCustomerPackageRecordByIdAsync(request.RecordId, cancellationToken)
            ?? throw new InvalidOperationException("Ödəniş qeydi tapılmadı.");

        var settlement = PaymentSettlementRules.Resolve(
            request.PriceDue,
            request.DiscountAmount,
            request.AmountPaidCash,
            request.AmountPaidCard,
            request.IsComplimentary);

        record.PriceDue = settlement.ListPrice;
        record.DiscountAmount = settlement.DiscountAmount;
        record.AmountPaidCash = settlement.Cash;
        record.AmountPaidCard = settlement.Card;
        record.AmountPaid = settlement.TotalPaid;
        record.IsComplimentary = request.IsComplimentary;

        await repository.UpdateCustomerPackageRecordAsync(record, cancellationToken);
    }
}
