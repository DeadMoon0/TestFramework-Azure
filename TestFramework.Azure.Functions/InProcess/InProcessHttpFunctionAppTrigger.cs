using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Functions.InProcess;

internal class InProcessHttpFunctionAppTrigger<TFunction>(
    Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult?>> action,
    VariableReference<CommonHttpRequest> request)
    : Step<HttpResponseResultContext>
    where TFunction : notnull
{
    public override string Name => "In-Process Http FunctionApp Trigger";
    public override string Description => $"Directly invokes {typeof(TFunction).Name} in-process";
    public override bool DoesReturn => true;

    public override Step<HttpResponseResultContext> Clone() =>
        new InProcessHttpFunctionAppTrigger<TFunction>(action, request).WithClonedOptions(this);

    public override StepInstance<Step<HttpResponseResultContext>, HttpResponseResultContext> GetInstance() =>
        new StepInstance<Step<HttpResponseResultContext>, HttpResponseResultContext>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(request);
    }

    public override async Task<HttpResponseResultContext?> Execute(RunContext context)
    {
        CommonHttpRequest commonRequest = request.GetRequiredValue(context.Variables);

        DefaultHttpContext httpContext = new() { RequestServices = context.Services };
        HttpRequest httpRequest = httpContext.Request;

        // The same headers, combined the same way, that the remote path puts on the wire - so a function
        // reads one value for a repeated header whether it is called in-process or deployed.
        foreach ((string name, string[] values) in commonRequest.ReadHeaders())
            httpRequest.Headers[name] = new StringValues(values);

        if (commonRequest.Content is not null)
        {
            byte[] bytes = await commonRequest.Content.ReadAsByteArrayAsync(context.Deadline.Token);
            httpRequest.Body = new MemoryStream(bytes);
            httpRequest.ContentLength = bytes.Length;
            httpRequest.Method = HttpMethods.Post;
        }

        TFunction functionInstance = context.Services.GetRequiredService<TFunction>();
        FunctionAppHttpInProcessCallProxy proxy = new(httpRequest, context.Services, context.Variables);
        IActionResult? result = await action(functionInstance, proxy);

        using HttpResponseMessage response = ConvertToHttpResponseMessage(result);
        return await HttpResponseResultContext.FromHttpResponseAsync(response, context.Deadline.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// The shape the Functions host gives a body: camelCase property names.
    /// </summary>
    /// <remarks>
    /// An in-process call must answer with the body a deployed app would send, or a test's assertions change when it
    /// moves from one to the other. Written with Newtonsoft, the family's one JSON library, set to the host's naming.
    /// </remarks>
    private static readonly JsonSerializerSettings HostJson = new()
    {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
    };

    private static HttpResponseMessage ConvertToHttpResponseMessage(IActionResult? result)
    {
        if (result is null)
            return new HttpResponseMessage(HttpStatusCode.OK);

        if (result is ObjectResult obj)
        {
            HttpResponseMessage response = new((HttpStatusCode)(obj.StatusCode ?? 200));
            response.Content = obj.Value switch
            {
                null => null,
                string text => new StringContent(text, Encoding.UTF8, "text/plain"),
                object value => new StringContent(JsonConvert.SerializeObject(value, HostJson), Encoding.UTF8, "application/json"),
            };
            return response;
        }

        if (result is ContentResult content)
        {
            HttpResponseMessage response = new((HttpStatusCode)(content.StatusCode ?? 200));
            if (content.Content is not null)
                response.Content = new StringContent(content.Content, Encoding.UTF8, content.ContentType ?? "text/plain");
            return response;
        }

        if (result is JsonResult json)
        {
            HttpResponseMessage response = new((HttpStatusCode)(json.StatusCode ?? 200));
            string body = JsonConvert.SerializeObject(json.Value, HostJson);
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return response;
        }

        if (result is StatusCodeResult sc)
            return new HttpResponseMessage((HttpStatusCode)sc.StatusCode);

        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
            $"IActionResult type '{result.GetType().Name}' is not supported by {nameof(InProcessHttpFunctionAppTrigger<TFunction>)}. " +
            $"Supported types: {nameof(ObjectResult)}, {nameof(ContentResult)}, {nameof(JsonResult)}, {nameof(StatusCodeResult)}.");
    }
}
