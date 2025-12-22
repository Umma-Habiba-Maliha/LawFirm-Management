using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System;

namespace LawFirmManagement.Hubs
{
    [Authorize] //  Allow ANY logged-in user (Admin, Lawyer, Client)
    public class AdminHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var user = Context.User;

            // If the user is an Admin, add them to the "Admins" group
            // This allows us to broadcast "New Registration" alerts to all admins at once.
            if (user != null && user.IsInRole("Admin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }

            // Note: Lawyers and Clients are automatically identifiable by their User ID.
            // We don't need to put them in a group; we can message them directly 
            // using _hub.Clients.User(userId).

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.User != null && Context.User.IsInRole("Admin"))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}