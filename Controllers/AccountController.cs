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

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                usersQuery = usersQuery.Where(u =>
                    (u.FullName != null && u.FullName.Contains(keyword)) ||
                    (u.Email != null && u.Email.Contains(keyword)) ||
                    (u.UserName != null && u.UserName.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                var normalizedRole = roleFilter.Trim();
                var userIdsInRole = from ur in _context.UserRoles.AsNoTracking()
                                    join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                    where r.Name == normalizedRole
                                    select ur.UserId;
                usersQuery = usersQuery.Where(u => userIdsInRole.Contains(u.Id));
            }

            var orderedUsers = usersQuery.OrderBy(u => u.Email);
            var pagedUsers = await PaginatedList<ApplicationUser>.CreateAsync(orderedUsers, currentPageNumber, currentPageSize);

            // Chỉ truy vấn Role của các User trong trang hiện tại (1 truy vấn duy nhất, không N+1)
            var pagedUserIds = pagedUsers.Select(u => u.Id).ToList();
            var roleLookup = await (from ur in _context.UserRoles.AsNoTracking()
                                    join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                    where pagedUserIds.Contains(ur.UserId) && r.Name != null
                                    select new { ur.UserId, RoleName = r.Name! })
                                   .ToListAsync();

            var rolesByUserId = roleLookup
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).OrderBy(n => n).ToList());

            var userWithRoles = pagedUsers.Select(user => new UserWithRoleVM
            {
                User = user,
                Roles = rolesByUserId.TryGetValue(user.Id, out var roles) ? roles : new List<string>()
            }).ToList();

            var paginatedResult = new PaginatedList<UserWithRoleVM>(
                userWithRoles,
                pagedUsers.TotalItems,
                pagedUsers.PageIndex,
                pagedUsers.PageSize);

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
            var selectedRoles = model.SelectedRoles ?? new List<string>();

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
    }
}
