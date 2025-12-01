using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace LawFirmManagement.Hubs
{
    [Authorize(Roles = "Admin")]
    public class AdminHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
            await base.OnDisconnectedAsync(exception);
        }
    }
}