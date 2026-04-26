using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

public sealed class Lane
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public LaneType LaneType { get; set; }
}
