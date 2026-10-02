using System.Net.Http;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

internal record TriggerRouting(string Name);
internal record TriggerHttpRouting(string Path, HttpMethod Method);