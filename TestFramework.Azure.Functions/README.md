# TestFramework.Azure.Functions

The parts of `TestFramework.Azure` that need a Function App's own code: running an HTTP handler in-process, and
reading a function's route, verb or name off its isolated-worker attributes.

They live apart because they need ASP.NET Core and the Azure Functions Worker, which `TestFramework.Azure` itself
does not bring. A test that only calls deployed or container-hosted Function Apps does not need this package.

## Install

```bash
dotnet add package TestFramework.Azure.Functions
```

It depends on `TestFramework.Azure`; configuration, identifiers and results are that package's.

## Use

Everything here extends the builders `TestFramework.Azure` already has, so a call reads exactly as it does there. Add
the namespace:

```csharp
using TestFramework.Azure;
using TestFramework.Azure.Functions;
```

| Call | What it does |
|------|--------------|
| `AzureExt.Trigger.FunctionApp.InProcessHttp<T>(...)` | resolves `T` from the run's services and runs your delegate against it, with an ASP.NET Core `HttpRequest` built from the payload stage |
| `AzureExt.Trigger.FunctionApp.Http(id).SelectEndpointWithMethod<T>(method)` | calls a remote Function App at the route and verb the method's `[Function]` and `[HttpTrigger]` attributes declare |
| `AzureExt.Trigger.FunctionApp.Managed<T>(id, method)` | runs a function through the host's admin endpoint, by the name in the method's `[Function]` attribute |

Without this package, `Http(id).SelectFunction(name, verb)` and `ManagedFunction(id, name)` do the remote calls by
name.

## In-process HTTP

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(AzureExt.Trigger.FunctionApp
        .InProcessHttp<OrderFunction>((function, call) => function.Run(call.HttpRequest))
        .WithHeader(Var.Const("x-trace"), Var.Const("order-42"))
        .Call())
    .Build();
```

The function type must be registered in the run's services. The response is the one the Functions host would send
for the same `IActionResult` - an object as camelCase JSON, a string as text - so a test's assertions hold when it
moves to a deployed app. `ObjectResult`, `ContentResult`, `JsonResult` and `StatusCodeResult` are supported; any other
result fails the step as a configuration mistake.

Use the smallest overload that matches what your handler returns:

- `InProcessHttp<T>((function, call) => { ... })` for synchronous handlers that return no result.
- `InProcessHttp<T>((function, call) => Task.CompletedTask)` for asynchronous handlers that return no result.
- `InProcessHttp<T>((function, call) => new OkResult())` for synchronous handlers that return `IActionResult`.
- `InProcessHttp<T>((function, call) => Task.FromResult<IActionResult>(...))` for asynchronous handlers that return `IActionResult`.

`call` gives the `HttpRequest`, `GetService<T>()` from the run's services, and `GetVariable(...)` for timeline variables.

## Endpoint selection from attributes

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(AzureExt.Trigger.FunctionApp.Http("Default").SelectEndpointWithMethod<HttpTests>(nameof(HttpTests.Run)).Call())
    .Build();
```

The method needs the isolated-worker `[Function(...)]` attribute and a parameter marked `[HttpTrigger(...)]`. The
route is taken relative to the host's route prefix, so `FunctionAppConfig.RoutePrefix` is applied once. A trigger
that declares more than one verb is refused rather than guessed at: state it with
`SelectEndpointWithMethod<T>(method, HttpMethod.Post)`. In-process-model functions, marked `[FunctionName]`, are not
read; select those with `SelectFunction(name, method)`.
