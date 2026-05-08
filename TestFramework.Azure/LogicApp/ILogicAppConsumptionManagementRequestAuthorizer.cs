using System.Net.Http;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;

namespace TestFramework.Azure.LogicApp;

/// <summary>
/// Applies authentication to Logic App Consumption management requests.
/// </summary>
public interface ILogicAppConsumptionManagementRequestAuthorizer
{
    /// <summary>
    /// Applies any required authentication headers to the outgoing request.
    /// </summary>
    Task AuthorizeAsync(LogicAppIdentifier identifier, LogicAppConfig config, HttpRequestMessage request, CancellationToken cancellationToken);
}