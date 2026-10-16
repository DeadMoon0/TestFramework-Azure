using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

/// <summary>
/// Builder stage used to select which remote Function App endpoint to call.
/// </summary>
/// <remarks>
/// Selecting an endpoint from a function's own attributes, <c>SelectEndpointWithMethod&lt;T&gt;(...)</c>, needs the
/// Functions Worker types and comes with the <c>TestFramework.Azure.Functions</c> package as an extension on this stage.
/// </remarks>
public interface IFunctionAppHttpConnectionStage :
    IFunctionAppSelectEndpointAction,
    ISelectFunctionAction;