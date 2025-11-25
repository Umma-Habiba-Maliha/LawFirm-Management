using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

public class AdminController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;

    public AdminController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    // ---------------------------------------------------------
    // 1. REGISTER ADMIN (GET) - Shows the form
    // ---------------------------------------------------------
    [HttpGet]
    public IActionResult RegisterAdmin()
    {
        // Point to your specific file location
        return View("~/Views/Account/Admin/RegisterAdmin.cshtml");
    }

    // ---------------------------------------------------------
    // 2. REGISTER ADMIN (POST) - Handles the submission
    // ---------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> RegisterAdmin(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Email and Password are required.");
            return View("~/Views/Account/Admin/RegisterAdmin.cshtml");
        }

        // Create the user object
        var user = new IdentityUser { UserName = email, Email = email };

        // Attempt to create the user in the database
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            // CRITICAL: Assign the "Admin" role
            await _userManager.AddToRoleAsync(user, "Admin");

            // Success! Go back to the user list
            return RedirectToAction("Users");
        }

        // If creation failed (e.g., password too weak), show errors
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        // Return the view again to show the errors
        return View("~/Views/Account/Admin/RegisterAdmin.cshtml");
    }

    // ---------------------------------------------------------
    // 3. USER LIST
    // ---------------------------------------------------------
    public IActionResult Users()
    {
        var users = _userManager.Users.ToList();
        return View("~/Views/Account/Admin/Users.cshtml", users);
    }

    // ---------------------------------------------------------
    // 4. DELETE USER
    // ---------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null)
        {
            await _userManager.DeleteAsync(user);
        }

        return RedirectToAction("Users");
    }
}