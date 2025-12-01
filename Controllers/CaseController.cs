using LawFirmManagement.Data;
using LawFirmManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CaseController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public CaseController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ---------------------------------------------------------
        // 1. LIST ALL CASES
        // ---------------------------------------------------------
        public async Task<IActionResult> Index()
        {
            var cases = await _db.Cases
                .Include(c => c.Client)
                .Include(c => c.Lawyer)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(cases);
        }

        // ---------------------------------------------------------
        // 2. CREATE CASE (GET)
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateCaseViewModel();
            await LoadDropdowns(model);
            return View(model);
        }

        // ---------------------------------------------------------
        // 3. CREATE CASE (POST)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Create(CreateCaseViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model); // Reload lists if validation fails
                return View(model);
            }

            var newCase = new Case
            {
                CaseTitle = model.CaseTitle,
                CaseType = model.CaseType,
                Description = model.Description,
                ClientId = model.ClientId,
                LawyerId = model.LawyerId,
                Status = CaseStatus.Pending,
                StartDate = DateTime.UtcNow
            };

            _db.Cases.Add(newCase);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // Helper to load lists
        private async Task LoadDropdowns(CreateCaseViewModel model)
        {
            var clientUsers = await _userManager.GetUsersInRoleAsync("Client");
            var lawyerUsers = await _userManager.GetUsersInRoleAsync("Lawyer");

            model.ClientList = clientUsers.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.Email
            });

            model.LawyerList = lawyerUsers.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.Email
            });
        }
    }
}