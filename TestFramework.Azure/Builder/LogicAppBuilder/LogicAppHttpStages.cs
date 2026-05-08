using System.Collections.Generic;
using System.Net.Http;
using TestFramework.Azure.LogicApp;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.LogicAppBuilder;

/// <summary>
/// Selects the workflow targeted by a Logic App HTTP invocation.
/// </summary>
public interface ILogicAppWorkflowStage
{
    /// <summary>
    /// Selects the workflow by constant name.
    /// </summary>
    /// <param name="workflowName">The workflow name to invoke.</param>
    /// <returns>The next stage used to select the trigger.</returns>
    ILogicAppTriggerStage Workflow(string workflowName);

    /// <summary>
    /// Selects the workflow by variable-backed name.
    /// </summary>
    /// <param name="workflowName">The workflow name variable.</param>
    /// <returns>The next stage used to select the trigger.</returns>
    ILogicAppTriggerStage Workflow(VariableReference<string> workflowName);
}

/// <summary>
/// Selects the trigger inside the targeted Logic App workflow.
/// </summary>
public interface ILogicAppTriggerStage
{
    /// <summary>
    /// Selects a manual HTTP trigger by constant name.
    /// </summary>
    /// <param name="triggerName">The manual trigger name. Defaults to <c>manual</c>.</param>
    /// <returns>The payload stage used to shape the request body and headers.</returns>
    ILogicAppHttpPayloadStage Manual(string triggerName = "manual");

    /// <summary>
    /// Selects a manual HTTP trigger by variable-backed name.
    /// </summary>
    /// <param name="triggerName">The manual trigger name variable.</param>
    /// <returns>The payload stage used to shape the request body and headers.</returns>
    ILogicAppHttpPayloadStage Manual(VariableReference<string> triggerName);

    /// <summary>
    /// Selects a timer or recurrence trigger by constant name.
    /// </summary>
    /// <param name="triggerName">The timer trigger name. Defaults to <c>Recurrence</c>.</param>
    /// <returns>The stage that can execute the trigger.</returns>
    ILogicAppFireAndForgetTriggerStage Timer(string triggerName = "Recurrence");

    /// <summary>
    /// Selects a timer or recurrence trigger by variable-backed name.
    /// </summary>
    /// <param name="triggerName">The timer trigger name variable.</param>
    /// <returns>The stage that can execute the trigger.</returns>
    ILogicAppFireAndForgetTriggerStage Timer(VariableReference<string> triggerName);
}

/// <summary>
/// Executes a Logic App trigger that does not need an HTTP payload.
/// </summary>
public interface ILogicAppFireAndForgetTriggerStage
{
    /// <summary>
    /// Finalizes the request and returns the executable trigger step.
    /// </summary>
    /// <returns>The Logic App trigger step.</returns>
    Step<LogicAppTriggerResult> Call();

    /// <summary>
    /// Finalizes the request and returns the explicit Logic App run context used for stateful run polling.
    /// </summary>
    /// <returns>The Logic App run-context step.</returns>
    Step<LogicAppRunContext> CallForRunContext();
}

/// <summary>
/// Shapes the outgoing request body and headers for a Logic App manual trigger call.
/// </summary>
public interface ILogicAppHttpPayloadStage : ILogicAppFireAndForgetTriggerStage
{
    /// <summary>
    /// Finalizes the request and returns a stateless Logic App trigger step that captures the callback response directly.
    /// </summary>
    /// <returns>The Logic App capture step.</returns>
    Step<LogicAppCapturedResult> CallAndCapture();

    /// <summary>
    /// Sets a string request body.
    /// </summary>
    /// <param name="text">The request body variable.</param>
    /// <returns>The current payload stage.</returns>
    ILogicAppHttpPayloadStage WithBody(VariableReference<string> text);

    /// <summary>
    /// Sets a binary request body.
    /// </summary>
    /// <param name="data">The request body variable.</param>
    /// <returns>The current payload stage.</returns>
    ILogicAppHttpPayloadStage WithBody(VariableReference<byte[]> data);

    /// <summary>
    /// Adds a single-valued header.
    /// </summary>
    /// <param name="key">The header name variable.</param>
    /// <param name="value">The header value variable.</param>
    /// <returns>The current payload stage.</returns>
    ILogicAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value);

    /// <summary>
    /// Adds a multi-valued header.
    /// </summary>
    /// <param name="key">The header name variable.</param>
    /// <param name="values">The header values variable.</param>
    /// <returns>The current payload stage.</returns>
    ILogicAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values);

    /// <summary>
    /// Adds a dictionary of single-valued headers.
    /// </summary>
    /// <param name="headers">The headers variable.</param>
    /// <returns>The current payload stage.</returns>
    ILogicAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers);

    /// <summary>
    /// Adds a dictionary of multi-valued headers.
    /// </summary>
    /// <param name="headers">The headers variable.</param>
    /// <returns>The current payload stage.</returns>
    ILogicAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers);
}