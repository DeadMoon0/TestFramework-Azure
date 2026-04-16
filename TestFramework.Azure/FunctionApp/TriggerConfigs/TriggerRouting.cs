using System.Collections.Generic;
using System.Net.Http;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

internal record TriggerRouting(string Name);
internal record TriggerHttpRouting(string Path, HttpMethod Method, VariableReference<Dictionary<string, string>> queryParameter);