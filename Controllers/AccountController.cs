using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.Process;
using DemoMVC.Models.ViewModels;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _context = context;
        }

        // GET: /Account?searchString=...&roleFilter=...&pageNumber=1&pageSize=10
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Account.Index)]
        public async Task<IActionResult> Index(string? searchString, string? roleFilter, int? pageNumber, int pageSize = 10)
        {
            int currentPageSize = pageSize is > 0 and <= 100 ? pageSize : 10;
            int currentPageNumber = pageNumber is > 0 ? pageNumber.Value : 1;

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentRoleFilter"] = roleFilter;
            ViewData["CurrentPageSize"] = currentPageSize;

            var allRoleNames = await _roleManager.Roles
                .AsNoTracking()
                .Where(r => r.Name != null)
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();
            ViewBag.RoleList = new SelectList(allRoleNames, roleFilter);

            var usersQuery = _userManager.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                var normalizedRole = roleFilter.Trim();
                var userIdsInRole = from ur in _context.UserRoles.AsNoTracking()
                                    join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                    where r.Name == normalizedRole
                                    select ur.UserId;
                usersQuery = usersQuery.Where(u => userIdsInRole.Contains(u.Id));
            }

            var usersList = await usersQuery
                .OrderBy(u => u.IsApproved)
                .ThenBy(u => u.Email)
                .ToListAsync();

            var indexedUsers = usersList
                .Select((u, index) => new { Stt = index + 1, User = u })
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                bool hasStt = int.TryParse(keyword, out int searchStt) && searchStt > 0;

                indexedUsers = indexedUsers.Where(x =>
                    (hasStt && x.Stt == searchStt) ||
                    VietnameseTextHelper.ContainsMatch(x.User.FullName, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.User.Email, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.User.UserName, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.User.StudentCode, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.User.StaffCode, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.User.UserType, keyword))
                    .ToList();
            }

            int totalItems = indexedUsers.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)currentPageSize);
            if (totalPages > 0 && currentPageNumber > totalPages) currentPageNumber = totalPages;

            var pagedIndexed = indexedUsers
                .Skip((currentPageNumber - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToList();

            // Chỉ truy vấn Role của các User trong trang hiện tại (1 truy vấn duy nhất, không N+1)
            var pagedUserIds = pagedIndexed.Select(x => x.User.Id).ToList();

            var roleLookup = await (from ur in _context.UserRoles.AsNoTracking()
                                    join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                    where pagedUserIds.Contains(ur.UserId) && r.Name != null
                                    select new { ur.UserId, RoleName = r.Name! })
                                   .ToListAsync();

            var rolesByUserId = roleLookup
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).OrderBy(n => n).ToList());

            var userWithRoles = pagedIndexed
                .Select(x => new UserWithRoleVM
                {
                    Stt = x.Stt,
                    User = x.User,
                    Roles = rolesByUserId.TryGetValue(x.User.Id, out var roles) ? roles : new List<string>()
                })
                .ToList();

            var paginatedResult = new PaginatedList<UserWithRoleVM>(
                userWithRoles,
                totalItems,
                currentPageNumber,
                currentPageSize);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_AccountTablePartial", paginatedResult);
            }

            return View(paginatedResult);
        }

        // GET: /Account/AssignRole?userId=...
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Account.AssignRole)]
        public async Task<IActionResult> AssignRole(string? userId)
        {
            if (string.IsNullOrEmpty(userId)) return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoleVM { Id = r.Id, Name = r.Name! })
                .ToListAsync();

            var model = new AssignRoleVM
            {
                UserId = user.Id,
                UserEmail = user.Email,
                FullName = user.FullName,
                SelectedRoles = userRoles.ToList(),
                AllRoles = allRoles
            };

            if (Request.IsAjaxRequest())
            {
                return PartialView("_AssignRoleModal", model);
            }

            return View(model);
        }

        // POST: /Account/AssignRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Account.AssignRole)]
        public async Task<IActionResult> AssignRole(AssignRoleVM model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var validRoleNames = (await _roleManager.Roles
                .AsNoTracking()
                .Where(r => r.Name != null)
                .Select(r => r.Name!)
                .ToListAsync())
                .ToHashSet(StringComparer.Ordinal);

            var selectedRoles = (model.SelectedRoles ?? new List<string>())
                .Where(r => validRoleNames.Contains(r))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Chống tự gỡ vai trò Admin khỏi chính tài khoản đang đăng nhập
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId &&
                userRoles.Contains("Admin") &&
                !selectedRoles.Contains("Admin"))
            {
                const string selfLockMsg = "Bạn không thể tự gỡ vai trò 'Admin' khỏi chính tài khoản đang đăng nhập.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = selfLockMsg });
                }
                TempData["ErrorMessage"] = selfLockMsg;
                return RedirectToAction(nameof(Index));
            }

            var rolesToAdd = selectedRoles.Except(userRoles).ToList();
            var rolesToRemove = userRoles.Except(selectedRoles).ToList();

            if (rolesToAdd.Any())
            {
                await _userManager.AddToRolesAsync(user, rolesToAdd);
            }

            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            // Cập nhật SecurityStamp để quyền mới có hiệu lực nhanh chóng
            await _userManager.UpdateSecurityStampAsync(user);
            if (user.Id == _userManager.GetUserId(User))
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            var successMsg = $"Đã cập nhật quyền (Role) cho tài khoản '{user.Email}' thành công.";

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = successMsg });
            }

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // PHÊ DUYỆT TÀI KHOẢN GIẢNG VIÊN (NHIỆM VỤ 3C)
        // =======================================================
        [HttpGet, ActionName("ApproveLecturer")]
        [Authorize(Policy = SystemPermissions.Account.Approve)]
        public async Task<IActionResult> ApproveLecturerModal(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_ApproveLecturerModal", user);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Account.Approve)]
        public async Task<IActionResult> ApproveLecturer(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { success = false, message = "Thiếu mã tài khoản cần duyệt." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản cần phê duyệt." });
            }

            if (user.IsApproved && user.UserType == "GiangVien")
            {
                return Json(new { success = false, message = $"Tài khoản '{user.Email}' đã được phê duyệt trước đó." });
            }

            user.UserType = "GiangVien";
            user.IsApproved = true;
            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                var err = string.Join("; ", updateRes.Errors.Select(e => e.Description));
                return Request.IsAjaxRequest()
                    ? Json(new { success = false, message = err })
                    : RedirectToAction(nameof(Index));
            }

            if (await _roleManager.RoleExistsAsync("GiangVien") && !await _userManager.IsInRoleAsync(user, "GiangVien"))
            {
                await _userManager.AddToRoleAsync(user, "GiangVien");
            }

            // Gỡ Role SinhVien nếu trước đó có gán nhầm
            if (await _userManager.IsInRoleAsync(user, "SinhVien"))
            {
                await _userManager.RemoveFromRoleAsync(user, "SinhVien");
            }

            await _userManager.UpdateSecurityStampAsync(user);

            var successMsg = $"Đã phê duyệt tài khoản Giảng viên '{user.Email}' và gán Role 'GiangVien' thành công.";
            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = successMsg });
            }

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // XÓA TÀI KHOẢN SINH VIÊN (BẢO VỆ ADMIN & GIẢNG VIÊN)
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Account.Delete)]
        public async Task<IActionResult> Delete(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null) return NotFound();

            var targetRoles = await _userManager.GetRolesAsync(targetUser);
            ViewBag.TargetRoles = targetRoles;
            ViewBag.IsProtected = IsProtectedAccount(targetUser, targetRoles);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_DeleteModal", targetUser);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Account.Delete)]
        public async Task<IActionResult> DeleteConfirmed(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { success = false, message = "Không xác định được tài khoản cần xóa." });
            }

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null)
            {
                const string notFoundMsg = "Không tìm thấy tài khoản cần xóa.";
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = notFoundMsg });
                TempData["ErrorMessage"] = notFoundMsg;
                return RedirectToAction(nameof(Index));
            }

            var targetRoles = await _userManager.GetRolesAsync(targetUser);
            if (IsProtectedAccount(targetUser, targetRoles))
            {
                const string protectedMsg = "Không thể xóa tài khoản Quản trị viên (Admin) hoặc Giáo viên/Giảng viên!";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = protectedMsg });
                }
                TempData["ErrorMessage"] = protectedMsg;
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(targetUser);
            if (result.Succeeded)
            {
                var successMsg = $"Đã xóa tài khoản '{targetUser.Email}' thành công.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message = successMsg });
                }
                TempData["SuccessMessage"] = successMsg;
            }
            else
            {
                var errMsg = string.Join("; ", result.Errors.Select(e => e.Description));
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = errMsg });
                }
                TempData["ErrorMessage"] = errMsg;
            }

            return RedirectToAction(nameof(Index));
        }

        private bool IsProtectedAccount(ApplicationUser targetUser, IList<string> targetRoles)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (targetUser.Id == currentUserId) return true;
            if (targetRoles.Contains("Admin") || targetRoles.Contains("GiangVien")) return true;
            if (string.Equals(targetUser.UserType, "Admin", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(targetUser.UserType, "GiangVien", StringComparison.OrdinalIgnoreCase) && targetUser.IsApproved) return true;
            return false;
        }
    }
}
