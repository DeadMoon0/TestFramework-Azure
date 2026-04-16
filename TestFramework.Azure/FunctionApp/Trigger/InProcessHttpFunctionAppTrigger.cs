using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.FunctionApp.InProcessProxies;
using TestFramework.Azure.FunctionApp.TriggerConfigs;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.Trigger;

internal class InProcessHttpFunctionAppTrigger<TFunction>(
    Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult?>> action,
    VariableReference<CommonHttpRequest> request)
    : Step<HttpResponseMessage>
    where TFunction : notnull
{
    public override string Name => "In-Process Http FunctionApp Trigger";
    public override string Description => $"Directly invokes {typeof(TFunction).Name} in-process";
    public override bool DoesReturn => true;

    public override Step<HttpResponseMessage> Clone() =>
        new InProcessHttpFunctionAppTrigger<TFunction>(action, request).WithClonedOptions(this);

    public override StepInstance<Step<HttpResponseMessage>, HttpResponseMessage> GetInstance() =>
        new StepInstance<Step<HttpResponseMessage>, HttpResponseMessage>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (request.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(request.Identifier!.Identifier, StepIOKind.Variable, true, typeof(CommonHttpRequest)));
    }

    public override async Task<HttpResponseMessage?> Execute(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        CommonHttpRequest commonRequest = request.GetRequiredValue(variableStore);

        DefaultHttpContext httpContext = new() { RequestServices = serviceProvider };
        HttpRequest httpRequest = httpContext.Request;

        if (commonRequest.Content is not null)
        {
            byte[] bytes = await commonRequest.Content.ReadAsByteArrayAsync(cancellationToken);
            httpRequest.Body = new MemoryStream(bytes);
            httpRequest.ContentLength = bytes.Length;
            string? contentType = commonRequest.Content.Headers.ContentType?.ToString();
            if (contentType is not null) httpRequest.ContentType = contentType;
            httpRequest.Method = HttpMethods.Post;
        }

        foreach (var header in commonRequest.Headers)
        {
            string[] values = [.. header.Value];
            if (values.Length <= 1)
            {
                httpRequest.Headers[header.Key] = new StringValues(values);
            }
            else
            {
                // Match remote HTTP formatting where repeated header values are typically rendered as a single comma-space list.
                httpRequest.Headers[header.Key] = new StringValues(string.Join(", ", values.Where(v => !string.IsNullOrEmpty(v))));
            }
        }

        TFunction functionInstance = serviceProvider.GetRequiredService<TFunction>();
        FunctionAppHttpInProcessCallProxy proxy = new(httpRequest, serviceProvider, variableStore);
        IActionResult? result = await action(functionInstance, proxy);

        return ConvertToHttpResponseMessage(result);
    }

    private static HttpResponseMessage ConvertToHttpResponseMessage(IActionResult? result)
    {
        if (result is null)
            return new HttpResponseMessage(HttpStatusCode.OK);

        if (result is ObjectResult obj)
        {
            HttpResponseMessage response = new((HttpStatusCode)(obj.StatusCode ?? 200));
            if (obj.Value is not null)
                response.Content = new StringContent(obj.Value.ToString()!);
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
            string body = JsonSerializer.Serialize(json.Value);
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return response;
        }

        if (result is StatusCodeResult sc)
            return new HttpResponseMessage((HttpStatusCode)sc.StatusCode);

        throw new NotSupportedException(
            $"IActionResult type '{result.GetType().Name}' is not supported by {nameof(InProcessHttpFunctionAppTrigger<TFunction>)}. " +
            $"Supported types: {nameof(ObjectResult)}, {nameof(ContentResult)}, {nameof(JsonResult)}, {nameof(StatusCodeResult)}.");
    }
}
