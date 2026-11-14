namespace ISTDocAi.Application.Common.Interfaces.IAM;

/// <summary>
/// Service for compressing and decompressing permission claims to reduce JWT token size.
/// Uses bitflags to represent multiple actions per page in a compact format.
/// </summary>
public interface IPermissionCompressionService
{
    /// <summary>
    /// Compresses a list of permission keys into a hierarchical dictionary format.
    /// Converts permissions like "MD.BP.AGT.VIEW", "MD.BP.AGT.ADD" into { "MD.BP.AGT": 3 }
    /// where 3 is a bitflag representing VIEW(1) + ADD(2).
    /// </summary>
    /// <param name="permissions">List of permission keys in format {PageId}.{ActionId}</param>
    /// <returns>Dictionary with PageId as key and action bitflags as value</returns>
    Dictionary<string, int> CompressPermissions(IEnumerable<string> permissions);

    /// <summary>
    /// Decompresses a hierarchical dictionary back into individual permission keys.
    /// Converts { "MD.BP.AGT": 3 } back into ["MD.BP.AGT.VIEW", "MD.BP.AGT.ADD"].
    /// </summary>
    /// <param name="compressed">Dictionary with PageId as key and action bitflags as value</param>
    /// <returns>List of individual permission keys</returns>
    IEnumerable<string> DecompressPermissions(Dictionary<string, int> compressed);
}
