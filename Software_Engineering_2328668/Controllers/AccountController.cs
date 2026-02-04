using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Data;
using Software_Engineering_2328668.Models.ViewModels;

namespace Software_Engineering_2328668.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        public AccountController(AppDbContext db) => _db = db;

        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["Title"] = "Sensore Login";
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            // Normalize inputs so "   " counts as empty.
            vm.Username = (vm.Username ?? string.Empty).Trim();
            vm.Password = (vm.Password ?? string.Empty).Trim();

            // Field-specific messages (exact behavior requested):
            // - If only username entered: ask for password.
            // - If only password entered: ask for username.
            // - If both missing: show both messages.
            if (string.IsNullOrWhiteSpace(vm.Username))
                ModelState.AddModelError(nameof(vm.Username), "Please enter your username.");
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Please enter your password.");

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Sensore Login";
                return View(vm);
            }

            // Hash the entered password using the same lower-hex SHA-256 as the seeder.
            string hash = ComputeSha256(vm.Password);

            // Look up active user by username.
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == vm.Username && u.IsActive);

            // Generic invalid message (do not reveal which field was wrong).
            if (user == null || !string.Equals(user.PasswordHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                ViewData["Title"] = "Sensore Login";
                return View(vm);
            }

            // Build auth cookie and redirect by role (unchanged).
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
        new Claim(ClaimTypes.Role, user.Role)
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return user.Role switch
            {
                "admin" => RedirectToAction("Dashboard", "Admin"),
                "patient" => RedirectToAction("Dashboard", "Patient"),
                "clinician" => RedirectToAction("Dashboard", "Clinician"),
                _ => RedirectToAction(nameof(Login))
            };
        }


        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => Content("Access denied.");

        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
