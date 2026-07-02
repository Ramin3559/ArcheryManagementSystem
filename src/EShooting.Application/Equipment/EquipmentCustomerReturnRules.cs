using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Equipment;

public static class EquipmentCustomerReturnRules
{
    public sealed class ReturnableLot
    {
        public Guid SaleReceiptId { get; init; }
        public Guid EquipmentItemId { get; init; }
        public int OriginalQuantity { get; init; }
        public int RemainingQuantity { get; set; }
        /// <summary>Kataloq vahid qiyməti (endirimdən əvvəl).</summary>
        public decimal UnitListPrice { get; init; }
        /// <summary>1 ədəd üçün endirim (satış sətirindən).</summary>
        public decimal UnitDiscountAmount { get; init; }
        /// <summary>1 ədəd üçün geri ödəniş (endirim nəzərə alınır).</summary>
        public decimal RefundUnitPrice { get; init; }
        public DateTime SaleDateUtc { get; init; }
    }

    public sealed class ReturnableItemSummary
    {
        public Guid EquipmentItemId { get; init; }
        public string EquipmentName { get; init; } = "";
        public DateTime SaleDateUtc { get; init; }
        public int PurchasedQuantity { get; init; }
        public int ReturnableQuantity { get; init; }
        public decimal? UnitListPrice { get; init; }
        public decimal? UnitDiscountAmount { get; init; }
        public decimal? RefundUnitPrice { get; init; }
    }

    public sealed class ReturnablePurchaseSummary
    {
        public Guid ReceiptId { get; init; }
        public DateTime SaleDateUtc { get; init; }
        public decimal ListTotal { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal PaidAmount { get; init; }
    }

    public sealed class ReturnAllocation
    {
        public Guid SaleReceiptId { get; init; }
        public Guid EquipmentItemId { get; init; }
        public int Quantity { get; init; }
        public decimal RefundUnitPrice { get; init; }
    }

