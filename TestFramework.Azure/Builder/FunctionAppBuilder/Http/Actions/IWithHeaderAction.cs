using System.Collections.Generic;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

/// <summary>
/// Adds headers to a remote Function App HTTP request.
/// </summary>
public interface IWithHeaderAction
{
    /// <summary>
    /// Adds a single-value header.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value);

    /// <summary>
    /// Adds a multi-value header.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="values">The header values.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values);

    /// <summary>
    /// Adds headers from a dictionary of single values.
    /// </summary>
    /// <param name="headers">The header dictionary.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers);

    /// <summary>
    /// Adds headers from a dictionary of multi-value entries.
    /// </summary>
    /// <param name="headers">The header dictionary.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers);

    /// <summary>
    /// Sets the <c>Content-Type</c> header.
    /// </summary>
    /// <param name="contentType">The content type value.</param>
    /// <returns>The current payload stage for further chaining.</returns>
    public IFunctionAppHttpPayloadStage WithContentType(VariableReference<string> contentType) => WithHeader("Content-Type", contentType);
}