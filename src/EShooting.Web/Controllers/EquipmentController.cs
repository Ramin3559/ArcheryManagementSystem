using EShooting.Application.Equipment.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("equipment")]
public sealed class EquipmentController(IMediator mediator) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetEquipmentItemsQuery(ActiveOnly: true), cancellationToken);
        var result = items.Select(x => new
        {
            x.Id,
            x.Name,
            x.Category,
            usageMode = (int)x.UsageMode,
            rentalQuantity = x.RentalQuantity,
            saleQuantity = x.SaleQuantity,
            x.Quantity,
            damagedQuantity = x.DamagedQuantity,
            unitPrice = x.UnitPrice ?? x.Price,
            price = x.UnitPrice ?? x.Price
        });

        return Ok(result);
    }
}
