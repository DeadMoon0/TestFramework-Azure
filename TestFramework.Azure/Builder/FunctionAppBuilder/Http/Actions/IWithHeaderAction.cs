using System.Collections.Generic;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

public interface IWithHeaderAction
{
    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value);
    public IFunctionAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values);
    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers);
    public IFunctionAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers);
    public IFunctionAppHttpPayloadStage WithContentType(VariableReference<string> contentType) => WithHeader("Content-Type", contentType);
}