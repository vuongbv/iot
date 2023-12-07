using Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

namespace WE.IOT.Service.SignalRHub;

//[Authorize(Roles = "authenticated")]
public class IotHub : Hub<IIotHubClient>
{
    public const string TenantIdParam = "TenantId";
    public const string MeasurementChanged = "MeasurementChange";
    public const string IotHubGroup = "IotHub";
    public const string MeasurementChange = "MeasurementChange";

    public async Task ConnectToMeasurementGroup()
    {
        var tenantName = Context.GetHttpContext()?.Request.Query["tenant_name"];

        if (string.IsNullOrEmpty(tenantName))
        {
            throw new TenantNotFoundException();
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, IotHubGroup + tenantName);
    }

    public async Task DisconnectToMeasurementGroup()
    {
        var tenantName = Context.GetHttpContext()?.Request.Query["tenant_name"];

        if (string.IsNullOrEmpty(tenantName))
        {
            throw new TenantNotFoundException();
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, IotHubGroup + tenantName);
    }

}

