using System.Net.Http;

namespace TestFramework.Azure.FunctionApp;

public record FunctionAppTriggerConfig
{
    public bool DoPing { get; set; } = true;
    public HttpMethod DefaultMethod { get; set; } = HttpMethod.Get;
}