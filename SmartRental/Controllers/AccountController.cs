using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartRental.Models;
using SmartRental.ViewModels.Account;

namespace SmartRental.Controllers
{
    [Authorize]
    [Route("Account/[action]")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet("/Profile")]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            return View(await CreateProfileViewModelAsync(user));
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            return View(new EditProfileViewModel
            {
                HoTen = user.HoTen ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DiaChi = user.DiaChi,
                Email = user.Email
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            model.Email = user.Email;
            if (!ModelState.IsValid) return View(model);

            user.HoTen = model.HoTen.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.DiaChi = string.IsNullOrWhiteSpace(model.DiaChi) ? null : model.DiaChi.Trim();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Đã cập nhật hồ sơ cá nhân.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet("/ChangePassword")]
        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<ProfileViewModel> CreateProfileViewModelAsync(ApplicationUser user) => new()
        {
            HoTen = user.HoTen,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DiaChi = user.DiaChi,
            Roles = (await _userManager.GetRolesAsync(user)).ToArray()
        };

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
