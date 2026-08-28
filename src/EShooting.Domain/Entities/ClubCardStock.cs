using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

/// <summary>Fiziki klub kartı stoku (növ + nömrə). Kimdə olduğu müştəri/açıq vermədən oxunur.</summary>
public sealed class ClubCardStock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ClubCardType CardType { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
