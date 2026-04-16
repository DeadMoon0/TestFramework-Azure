using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TestFramework.Azure.Exceptions;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

internal record RemoteConnection(Uri BasePath)
{
    internal async Task EnsurePingAsync()
    {
        Ping pingSender = new Ping();

        PingReply reply = await pingSender.SendPingAsync(BasePath.Host);
        if (reply.Status != IPStatus.Success)
        {
            throw new FunctionAppPingException("Ping was not Successful with Status: " + reply.Status, null);
        }
    }
}