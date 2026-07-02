namespace EShooting.Web.Contracts.Sessions;

public sealed class ReturnEquipmentBulkRequest
{
    public List<Guid> SessionIds { get; set; } = [];
    public List<Guid> IssueIds { get; set; } = [];
    public List<ReturnEquipmentDamagedLine> Damaged { get; set; } = [];
}

public sealed class ReturnEquipmentDamagedLine
{
    public Guid SessionId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public int Quantity { get; set; }
}
