using System.Net.Http;

namespace TestFramework.Azure.Functions;

/// <summary>
/// An HTTP endpoint as a function's attributes declare it: the route relative to the host's route prefix, and the verb.
/// </summary>
internal sealed record FunctionAppHttpEndpoint(string Path, HttpMethod Method);
