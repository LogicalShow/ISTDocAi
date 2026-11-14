using MediatR;

namespace ISTDocAi.Application.Features.IAM.Events;

public enum PermissionChangeReason
{
    PermissionsAssigned,
    PermissionsImported,
    UserActiveStateChanged,
    UserDeleted
}

/// <summary>
/// Published after a committed change that alters the effective permissions (or the
/// active/existing state) of the given users. Observers invalidate the server-side
/// permission cache and push the change to the users' connected clients.
/// </summary>
public sealed record UserPermissionsChangedNotification(
    IReadOnlyCollection<string> UserIds,
    PermissionChangeReason Reason) : INotification;
