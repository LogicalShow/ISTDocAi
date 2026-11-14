namespace ISTDocAi.Application.Common.Interfaces.Serialization;

public interface ILogPayloadSerializer
{
    string Serialize<TValue>(TValue value);
}
