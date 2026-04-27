using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

/// <summary>
/// Builder stage used to add request payload details before invoking a remote Function App endpoint.
/// </summary>
public interface IFunctionAppHttpPayloadStage :
    IWithBodyAction,
    IWithHeaderAction,
    ICallRemoteHttpAction;