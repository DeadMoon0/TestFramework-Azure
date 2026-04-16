using System.Net.Http;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

#pragma warning disable CA1716 // Call() method name matches reserved keyword
public interface ICallRemoteHttpAction
{
    public Step<HttpResponseMessage> Call();
}
#pragma warning restore CA1716