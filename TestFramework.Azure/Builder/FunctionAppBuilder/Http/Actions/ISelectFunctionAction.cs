using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

/// <summary>
/// Selects a remote Function App endpoint by Azure Function name using the default route prefix semantics.
/// </summary>
public interface ISelectFunctionAction
{
    /// <summary>
    /// Selects a remote endpoint by function name using the default Functions HTTP route prefix.
    /// </summary>
    public IFunctionAppHttpPayloadStage SelectFunction(string functionName, HttpMethod method);

    /// <summary>
    /// Selects a remote endpoint by function name using runtime-resolved values.
    /// </summary>
    public IFunctionAppHttpPayloadStage SelectFunction(VariableReference<string> functionName, VariableReference<HttpMethod> method);
}