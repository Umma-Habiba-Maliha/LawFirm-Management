using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using LawFirmManagement.Services; // Ensure you have this for IEmailSender

namespace LawFirmManagement.Controllers
{
    [Authorize] // Default: User must be logged in
    public class ManageController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IEmailSender _emailSender;

        public ManageController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        // ============================================================
        // 1. CHANGE PASSWORD (For Logged-in Users)
        // ============================================================
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["msg"] = "Your password has been changed successfully.";
                return View();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // ============================================================
        // 2. FORGOT PASSWORD (For users who can't log in)
        // ============================================================
        [HttpGet]
        [AllowAnonymous] // Allow anyone to access this
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Generate Token
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                    // Create Link pointing to ResetPassword action in THIS controller
                    var callbackUrl = Url.Action("ResetPassword", "Manage",
                        new { email = model.Email, token = token }, Request.Scheme);

                    // Send Email
                    await _emailSender.SendEmailAsync(model.Email, "Reset Password",
                        $"Please reset your password by <a href='{callbackUrl}'>clicking here</a>.");
                }

                // Don't reveal if user exists or not
                return View("ForgotPasswordConfirmation");
            }
            return View(model);
        }

        // Helper view to show "Email Sent" message
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // ============================================================
        // 3. RESET PASSWORD (The link from email goes here)
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                ModelState.AddModelError("", "Invalid password reset token");
            }
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal user not found
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }

    // ============================================================
    // VIEW MODELS (Helper Classes)
    // ============================================================

    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current Password")]
        public string OldPassword { get; set; } = "";

        [Required, DataType(DataType.Password), Display(Name = "New Password")]
        public string NewPassword { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    // Removed duplicate definitions of ForgotPasswordViewModel and ResetPasswordViewModel
    // They are already defined in AccountController.cs (or elsewhere in the namespace)
}