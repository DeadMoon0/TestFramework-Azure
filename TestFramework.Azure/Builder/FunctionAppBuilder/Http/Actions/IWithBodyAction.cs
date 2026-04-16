using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

public interface IWithBodyAction
{
    public IFunctionAppHttpPayloadStage WithBody(VariableReference<string> text);
    public IFunctionAppHttpPayloadStage WithBody(VariableReference<byte[]> data);
}