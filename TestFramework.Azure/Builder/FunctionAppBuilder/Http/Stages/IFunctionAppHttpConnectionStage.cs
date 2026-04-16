using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

public interface IFunctionAppHttpConnectionStage :
    ISelectEndpointWithMethodAction,
    ISelectEndpointAction;