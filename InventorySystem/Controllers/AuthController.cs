using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Configuration;
using InventorySystem.DTOs;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null, bool expired = false)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.SessionExpired = expired;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequestDTO request, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authService.LoginAsync(request);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(request);
            }

            var cookieOptions = JwtCookieHelper.CreateCookieOptions(
                request.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(1));

            Response.Cookies.Append(JwtCookieHelper.CookieName, result.Token, cookieOptions);

            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        [HttpPost]
        public IActionResult Logout()
        {
            JwtCookieHelper.DeleteToken(Response);
            return RedirectToAction("Login");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
