using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

/// <summary>
/// Builder stage used to select which remote Function App endpoint to call.
/// </summary>
public interface IFunctionAppHttpConnectionStage :
    ISelectEndpointWithMethodAction,
    ISelectEndpointAction,
    ISelectFunctionAction;