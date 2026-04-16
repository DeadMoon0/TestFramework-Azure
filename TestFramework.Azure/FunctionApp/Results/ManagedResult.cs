using System.Net;

namespace TestFramework.Azure.FunctionApp.Results;

public class ManagedResult
{
    public HttpStatusCode StatusCode { get; init; }
    public string? Body { get; init; }
}