    public static decimal ComputeRefundUnitPrice(
        EquipmentSaleReceipt receipt,
        EquipmentSaleReceiptLine line,
        IReadOnlyCollection<EquipmentSaleReceiptLine> receiptLines)
    {
        var qty = Math.Max(1, line.Quantity);
        var lineList = line.UnitPrice * qty;
        if (lineList <= 0m)
        {
            return line.UnitPrice;
        }

        var lineDiscount = ResolveLineDiscount(receipt, line, receiptLines);
        var lineNet = lineList - lineDiscount;
        return Math.Round(lineNet / qty, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeUnitDiscountAmount(
        EquipmentSaleReceipt receipt,
        EquipmentSaleReceiptLine line,
        IReadOnlyCollection<EquipmentSaleReceiptLine> receiptLines)
    {
        var qty = Math.Max(1, line.Quantity);
        var lineList = line.UnitPrice * qty;
        if (lineList <= 0m)
        {
            return 0m;
        }

        var lineDiscount = ResolveLineDiscount(receipt, line, receiptLines);
        return Math.Round(lineDiscount / qty, 2, MidpointRounding.AwayFromZero);
    }

    static decimal ResolveLineDiscount(
        EquipmentSaleReceipt receipt,
        EquipmentSaleReceiptLine line,
        IReadOnlyCollection<EquipmentSaleReceiptLine> receiptLines)
    {
        var qty = Math.Max(1, line.Quantity);
        var lineList = line.UnitPrice * qty;
        if (lineList <= 0m)
        {
            return 0m;
        }

        var lineDiscount = Math.Min(Math.Max(0m, line.DiscountAmount), lineList);
        var totalLineDiscounts = receiptLines.Sum(x => Math.Max(0m, x.DiscountAmount));

        // Köhnə qaimələr: yalnız sətirdə endirim qeyd olunmayıbsa proporsional bölünür.
        if (lineDiscount <= 0m && totalLineDiscounts <= 0m && receipt.DiscountAmount > 0m)
        {
            var receiptListTotal = receiptLines.Sum(x => x.UnitPrice * Math.Max(1, x.Quantity));
            if (receiptListTotal > 0m)
            {
                var receiptDiscount = Math.Min(receipt.DiscountAmount, receiptListTotal);
                return receiptDiscount * (lineList / receiptListTotal);
            }
        }

        return lineDiscount;
    }

    public static List<ReturnableLot> BuildReturnableLots(
        Guid athleteId,
        IReadOnlyCollection<EquipmentSaleReceipt> receipts,
        IReadOnlyCollection<EquipmentSaleReceiptLine> allLines)
    {
        var athleteReceiptIds = receipts
            .Where(r => r.AthleteId == athleteId)
            .Select(r => r.Id)
            .ToHashSet();

        var receiptById = receipts.ToDictionary(x => x.Id);
        var linesByReceipt = allLines
            .GroupBy(x => x.ReceiptId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<EquipmentSaleReceiptLine>)g.ToList());

        var lots = receipts
            .Where(r => r.AthleteId == athleteId && r.Type == EquipmentSaleReceiptType.Sale)
            .OrderBy(r => r.CreatedAtUtc)
            .SelectMany(r =>
            {
                if (!linesByReceipt.TryGetValue(r.Id, out var receiptLines))
                {
                    return [];
                }

                return receiptLines.Select(l => new ReturnableLot
                {
                    SaleReceiptId = r.Id,
                    EquipmentItemId = l.EquipmentItemId,
                    OriginalQuantity = Math.Max(1, l.Quantity),
                    RemainingQuantity = Math.Max(1, l.Quantity),
                    UnitListPrice = l.UnitPrice,
                    UnitDiscountAmount = ComputeUnitDiscountAmount(r, l, receiptLines),
                    RefundUnitPrice = ComputeRefundUnitPrice(r, l, receiptLines),
                    SaleDateUtc = r.CreatedAtUtc
                });
            })
            .ToList();

        var returns = receipts
            .Where(r => r.AthleteId == athleteId && r.Type == EquipmentSaleReceiptType.Return)
            .OrderBy(r => r.CreatedAtUtc);

        foreach (var ret in returns)
        {
            if (ret.OriginalReceiptId is not Guid origId || !athleteReceiptIds.Contains(origId))
            {
                continue;
            }

            foreach (var retLine in allLines.Where(x => x.ReceiptId == ret.Id))
            {
                var remaining = retLine.Quantity;
                foreach (var lot in lots.Where(x =>
                             x.SaleReceiptId == origId
                             && x.EquipmentItemId == retLine.EquipmentItemId
                             && x.RemainingQuantity > 0))
                {
                    if (remaining <= 0) break;
                    var take = Math.Min(remaining, lot.RemainingQuantity);
                    lot.RemainingQuantity -= take;
                    remaining -= take;
                }
            }
        }

        return lots;
    }

    /// <summary>Hər avadanlıq üçün yalnız ən son alış qalıqlarını saxlayır.</summary>
    public static List<ReturnableLot> FilterToLatestPurchasePerItem(IReadOnlyCollection<ReturnableLot> lots)
    {
        return lots
            .Where(x => x.RemainingQuantity > 0)
            .GroupBy(x => x.EquipmentItemId)
            .SelectMany(g =>
            {
                var latestUtc = g.Max(x => x.SaleDateUtc);
                return g.Where(x => x.SaleDateUtc == latestUtc);
            })
            .ToList();
    }

    public static List<ReturnableLot> BuildActiveReturnableLots(
        Guid athleteId,
        IReadOnlyCollection<EquipmentSaleReceipt> receipts,
        IReadOnlyCollection<EquipmentSaleReceiptLine> allLines)
    {
        var lots = BuildReturnableLots(athleteId, receipts, allLines);
        return FilterToLatestPurchasePerItem(lots);
    }

    public static IReadOnlyList<ReturnableItemSummary> SummarizeReturnable(
        IReadOnlyCollection<ReturnableLot> lots,
        IReadOnlyDictionary<Guid, string> equipmentNames)
    {
        return lots
            .Where(x => x.RemainingQuantity > 0)
            .GroupBy(x => x.EquipmentItemId)
            .Select(g =>
            {
                var totalQty = g.Sum(x => x.RemainingQuantity);
                var listPrices = g.Select(x => x.UnitListPrice).Distinct().ToList();
                var discountPrices = g.Select(x => x.UnitDiscountAmount).Distinct().ToList();
                var refundPrices = g.Select(x => x.RefundUnitPrice).Distinct().ToList();
                var weightedRefund = totalQty > 0
                    ? Math.Round(g.Sum(x => x.RefundUnitPrice * x.RemainingQuantity) / totalQty, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var weightedDiscount = totalQty > 0
                    ? Math.Round(g.Sum(x => x.UnitDiscountAmount * x.RemainingQuantity) / totalQty, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new ReturnableItemSummary
                {
                    EquipmentItemId = g.Key,
                    EquipmentName = equipmentNames.GetValueOrDefault(g.Key) ?? "Avadanlıq",
                    SaleDateUtc = g.Max(x => x.SaleDateUtc),
                    PurchasedQuantity = g.Sum(x => x.OriginalQuantity),
                    ReturnableQuantity = totalQty,
                    UnitListPrice = listPrices.Count == 1 ? listPrices[0] : null,
                    UnitDiscountAmount = discountPrices.Count == 1 ? discountPrices[0] : weightedDiscount,
                    RefundUnitPrice = refundPrices.Count == 1 ? refundPrices[0] : weightedRefund
                };
            })
            .OrderByDescending(x => x.SaleDateUtc)
            .ThenBy(x => x.EquipmentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static (List<ReturnAllocation> Allocations, string? Error) AllocateCustomerReturn(
        IReadOnlyCollection<ReturnableLot> lots,
        IReadOnlyList<(Guid EquipmentItemId, int Quantity)> requested)
    {
        var working = lots
            .Where(x => x.RemainingQuantity > 0)
            .Select(x => new ReturnableLot
            {
                SaleReceiptId = x.SaleReceiptId,
                EquipmentItemId = x.EquipmentItemId,
                OriginalQuantity = x.OriginalQuantity,
                RemainingQuantity = x.RemainingQuantity,
                UnitListPrice = x.UnitListPrice,
                UnitDiscountAmount = x.UnitDiscountAmount,
                RefundUnitPrice = x.RefundUnitPrice,
                SaleDateUtc = x.SaleDateUtc
            })
            .OrderBy(x => x.SaleDateUtc)
            .ToList();

        var allocations = new List<ReturnAllocation>();
        foreach (var req in requested)
        {
            if (req.Quantity <= 0) continue;

            var available = working
                .Where(x => x.EquipmentItemId == req.EquipmentItemId && x.RemainingQuantity > 0)
                .Sum(x => x.RemainingQuantity);
            if (available < req.Quantity)
            {
                return ([], "Qaytarma sayı qalan alınmış saydan çox ola bilməz.");
            }

            var remaining = req.Quantity;
            foreach (var lot in working.Where(x => x.EquipmentItemId == req.EquipmentItemId && x.RemainingQuantity > 0))
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, lot.RemainingQuantity);
                lot.RemainingQuantity -= take;
                remaining -= take;
                allocations.Add(new ReturnAllocation
                {
                    SaleReceiptId = lot.SaleReceiptId,
                    EquipmentItemId = lot.EquipmentItemId,
                    Quantity = take,
                    RefundUnitPrice = lot.RefundUnitPrice
                });
            }
        }

        return (allocations, null);
    }

    public static List<ReturnablePurchaseSummary> BuildReturnablePurchaseSummaries(
        IReadOnlyCollection<ReturnableLot> lots,
        IReadOnlyCollection<EquipmentSaleReceipt> receipts,
        IReadOnlyCollection<EquipmentSaleReceiptLine> allLines)
    {
        var receiptIds = lots
            .Where(x => x.RemainingQuantity > 0)
            .Select(x => x.SaleReceiptId)
            .Distinct()
            .ToHashSet();

        var receiptById = receipts.ToDictionary(x => x.Id);
        var linesByReceipt = allLines
            .GroupBy(x => x.ReceiptId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<EquipmentSaleReceiptLine>)g.ToList());

        return receiptIds
            .Select(receiptId =>
            {
                if (!receiptById.TryGetValue(receiptId, out var receipt)
                    || receipt.Type != EquipmentSaleReceiptType.Sale)
                {
                    return null;
                }

                var receiptLines = linesByReceipt.GetValueOrDefault(receiptId) ?? [];
                var listTotal = receiptLines.Sum(x => x.UnitPrice * Math.Max(1, x.Quantity));
                var lineDiscountTotal = receiptLines.Sum(x => Math.Max(0m, x.DiscountAmount));
                var discountAmount = lineDiscountTotal > 0m
                    ? lineDiscountTotal
                    : Math.Max(0m, receipt.DiscountAmount);

                return new ReturnablePurchaseSummary
                {
                    ReceiptId = receiptId,
                    SaleDateUtc = receipt.CreatedAtUtc,
                    ListTotal = listTotal,
                    DiscountAmount = discountAmount,
                    PaidAmount = receipt.AmountPaid
                };
            })
            .Where(x => x is not null)
            .Cast<ReturnablePurchaseSummary>()
            .OrderByDescending(x => x.SaleDateUtc)
            .ToList();
    }
}
