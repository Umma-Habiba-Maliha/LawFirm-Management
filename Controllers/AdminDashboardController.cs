using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;

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
            // Fetch counts
            var totalClients = (await _userManager.GetUsersInRoleAsync("Client")).Count;
            var totalLawyers = (await _userManager.GetUsersInRoleAsync("Lawyer")).Count;
            var pendingCount = await _db.PendingUsers.CountAsync(p => !p.IsProcessed);
            var unreadNotifs = await _db.Notifications.CountAsync(n => !n.IsRead);

            // --- Calculate Total Revenue ---
            // Sums up the 'TotalAmount' column from the Payments table
            // We use .SumAsync to do this efficiently in the database
            decimal totalRevenue = 0;
            if (_db.Payments.Any())
            {
                totalRevenue = await _db.Payments.SumAsync(p => p.TotalAmount);
            }

            var vm = new AdminDashboardViewModel
            {
                TotalClients = totalClients,
                TotalLawyers = totalLawyers,
                PendingRequests = pendingCount,
                UnreadNotifications = unreadNotifs,

                // Pass the calculated revenue
                TotalRevenue = totalRevenue,

                RecentPending = await _db.PendingUsers
                    .Where(p => !p.IsProcessed)
                    .OrderByDescending(p => p.RequestedAt)
                    .Take(5)
                    .ToListAsync(),

                RecentNotifications = await _db.Notifications
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }

        // Quick link to show all notifications
        public async Task<IActionResult> Notifications()
        {
            var list = await _db.Notifications.OrderByDescending(n => n.CreatedAt).ToListAsync();
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(System.Guid id)
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