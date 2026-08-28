using EShooting.Domain.Enums;

namespace EShooting.Application.Athletes;

public static class ClubCardTypeLabels
{
    public static string Get(ClubCardType type) => type switch
    {
        ClubCardType.Boz => "Boz",
        ClubCardType.Qirmizi => "Qırmızı",
        ClubCardType.Qara => "Qara",
        ClubCardType.VipQizili => "VIP-Qızılı",
        _ => type.ToString()
    };

    public static string FormatCard(ClubCardType type, string cardNumber) =>
        $"{Get(type)} · {cardNumber}";

    public static string FormatUnavailable(ClubCardType type, string cardNumber) =>
        "Mövcud deyil";
}
