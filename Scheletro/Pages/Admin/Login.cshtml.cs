using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Scheletro.Class;
using Scheletro.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Scheletro.Pages.Admin
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly dbContext _db;
        private readonly LoggerDTO _logger;

        public LoginModel(dbContext db, LoggerDTO logger)
        {
            _db = db;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Inserisci lo username.")]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Inserisci la password.")]
        public string Password { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect(AuthConfiguration.GetLandingPage(User));
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            Username = Username.Trim();

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username == Username && x.Password == Password && x.Enabled);

            if (user == null)
            {
                _logger.Log(new LoggerDTO.logDTO
                {
                    typeMessage = "AUTH",
                    TypeFunction = "Login",
                    TypeText = $"Tentativo di login fallito per username '{Username}'."
                });

                ModelState.AddModelError(string.Empty, "Credenziali non valide.");
                return Page();
            }

            var roleValue = AuthConfiguration.NormalizeRole(user.Role);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, roleValue)
            };

            if (!string.IsNullOrWhiteSpace(user.Nome))
            {
                claims.Add(new Claim(ClaimTypes.GivenName, user.Nome));
            }

            if (!string.IsNullOrWhiteSpace(user.Cognome))
            {
                claims.Add(new Claim(ClaimTypes.Surname, user.Cognome));
            }

            var identity = new ClaimsIdentity(
                claims,
                AuthConfiguration.CookieScheme,
                ClaimTypes.Name,
                ClaimTypes.Role);

            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            await HttpContext.SignOutAsync(AuthConfiguration.CookieScheme);
            await HttpContext.SignInAsync(
                AuthConfiguration.CookieScheme,
                principal,
                authProperties);

            _logger.Log(new LoggerDTO.logDTO
            {
                typeMessage = "AUTH",
                TypeFunction = "Login",
                TypeText = $"Login riuscito per utente '{user.Username}' con ruolo '{roleValue}'."
            });

            return Redirect(AuthConfiguration.GetLandingPage(roleValue));
        }
    }
}
