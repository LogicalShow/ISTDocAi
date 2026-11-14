using ISTDocAi.Domain.Common.Entities;
using ISTDocAi.Domain.Common.Enums;

namespace ISTDocAi.Domain.Entities.IAM;

public class IAM_PermissionAudit : Entity
{
    public string PerformedByUserId { get; set; } = string.Empty;
    
    public AccessAuditEventType EventType { get; set; }

    public string? TargetUserId { get; set; }

    public string? TargetRoleId { get; set; }

    public Dictionary<string, PermissionChangeType>? PermissionChanges { get; set; }

    public PermissionChangeSource? Source { get; set; }

    public string? SourceRoleId { get; set; }

    public DateTime PerformedAt { get; set; }
}
