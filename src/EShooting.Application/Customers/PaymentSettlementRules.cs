namespace EShooting.Application.Customers;

/// <summary>Tam ödəniş və endirim qaydaları (borc yoxdur).</summary>
public static class PaymentSettlementRules
{
    public const decimal Tolerance = 0.01m;

    public sealed record SettlementResult(
        decimal ListPrice,
        decimal DiscountAmount,
        decimal AmountPayable,
        decimal Cash,
        decimal Card,
        decimal TotalPaid);

    public sealed record LineShare(
        decimal ListPrice,
        decimal DiscountAmount,
        decimal Cash,
        decimal Card);

    public static SettlementResult Resolve(
        decimal listPrice,
        decimal discountAmount,
        decimal amountPaidCash,
        decimal amountPaidCard,
        bool isComplimentary)
    {
        listPrice = Math.Max(0m, listPrice);

        if (isComplimentary)
        {
            if (amountPaidCash > Tolerance || amountPaidCard > Tolerance || discountAmount > Tolerance)
            {
                throw new InvalidOperationException("Ödənişsiz seçildikdə məbləğ və endirim daxil edilə bilməz.");
            }

            return new SettlementResult(listPrice, listPrice, 0m, 0m, 0m, 0m);
        }

        var discount = Math.Clamp(Math.Max(0m, discountAmount), 0m, listPrice);
        var payable = listPrice - discount;
        var cash = Math.Max(0m, amountPaidCash);
        var card = Math.Max(0m, amountPaidCard);
        var total = cash + card;

        if (payable <= Tolerance)
        {
            if (total > Tolerance)
            {
                throw new InvalidOperationException("Endirim tam ödəniləcək məbləği əhatə edir; əlavə ödəniş daxil edilə bilməz.");
            }

            return new SettlementResult(listPrice, discount, 0m, 0m, 0m, 0m);
        }

        if (Math.Abs(total - payable) > Tolerance)
        {
            if (total <= Tolerance)
            {
                throw new InvalidOperationException("Ödəniş daxil etmədiniz. Zəhmət olmasa ödəniş daxil edin.");
            }

            throw new InvalidOperationException($"Ödəniş cəmi {payable:0.##} AZN olmalıdır (nağd + kart).");
        }

        return new SettlementResult(listPrice, discount, payable, cash, card, total);
    }

    public static void EnsureDiscountAllowed(decimal discountAmount, bool canApplyDiscount)
    {
        if (discountAmount > Tolerance && !canApplyDiscount)
        {
            throw new InvalidOperationException("Endirim tətbiq etmək üçün icazəniz yoxdur.");
        }
    }

    /// <summary>Endirim və nağd/kart ödənişini zolaq və avadanlıq paylarına bölür.</summary>
    public static (LineShare Package, LineShare Equipment) SplitCombinedCheckout(
        decimal packageListPrice,
        decimal equipmentListPrice,
        decimal totalDiscount,
        decimal totalCash,
        decimal totalCard)
    {
        packageListPrice = Math.Max(0m, packageListPrice);
        equipmentListPrice = Math.Max(0m, equipmentListPrice);
        var totalList = packageListPrice + equipmentListPrice;

        if (totalList <= Tolerance)
        {
            return (
                new LineShare(0m, 0m, 0m, 0m),
                new LineShare(0m, 0m, 0m, 0m));
        }

        var discount = Math.Clamp(Math.Max(0m, totalDiscount), 0m, totalList);
        var packageDiscount = totalList > 0m ? RoundMoney(discount * packageListPrice / totalList) : 0m;
        var equipmentDiscount = discount - packageDiscount;

        var packagePayable = packageListPrice - packageDiscount;
        var equipmentPayable = equipmentListPrice - equipmentDiscount;

        var cash = Math.Max(0m, totalCash);
        var card = Math.Max(0m, totalCard);
        var packageCash = Math.Min(cash, packagePayable);
        var packageCard = Math.Min(card, Math.Max(0m, packagePayable - packageCash));
        var cashAfterPackage = cash - packageCash;
        var cardAfterPackage = card - packageCard;
        var equipmentCash = Math.Min(cashAfterPackage, equipmentPayable);
        var equipmentCard = Math.Min(cardAfterPackage, Math.Max(0m, equipmentPayable - equipmentCash));

        return (
            new LineShare(packageListPrice, packageDiscount, packageCash, packageCard),
            new LineShare(equipmentListPrice, equipmentDiscount, equipmentCash, equipmentCard));
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
