using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Customers;
using EShooting.Application.Athletes.Commands;
using EShooting.Application.Equipment;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using EShooting.Web.Contracts.EquipmentSales;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EShooting.Web.Controllers;

[ApiController]
[Authorize(Policy = "ReceptionPanel")]
[Route("equipment-sales")]
public sealed class EquipmentSalesController(ITrainingCenterRepository repository, IMediator mediator) : ControllerBase
{
    [HttpPost("sale")]
    public async Task<IActionResult> CreateSale([FromBody] CreateEquipmentSaleRequest request, CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanSellEquipment) is { } denied)
        {
            return denied;
        }

        try
        {
            var lines = (request.Lines ?? [])
                .Where(x => x.EquipmentItemId != Guid.Empty && x.Quantity > 0)
                .GroupBy(x => x.EquipmentItemId)
                .Select(g => new
                {
                    EquipmentItemId = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    DiscountAmount = g.Sum(x => Math.Max(0m, x.DiscountAmount))
                })
                .ToList();
            if (lines.Count == 0)
            {
                return BadRequest(new { error = "Avadanlıq seçilməyib." });
            }

            Guid athleteId;
            var isNewCustomer = false;
            if (request.AthleteId is Guid existingId && existingId != Guid.Empty)
            {
                var ath = await repository.GetAthleteByIdAsync(existingId, cancellationToken);
                if (ath is null)
                {
                    return BadRequest(new { error = "Müştəri tapılmadı." });
                }

                athleteId = existingId;
            }
            else
            {
                var first = (request.FirstName ?? "").Trim();
                var last = (request.LastName ?? "").Trim();
                var phone = (request.PhoneNumber ?? "").Trim();
                if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(phone))
                {
                    return BadRequest(new { error = "Ad, Soyad və Telefon mütləqdir." });
                }

                var registered = await mediator.Send(
                    new QuickRegisterAthleteCommand(first, last, phone, User.GetStaffMemberId()),
                    cancellationToken);
                athleteId = registered.AthleteId;
                isNewCustomer = registered.IsNewCustomer;
            }

            var athlete = await repository.GetAthleteByIdAsync(athleteId, cancellationToken);
            var customerName = athlete?.FullName ?? "—";

            var catalog = await repository.GetEquipmentItemsAsync(activeOnly: true, cancellationToken);
            var byId = catalog.ToDictionary(x => x.Id);

            decimal total = 0m;
            decimal totalDiscount = 0m;
            var receiptLines = new List<EquipmentSaleReceiptLine>();
            var stockToDeduct = new Dictionary<Guid, int>();

            foreach (var l in lines)
            {
                if (!byId.TryGetValue(l.EquipmentItemId, out var item) || item.IsDeleted || !item.IsActive)
                {
                    return BadRequest(new { error = "Seçilmiş avadanlıq tapılmadı və ya deaktivdir." });
                }

                if (item.SaleQuantity < l.Quantity)
                {
                    return BadRequest(new { error = $"«{item.Name}» üçün satış stoku kifayət etmir (mövcud: {item.SaleQuantity}, istənilən: {l.Quantity})." });
                }

                var unitPrice = EquipmentIssuanceRules.ResolveUnitPrice(item);
                var lineList = unitPrice * l.Quantity;
                var lineDiscount = Math.Min(Math.Max(0m, l.DiscountAmount), lineList);
                total += lineList;
                totalDiscount += lineDiscount;
                stockToDeduct[item.Id] = l.Quantity;
                receiptLines.Add(new EquipmentSaleReceiptLine
                {
                    ReceiptId = Guid.Empty,
                    EquipmentItemId = item.Id,
                    Quantity = l.Quantity,
                    UnitPrice = unitPrice,
                    DiscountAmount = lineDiscount
                });
            }

            if (total <= 0m)
            {
                return BadRequest(new { error = "Satış məbləği sıfırdır." });
            }

            if (request.DiscountAmount > PaymentSettlementRules.Tolerance
                && Math.Abs(request.DiscountAmount - totalDiscount) > PaymentSettlementRules.Tolerance)
            {
                return BadRequest(new { error = "Endirim hər avadanlıq sətirində ayrıca daxil edilməlidir." });
            }

            try
            {
                PaymentSettlementRules.EnsureDiscountAllowed(
                    totalDiscount,
                    User.HasReceptionPermission(ReceptionStaffClaims.CanApplyDiscount));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }

            var receipt = CustomerBillingService.BuildEquipmentSaleReceipt(
                athleteId,
                total,
                totalDiscount,
                request.AmountPaidCash,
                request.AmountPaidCard,
                User.GetStaffMemberId());
            foreach (var rl in receiptLines)
            {
                rl.ReceiptId = receipt.Id;
            }

            await repository.CreateEquipmentSaleAsync(receipt, receiptLines, stockToDeduct, cancellationToken);
            return Ok(new
            {
                receiptId = receipt.Id,
                totalAmount = total,
                athleteId,
                customerName,
                isNewCustomer,
                soldByStaffId = receipt.CreatedByStaffId
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            return BadRequest(new { error = MapSaleDbError(ex) });
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "Satış qeydə alınarkən xəta baş verdi." });
        }
    }

    private static string MapSaleDbError(DbUpdateException ex)
    {
        var detail = ex.InnerException?.Message ?? ex.Message;
        if (detail.Contains("UX_Athletes_PhoneNumber", StringComparison.OrdinalIgnoreCase)
            || (detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                && detail.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase)))
        {
            return "Bu telefon nömrəsi artıq qeydiyyatdadır. Müştərini axtarışdan seçin.";
        }

        if (detail.Contains("FK_EquipmentSaleReceiptLines", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("EquipmentSaleReceipts", StringComparison.OrdinalIgnoreCase))
        {
            return "Satış qeydə alınmadı. Səhifəni yeniləyib yenidən cəhd edin.";
        }

        return "Satış qeydə alınmadı. Səhifəni yeniləyib yenidən cəhd edin.";
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> ListReceipts([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = (q ?? "").Trim();
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var receipts = await repository.GetEquipmentSaleReceiptsAsync(cancellationToken);
        var lines = await repository.GetEquipmentSaleReceiptLinesAsync(cancellationToken);

        var athById = athletes.ToDictionary(x => x.Id);
        var filtered = receipts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(r =>
            {
                if (!athById.TryGetValue(r.AthleteId, out var a)) return false;
                return (a.FullName?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
                    || (a.PhoneNumber?.Contains(new string(query.Where(char.IsDigit).ToArray())) ?? false);
            });
        }

        var lineCountByReceipt = lines.GroupBy(x => x.ReceiptId).ToDictionary(g => g.Key, g => g.Count());
        var result = filtered
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .Select(r =>
            {
                var name = athById.TryGetValue(r.AthleteId, out var a) ? a.FullName : "—";
                var phone = athById.TryGetValue(r.AthleteId, out var a2) ? a2.PhoneNumber : "";
                return new
                {
                    id = r.Id,
                    athleteId = r.AthleteId,
                    customerName = name,
                    phoneNumber = phone,
                    type = r.Type.ToString(),
                    originalReceiptId = r.OriginalReceiptId,
                    totalAmount = r.TotalAmount,
                    createdAtUtc = r.CreatedAtUtc,
                    lineCount = lineCountByReceipt.GetValueOrDefault(r.Id)
                };
            })
            .ToList();

        return Ok(new { items = result });
    }

    [HttpGet("receipts/{id:guid}")]
    public async Task<IActionResult> GetReceipt([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var receipt = await repository.GetEquipmentSaleReceiptByIdAsync(id, cancellationToken);
        if (receipt is null) return NotFound();
        var lines = (await repository.GetEquipmentSaleReceiptLinesAsync(cancellationToken))
            .Where(x => x.ReceiptId == id)
            .ToList();
        var equipment = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var map = equipment.ToDictionary(x => x.Id, x => x.Name);

        return Ok(new
        {
            id = receipt.Id,
            athleteId = receipt.AthleteId,
            type = receipt.Type.ToString(),
            originalReceiptId = receipt.OriginalReceiptId,
            totalAmount = receipt.TotalAmount,
            amountPaidCash = receipt.AmountPaidCash,
            amountPaidCard = receipt.AmountPaidCard,
            createdAtUtc = receipt.CreatedAtUtc,
            lines = lines.Select(l => new
            {
                id = l.Id,
                equipmentItemId = l.EquipmentItemId,
                equipmentName = map.GetValueOrDefault(l.EquipmentItemId) ?? "Avadanlıq",
                quantity = l.Quantity,
                unitPrice = l.UnitPrice,
                lineTotal = l.UnitPrice * l.Quantity
            })
        });
    }

    [HttpGet("customers/{athleteId:guid}/returnable")]
    public async Task<IActionResult> GetCustomerReturnableItems(
        [FromRoute] Guid athleteId,
        CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(athleteId, cancellationToken);
        if (athlete is null)
        {
            return NotFound(new { error = "Müştəri tapılmadı." });
        }

        var receipts = await repository.GetEquipmentSaleReceiptsAsync(cancellationToken);
        var lines = await repository.GetEquipmentSaleReceiptLinesAsync(cancellationToken);
        var equipment = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var nameMap = equipment.ToDictionary(x => x.Id, x => x.Name);

        var lots = EquipmentCustomerReturnRules.BuildActiveReturnableLots(athleteId, receipts, lines);
        var items = EquipmentCustomerReturnRules.SummarizeReturnable(lots, nameMap);
        var purchases = EquipmentCustomerReturnRules.BuildReturnablePurchaseSummaries(lots, receipts, lines);
        var totalDiscount = purchases.Sum(x => x.DiscountAmount);
        var latestPurchaseUtc = items.Count > 0 ? items.Max(x => x.SaleDateUtc) : (DateTime?)null;

        return Ok(new
        {
            athleteId,
            customerName = athlete.FullName,
            phoneNumber = athlete.PhoneNumber,
            totalDiscountAmount = totalDiscount,
            latestPurchaseDateLocal = latestPurchaseUtc is DateTime latest
                ? AzerbaijanTime.UtcToLocalDateTime(latest).ToString("dd.MM.yyyy HH:mm")
                : null,
            purchases = purchases.Select(x => new
            {
                receiptId = x.ReceiptId,
                saleDateUtc = x.SaleDateUtc,
                saleDateLocal = AzerbaijanTime.UtcToLocalDateTime(x.SaleDateUtc).ToString("dd.MM.yyyy HH:mm"),
                listTotal = x.ListTotal,
                discountAmount = x.DiscountAmount,
                paidAmount = x.PaidAmount
            }),
            items = items.Select(x => new
            {
                equipmentItemId = x.EquipmentItemId,
                equipmentName = x.EquipmentName,
                saleDateUtc = x.SaleDateUtc,
                saleDateLocal = AzerbaijanTime.UtcToLocalDateTime(x.SaleDateUtc).ToString("dd.MM.yyyy HH:mm"),
                purchasedQuantity = x.PurchasedQuantity,
                returnableQuantity = x.ReturnableQuantity,
                unitListPrice = x.UnitListPrice,
                unitDiscountAmount = x.UnitDiscountAmount,
                refundUnitPrice = x.RefundUnitPrice,
                unitPrice = x.RefundUnitPrice
            })
        });
    }

    [HttpPost("return")]
    public async Task<IActionResult> CreateReturn([FromBody] CreateEquipmentReturnRequest request, CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanReturnEquipment) is { } denied)
        {
            return denied;
        }

        var linesReq = (request.Lines ?? [])
            .Where(x => x.EquipmentItemId != Guid.Empty)
            .Select(x => (x.EquipmentItemId, Quantity: x.Quantity > 0 ? x.Quantity : 1))
            .ToList();
        if (linesReq.Count == 0)
        {
            return BadRequest(new { error = "Avadanlıq seçilməyib." });
        }

        if (request.AthleteId != Guid.Empty)
        {
            return await CreateCustomerReturnAsync(request.AthleteId, linesReq, cancellationToken);
        }

        if (request.OriginalReceiptId == Guid.Empty)
        {
            return BadRequest(new { error = "Müştəri seçilməyib." });
        }

        return await CreateReceiptReturnAsync(request.OriginalReceiptId, linesReq, cancellationToken);
    }

    async Task<IActionResult> CreateCustomerReturnAsync(
        Guid athleteId,
        List<(Guid EquipmentItemId, int Quantity)> linesReq,
        CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(athleteId, cancellationToken);
        if (athlete is null)
        {
            return BadRequest(new { error = "Müştəri tapılmadı." });
        }

        var receipts = await repository.GetEquipmentSaleReceiptsAsync(cancellationToken);
        var allLines = await repository.GetEquipmentSaleReceiptLinesAsync(cancellationToken);
        var lots = EquipmentCustomerReturnRules.BuildActiveReturnableLots(athleteId, receipts, allLines);
        var (allocations, error) = EquipmentCustomerReturnRules.AllocateCustomerReturn(lots, linesReq);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        if (allocations.Count == 0)
        {
            return BadRequest(new { error = "Qaytarıla bilən avadanlıq tapılmadı." });
        }

        decimal totalRefund = 0m;
        var receiptIds = new List<Guid>();
        foreach (var group in allocations.GroupBy(x => x.SaleReceiptId))
        {
            decimal groupRefund = 0m;
            var returnLines = new List<EquipmentSaleReceiptLine>();
            foreach (var alloc in group)
            {
                groupRefund += alloc.RefundUnitPrice * alloc.Quantity;
                returnLines.Add(new EquipmentSaleReceiptLine
                {
                    ReceiptId = Guid.Empty,
                    EquipmentItemId = alloc.EquipmentItemId,
                    Quantity = alloc.Quantity,
                    UnitPrice = alloc.RefundUnitPrice
                });

                var item = await repository.GetEquipmentItemByIdAsync(alloc.EquipmentItemId, cancellationToken);
                if (item is not null)
                {
                    EShooting.Application.Equipment.EquipmentIssuanceRules.ApplyStockOnReturn(
                        item,
                        EquipmentIssueType.Sale,
                        alloc.Quantity);
                    await repository.UpdateEquipmentItemAsync(item, cancellationToken);
                }
            }

            var retReceipt = new EquipmentSaleReceipt
            {
                AthleteId = athleteId,
                Type = EquipmentSaleReceiptType.Return,
                OriginalReceiptId = group.Key,
                TotalAmount = groupRefund,
                AmountPaidCash = groupRefund,
                AmountPaidCard = 0m,
                CreatedByStaffId = User.GetStaffMemberId(),
                CreatedAtUtc = DateTime.UtcNow
            };
            foreach (var rl in returnLines)
            {
                rl.ReceiptId = retReceipt.Id;
            }

            await repository.AddEquipmentSaleReceiptAsync(retReceipt, returnLines, cancellationToken);
            totalRefund += groupRefund;
            receiptIds.Add(retReceipt.Id);
        }

        return Ok(new { receiptIds, refundAmount = totalRefund });
    }

    async Task<IActionResult> CreateReceiptReturnAsync(
        Guid originalReceiptId,
        List<(Guid EquipmentItemId, int Quantity)> linesReq,
        CancellationToken cancellationToken)
    {
        var original = await repository.GetEquipmentSaleReceiptByIdAsync(originalReceiptId, cancellationToken);
        if (original is null || original.Type != EquipmentSaleReceiptType.Sale)
        {
            return BadRequest(new { error = "Satış qaiməsi tapılmadı." });
        }

        var originalLines = (await repository.GetEquipmentSaleReceiptLinesAsync(cancellationToken))
            .Where(x => x.ReceiptId == original.Id)
            .ToList();
        var origByItemId = originalLines.ToDictionary(x => x.EquipmentItemId);

        decimal refund = 0m;
        var returnLines = new List<EquipmentSaleReceiptLine>();
        foreach (var l in linesReq)
        {
            if (!origByItemId.TryGetValue(l.EquipmentItemId, out var orig))
            {
                return BadRequest(new { error = "Seçilmiş avadanlıq bu qaimədə yoxdur." });
            }
            if (l.Quantity > orig.Quantity)
            {
                return BadRequest(new { error = "Qaytarma sayı alınan saydan çox ola bilməz." });
            }

            var refundUnit = EquipmentCustomerReturnRules.ComputeRefundUnitPrice(original, orig, originalLines);
            refund += refundUnit * l.Quantity;
            returnLines.Add(new EquipmentSaleReceiptLine
            {
                ReceiptId = Guid.Empty,
                EquipmentItemId = l.EquipmentItemId,
                Quantity = l.Quantity,
                UnitPrice = refundUnit
            });

            var item = await repository.GetEquipmentItemByIdAsync(l.EquipmentItemId, cancellationToken);
            if (item is not null)
            {
                EShooting.Application.Equipment.EquipmentIssuanceRules.ApplyStockOnReturn(
                    item,
                    EquipmentIssueType.Sale,
                    l.Quantity);
                await repository.UpdateEquipmentItemAsync(item, cancellationToken);
            }
        }

        var retReceipt = new EquipmentSaleReceipt
        {
            AthleteId = original.AthleteId,
            Type = EquipmentSaleReceiptType.Return,
            OriginalReceiptId = original.Id,
            TotalAmount = refund,
            AmountPaidCash = refund,
            AmountPaidCard = 0m,
            CreatedByStaffId = User.GetStaffMemberId(),
            CreatedAtUtc = DateTime.UtcNow
        };
        foreach (var rl in returnLines)
        {
            rl.ReceiptId = retReceipt.Id;
        }

        await repository.AddEquipmentSaleReceiptAsync(retReceipt, returnLines, cancellationToken);
        return Ok(new { receiptIds = new[] { retReceipt.Id }, refundAmount = refund });
    }
}

