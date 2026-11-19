using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.Process;
using DemoMVC.Models.ViewModels;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class RoleController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public RoleController(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // GET: /Role?searchString=...&pageNumber=1&pageSize=10
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Role.Index)]
        public async Task<IActionResult> Index(string? searchString, int? pageNumber, int pageSize = 10)
        {
            int currentPageSize = pageSize is > 0 and <= 100 ? pageSize : 10;
            int currentPageNumber = pageNumber is > 0 ? pageNumber.Value : 1;

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentPageSize"] = currentPageSize;

            var rolesQuery = _roleManager.Roles.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                rolesQuery = rolesQuery.Where(r => r.Name != null && r.Name.Contains(keyword));
            }

            var claimCounts = await _context.RoleClaims
                .AsNoTracking()
                .Where(rc => rc.ClaimType == SystemPermissions.ClaimType)
                .GroupBy(rc => rc.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count);

            var userCounts = await _context.UserRoles
                .AsNoTracking()
                .GroupBy(ur => ur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count);

            var roles = await rolesQuery
                .OrderBy(r => r.Name)
                .ToListAsync();

            var roleItems = roles.Select(r => new RoleListItemVM
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                ClaimCount = claimCounts.TryGetValue(r.Id, out var cc) ? cc : 0,
                UserCount = userCounts.TryGetValue(r.Id, out var uc) ? uc : 0
            }).ToList();

            var paginatedRoles = PaginatedList<RoleListItemVM>.Create(roleItems, currentPageNumber, currentPageSize);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_RoleTablePartial", paginatedRoles);
            }

            return View(paginatedRoles);
        }

        // GET: /Role/Create
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Role.Create)]
        public IActionResult Create()
        {
            if (Request.IsAjaxRequest())
            {
                return PartialView("_CreateModal");
            }
            return View();
        }

        // POST: /Role/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Role.Create)]
        public async Task<IActionResult> Create(string? roleName)
        {
            ViewData["RoleName"] = roleName;

            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError("roleName", "Tên Role không được để trống.");
                return Request.IsAjaxRequest() ? PartialView("_CreateModal") : View();
            }

            roleName = roleName.Trim();
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                ModelState.AddModelError("roleName", $"Role '{roleName}' đã tồn tại trong hệ thống.");
                return Request.IsAjaxRequest() ? PartialView("_CreateModal") : View();
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded)
            {
                var successMsg = $"Đã tạo Role '{roleName}' thành công.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message = successMsg });
                }
                TempData["SuccessMessage"] = successMsg;
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Request.IsAjaxRequest() ? PartialView("_CreateModal") : View();
        }

        // GET: /Role/Edit/{id}
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Role.Edit)]
        public async Task<IActionResult> Edit(string? id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_EditModal", role);
            }
            return View(role);
        }

        // POST: /Role/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Role.Edit)]
        public async Task<IActionResult> Edit(string id, string? roleName)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError("roleName", "Tên Role không được để trống.");
                return Request.IsAjaxRequest() ? PartialView("_EditModal", role) : View(role);
            }

            roleName = roleName.Trim();
            var existingRole = await _roleManager.FindByNameAsync(roleName);
            if (existingRole != null && existingRole.Id != role.Id)
            {
                ModelState.AddModelError("roleName", $"Role '{roleName}' đã tồn tại trong hệ thống.");
                role.Name = roleName;
                return Request.IsAjaxRequest() ? PartialView("_EditModal", role) : View(role);
            }

            role.Name = roleName;
            var result = await _roleManager.UpdateAsync(role);
            if (result.Succeeded)
            {
                var successMsg = $"Đã cập nhật Role thành '{role.Name}'.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message = successMsg });
                }
                TempData["SuccessMessage"] = successMsg;
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Request.IsAjaxRequest() ? PartialView("_EditModal", role) : View(role);
        }

        // GET: /Role/Delete/{id}
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Role.Delete)]
        public async Task<IActionResult> Delete(string? id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            ViewBag.UserCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == role.Id);
            ViewBag.ClaimCount = await _context.RoleClaims.CountAsync(rc => rc.RoleId == role.Id && rc.ClaimType == SystemPermissions.ClaimType);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_DeleteModal", role);
            }
            return View(role);
        }

        // POST: /Role/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Role.Delete)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Không tìm thấy Role cần xóa." });
                }
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                var successMsg = $"Đã xóa Role '{role.Name}' thành công.";
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

        // =======================================================
        // PHÂN QUYỀN ĐỘNG (ROLE CLAIMS) - STT 14
        // =======================================================
        // GET: /Role/AssignClaim?roleId=...
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Role.AssignClaim)]
        public async Task<IActionResult> AssignClaim(string? roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId)) return NotFound();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingPermissionValues = existingClaims
                .Where(c => c.Type == SystemPermissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var model = new RoleClaimVM
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                Claims = SystemPermissions.GetAll()
                    .Select(p => new RoleClaimItemVM
                    {
                        Type = SystemPermissions.ClaimType,
                        Value = p.Value,
                        DisplayName = p.DisplayName,
                        Group = p.Group,
                        Description = p.Description,
                        Selected = existingPermissionValues.Contains(p.Value)
                    })
                    .ToList()
            };

            if (Request.IsAjaxRequest())
            {
                return PartialView("_AssignClaimModal", model);
            }
            return View(model);
        }

        // POST: /Role/AssignClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Role.AssignClaim)]
        public async Task<IActionResult> AssignClaim(RoleClaimVM model)
        {
            if (string.IsNullOrWhiteSpace(model.RoleId)) return NotFound();

            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role == null) return NotFound();

            var validPermissions = SystemPermissions.GetAll()
                .Select(p => p.Value)
                .ToHashSet(StringComparer.Ordinal);

            var selectedValues = (model.Claims ?? new List<RoleClaimItemVM>())
                .Where(c => c.Selected && validPermissions.Contains(c.Value))
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingPermissionClaims = existingClaims
                .Where(c => c.Type == SystemPermissions.ClaimType)
                .ToList();

            var existingValues = existingPermissionClaims
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            // Xóa các quyền bị bỏ chọn
            foreach (var claim in existingPermissionClaims)
            {
                if (!selectedValues.Contains(claim.Value))
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }
            }

            // Thêm các quyền mới được tích chọn
            foreach (var permissionValue in selectedValues)
            {
                if (!existingValues.Contains(permissionValue))
                {
                    await _roleManager.AddClaimAsync(role, new Claim(SystemPermissions.ClaimType, permissionValue));
                }
            }

            // Cập nhật SecurityStamp cho các user thuộc Role này để phiên đăng nhập cập nhật quyền mới
            if (!string.IsNullOrWhiteSpace(role.Name))
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                var currentUserId = _userManager.GetUserId(User);
                foreach (var u in usersInRole)
                {
                    await _userManager.UpdateSecurityStampAsync(u);
                    if (u.Id == currentUserId)
                    {
                        await _signInManager.RefreshSignInAsync(u);
                    }
                }
            }

            var successMsg = $"Đã cập nhật {selectedValues.Count} quyền cho Role '{role.Name}' thành công.";
            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = successMsg });
            }

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction(nameof(Index));
        }
    }
}
