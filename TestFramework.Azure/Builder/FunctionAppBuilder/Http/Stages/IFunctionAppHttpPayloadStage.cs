using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

public interface IFunctionAppHttpPayloadStage :
    IWithBodyAction,
    IWithHeaderAction,
    ICallRemoteHttpAction;