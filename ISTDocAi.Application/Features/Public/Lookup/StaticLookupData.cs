using ISTDocAi.Application.Common.BaseModels;

namespace ISTDocAi.Application.Features.Public.Lookup;

public static class StaticLookupData
{
    public static readonly IReadOnlyList<LookupItem> Items =
    [
        new("1", "ACT", "Active", "نشط"),
        new("2", "INA", "Inactive", "غير نشط"),
        new("3", "PND", "Pending", "قيد الانتظار"),
        new("4", "APP", "Approved", "معتمد"),
        new("5", "REJ", "Rejected", "مرفوض"),
        new("6", "DRF", "Draft", "مسودة"),
        new("7", "CLS", "Closed", "مغلق"),
        new("8", "CAN", "Cancelled", "ملغى")
    ];
}
