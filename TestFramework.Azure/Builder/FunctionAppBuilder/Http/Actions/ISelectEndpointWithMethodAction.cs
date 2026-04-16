using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

public interface ISelectEndpointWithMethodAction
{
    public IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(string methodName);
}