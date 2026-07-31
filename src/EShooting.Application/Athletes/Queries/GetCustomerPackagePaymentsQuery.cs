using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Queries;

public sealed record GetCustomerPackagePaymentsQuery(Guid AthleteId)
    : IRequest<IReadOnlyList<CustomerPackagePaymentItem>>;

public sealed class CustomerPackagePaymentItem
{
    public Guid Id { get; init; }
    public string PackageName { get; init; } = "";
    public string BillingTypeLabel { get; init; } = "";
    public decimal PriceDue { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal AmountPaidCash { get; init; }
    public decimal AmountPaidCard { get; init; }
    public decimal AmountPaid { get; init; }
    public bool IsComplimentary { get; init; }
    public bool IsActive { get; init; }
    public string CreatedAtLocal { get; init; } = "";
}

public sealed class GetCustomerPackagePaymentsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetCustomerPackagePaymentsQuery, IReadOnlyList<CustomerPackagePaymentItem>>
{
    public async Task<IReadOnlyList<CustomerPackagePaymentItem>> Handle(
        GetCustomerPackagePaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        var records = (await repository.GetCustomerPackageRecordsAsync(cancellationToken))
            .Where(r => r.AthleteId == athlete.Id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();

        return records
            .Select(r => new CustomerPackagePaymentItem
            {
                Id = r.Id,
                PackageName = r.PackageName,
                BillingTypeLabel = r.BillingTypeLabel,
                PriceDue = r.PriceDue,
                DiscountAmount = r.DiscountAmount,
                AmountPaidCash = r.AmountPaidCash,
                AmountPaidCard = r.AmountPaidCard,
                AmountPaid = r.AmountPaid,
                IsComplimentary = r.IsComplimentary,
                IsActive = r.IsActive,
                CreatedAtLocal = DateDisplayFormats.FormatDateTime(
                    AzerbaijanTime.UtcToLocalDateTime(DateTimeAssumedUtc.AsUtc(r.CreatedAtUtc)))
            })
            .ToList();
    }
}
