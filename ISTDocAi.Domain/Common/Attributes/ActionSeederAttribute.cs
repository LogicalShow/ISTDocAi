namespace ISTDocAi.Domain.Common.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ActionSeederAttribute : Attribute
{
    public string[] CustomActions { get; }

    public ActionSeederAttribute(params string[] customActions)
    {
        CustomActions = customActions ?? Array.Empty<string>();
    }
}
