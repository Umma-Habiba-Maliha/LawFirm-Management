using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminDashboardController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var totalClients = await _userManager.GetUsersInRoleAsync("Client");
            var totalLawyers = await _userManager.GetUsersInRoleAsync("Lawyer");
            var pendingCount = await _db.PendingUsers.CountAsync(p => !p.IsProcessed);
            var unreadNotifs = await _db.Notifications.CountAsync(n => !n.IsRead);

            var vm = new AdminDashboardViewModel
            {
                TotalClients = totalClients.Count,
                TotalLawyers = totalLawyers.Count,
                PendingRequests = pendingCount,
                UnreadNotifications = unreadNotifs,
                RecentPending = await _db.PendingUsers.Where(p => !p.IsProcessed).OrderByDescending(p => p.RequestedAt).Take(5).ToListAsync(),
                RecentNotifications = await _db.Notifications.OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync()
            };

            return View(vm);
        }

        // quick link to show all notifications
        public async Task<IActionResult> Notifications()
        {
            var list = await _db.Notifications.OrderByDescending(n => n.CreatedAt).ToListAsync();
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(Guid id)
        {
            var n = await _db.Notifications.FindAsync(id);
            if (n != null)
            {
                n.IsRead = true;
                _db.Notifications.Update(n);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Notifications));
        }
    }
}
