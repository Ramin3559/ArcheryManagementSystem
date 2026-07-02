using EShooting.Domain.Enums;

namespace EShooting.Application.Customers;

public static class PaymentMethodParser
{
    public static PaymentMethod? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "cash" or "negd" or "nagd" or "nağd" or "0" => PaymentMethod.Cash,
            "card" or "kart" or "1" => PaymentMethod.Card,
            _ => Enum.TryParse<PaymentMethod>(value, true, out var parsed) ? parsed : null
        };
    }

    public static string ToLabel(PaymentMethod? method) => method switch
    {
        PaymentMethod.Cash => "Nağd",
        PaymentMethod.Card => "Kart",
        _ => "—"
    };
}
