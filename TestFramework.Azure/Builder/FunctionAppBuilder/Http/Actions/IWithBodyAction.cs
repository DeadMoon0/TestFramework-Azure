using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

/// <summary>
/// Adds body content to a remote Function App HTTP request.
/// </summary>
public interface IWithBodyAction
{
    /// <summary>
    /// Adds a text body to the request.
    /// </summary>
    /// <param name="text">The text body variable.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithBody(VariableReference<string> text);

    /// <summary>
    /// Adds a binary body to the request.
    /// </summary>
    /// <param name="data">The binary body variable.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithBody(VariableReference<byte[]> data);
}