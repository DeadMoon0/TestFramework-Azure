using System;
using System.Collections.Generic;
using TestFramework.Azure.Builder.FunctionAppBuilder;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.LogicApp;
using TestFramework.Azure.LogicApp.Trigger;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.LogicAppBuilder;

internal sealed class RemoteLogicAppBuilder(LogicAppIdentifier appIdentifier)
    : ILogicAppWorkflowStage, ILogicAppTriggerStage, ILogicAppHttpPayloadStage, ILogicAppExecutionStage
{
    private VariableReference<string>? _workflowName;
    private VariableReference<string>? _triggerName;
    private readonly FunctionAppHttpRequestBuilderState _request = new();
    private LogicAppTriggerExecutionMode _triggerMode = LogicAppTriggerExecutionMode.HttpCallback;

    public ILogicAppTriggerStage Workflow(string workflowName) => Workflow(Var.Const(workflowName));

    public ILogicAppTriggerStage Workflow(VariableReference<string> workflowName)
    {
        _workflowName = workflowName;
        return this;
    }

    public ILogicAppHttpPayloadStage Manual(string triggerName = "manual") => Manual(Var.Const(triggerName));

    public ILogicAppHttpPayloadStage Manual(VariableReference<string> triggerName)
    {
        _triggerName = triggerName;
        _triggerMode = LogicAppTriggerExecutionMode.HttpCallback;
        return this;
    }

    public ILogicAppExecutionStage Timer(string triggerName = "Recurrence") => Timer(Var.Const(triggerName));

    public ILogicAppExecutionStage Timer(VariableReference<string> triggerName)
    {
        _triggerName = triggerName;
        _triggerMode = LogicAppTriggerExecutionMode.ManagementRun;
        return this;
    }

    public Step<LogicAppExecutionResult> Call()
    {
        if (_triggerName is null)
            throw new TestFramework.Core.Exceptions.FrameworkStateException("No Logic App trigger was selected. Call Workflow(...).Manual(...) or Timer(...) first.");

        return new LogicAppExecutionStep(appIdentifier, BuildRequest(captureResponse: false));
    }

    public Step<LogicAppExecutionResult> CallAndCapture()
    {
        if (_triggerName is null)
            throw new TestFramework.Core.Exceptions.FrameworkStateException("No Logic App trigger was selected. Call Workflow(...).Manual(...) first.");

        if (_triggerMode != LogicAppTriggerExecutionMode.HttpCallback)
            throw new TestFramework.Core.Exceptions.UnsupportedFrameworkValueException("CallAndCapture() is only supported for Logic App HTTP callback triggers. Use Call() for timer triggers.");

        return new LogicAppExecutionStep(appIdentifier, BuildRequest(captureResponse: true));
    }

    public ILogicAppHttpPayloadStage WithBody(VariableReference<string> text)
    {
        _request.SetBody(text);
        return this;
    }

    public ILogicAppHttpPayloadStage WithBody(VariableReference<byte[]> data)
    {
        _request.SetBody(data);
        return this;
    }

    public ILogicAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string> value)
    {
        _request.AddHeader(key, value);
        return this;
    }

    public ILogicAppHttpPayloadStage WithHeader(VariableReference<string> key, VariableReference<string[]> values)
    {
        _request.AddHeader(key, values);
        return this;
    }

    public ILogicAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string>> headers)
    {
        _request.AddHeaders(headers);
        return this;
    }

    public ILogicAppHttpPayloadStage WithHeaders(VariableReference<Dictionary<string, string[]>> headers)
    {
        _request.AddHeaders(headers);
        return this;
    }

    private LogicAppExecutionRequest BuildRequest(bool captureResponse)
        => new(
            _workflowName,
            _triggerName ?? throw new TestFramework.Core.Exceptions.FrameworkStateException("No Logic App trigger was selected."),
            _triggerMode,
            _triggerMode == LogicAppTriggerExecutionMode.HttpCallback ? _request.BuildVariable() : null,
            captureResponse);
}