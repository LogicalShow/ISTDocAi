using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Enums;

namespace ISTDocAi.Infrastructure.IAM.Services;

/// <summary>
/// Implementation of permission compression service using bitflags.
/// </summary>
public class PermissionCompressionService : IPermissionCompressionService
{
    /// <summary>
    /// Compresses permission keys into a hierarchical format using bitflags.
    /// Example: ["MD.BP.AGT.VIEW", "MD.BP.AGT.ADD", "MD.BP.AGT.EDIT"]
    /// becomes { "MD.BP.AGT": 7 } where 7 = VIEW(1) + ADD(2) + EDIT(4)
    /// </summary>
    public Dictionary<string, int> CompressPermissions(IEnumerable<string> permissions)
    {
        var compressed = new Dictionary<string, int>();

        foreach (var permission in permissions)
        {
            if (string.IsNullOrWhiteSpace(permission))
                continue;

            var parts = permission.Split('.');
            if (parts.Length < 2)
                continue; // Invalid format, skip

            // Extract PageId (all parts except the last one)
            var pageId = string.Join(".", parts.Take(parts.Length - 1));

            // Extract ActionId (last part)
            var actionName = parts.Last();

            // Try to parse as ActionDataEnum
            if (!Enum.TryParse<ActionDataEnum>(actionName, true, out var action))
                continue; // Unknown action, skip

            // Calculate bitflag: 1 << enum ordinal value
            // VIEW(0) = 1, ADD(1) = 2, EDIT(2) = 4, DELETE(3) = 8, PRINT(4) = 16
            var actionBit = 1 << (int)action;

            // Add or combine with existing bitflags for this page
            if (!compressed.ContainsKey(pageId))
                compressed[pageId] = 0;

            compressed[pageId] |= actionBit;
        }

        return compressed;
    }

    /// <summary>
    /// Decompresses bitflag dictionary back into individual permission keys.
    /// Example: { "MD.BP.AGT": 7 } becomes ["MD.BP.AGT.VIEW", "MD.BP.AGT.ADD", "MD.BP.AGT.EDIT"]
    /// </summary>
    public IEnumerable<string> DecompressPermissions(Dictionary<string, int> compressed)
    {
        var permissions = new List<string>();

        if (compressed == null)
            return permissions;

        foreach (var (pageId, actionFlags) in compressed)
        {
            if (string.IsNullOrWhiteSpace(pageId))
                continue;

            // Check each action in the enum
            foreach (ActionDataEnum action in Enum.GetValues(typeof(ActionDataEnum)))
            {
                var actionBit = 1 << (int)action;

                // Check if this action's bit is set
                if ((actionFlags & actionBit) != 0)
                {
                    permissions.Add($"{pageId}.{action}");
                }
            }
        }

        return permissions;
    }
}
