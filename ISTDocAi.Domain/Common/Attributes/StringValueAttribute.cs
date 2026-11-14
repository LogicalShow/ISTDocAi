namespace ISTDocAi.Domain.Common.Attributes;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class StringValueAttribute : Attribute
{
    public string Value { get; }

    public StringValueAttribute(string value)
    {
        Value = value;
    }
}

