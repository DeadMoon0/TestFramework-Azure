using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

public interface ISelectEndpointAction
{
    public IFunctionAppHttpPayloadStage SelectEndpoint(VariableReference<string> subPath, VariableReference<HttpMethod> method);
}