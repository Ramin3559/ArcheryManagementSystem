namespace EShooting.Web.Contracts.Sessions;

public sealed class ReturnEquipmentBulkRequest
{
    public List<Guid> SessionIds { get; set; } = [];
}

