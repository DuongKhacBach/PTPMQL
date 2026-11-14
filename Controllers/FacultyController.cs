using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class FacultyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FacultyController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Faculty?pageNumber=1&pageSize=5
        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien,SinhVien")]
        public async Task<IActionResult> Index(int? pageNumber, int? pageSize)
        {
            int currentPageSize = pageSize ?? 5;
            int currentPageNumber = pageNumber ?? 1;
            ViewData["CurrentPageSize"] = currentPageSize;

            var query = _context.Faculty
                .Include(f => f.Students)
                .OrderBy(f => f.FacultyCode)
                .AsNoTracking();

            var paginatedFaculties = await PaginatedList<Faculty>.CreateAsync(query, currentPageNumber, currentPageSize);
            return View(paginatedFaculties);
        }

        // GET: /Faculty/Details/{id}
        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien,SinhVien")]
        public async Task<IActionResult> Details(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FacultyId == id);

            if (faculty == null) return NotFound();

            return View(faculty);
        }

        // GET: /Faculty/Create
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Faculty/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("FacultyId,FacultyCode,FacultyName")] Faculty faculty)
        {
            faculty.FacultyId = faculty.FacultyId?.Trim().ToUpperInvariant() ?? string.Empty;
            faculty.FacultyCode = faculty.FacultyCode?.Trim() ?? string.Empty;
            faculty.FacultyName = faculty.FacultyName?.Trim() ?? string.Empty;

            if (await _context.Faculty.AnyAsync(f => f.FacultyId == faculty.FacultyId))
            {
                ModelState.AddModelError(nameof(Faculty.FacultyId), "Mã Khoa đã tồn tại.");
            }
            if (await _context.Faculty.AnyAsync(f => f.FacultyCode == faculty.FacultyCode))
            {
                ModelState.AddModelError(nameof(Faculty.FacultyCode), "Mã số ngành 2 chữ số này đã được sử dụng.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(faculty);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm Khoa '{faculty.FacultyName}' ({faculty.FacultyId}) thành công.";
                return RedirectToAction(nameof(Index));
            }
            return View(faculty);
        }

        // GET: /Faculty/Edit/{id}
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var faculty = await _context.Faculty.FindAsync(id);
            if (faculty == null) return NotFound();

            return View(faculty);
        }

        // POST: /Faculty/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id, [Bind("FacultyId,FacultyCode,FacultyName")] Faculty faculty)
        {
            if (id != faculty.FacultyId) return NotFound();

            faculty.FacultyCode = faculty.FacultyCode?.Trim() ?? string.Empty;
            faculty.FacultyName = faculty.FacultyName?.Trim() ?? string.Empty;

            if (await _context.Faculty.AnyAsync(f => f.FacultyId != id && f.FacultyCode == faculty.FacultyCode))
            {
                ModelState.AddModelError(nameof(Faculty.FacultyCode), "Mã số ngành 2 chữ số này đã được sử dụng bởi khoa khác.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(faculty);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật thông tin Khoa '{faculty.FacultyName}' ({faculty.FacultyId}).";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Faculty.AnyAsync(e => e.FacultyId == faculty.FacultyId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(faculty);
        }

        // GET: /Faculty/Delete/{id}
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FacultyId == id);

            if (faculty == null) return NotFound();

            return View(faculty);
        }

        // POST: /Faculty/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .FirstOrDefaultAsync(f => f.FacultyId == id);

            if (faculty != null)
            {
                if (faculty.Students != null && faculty.Students.Any())
                {
                    TempData["ErrorMessage"] = $"Không thể xóa khoa '{faculty.FacultyName}' vì đang có {faculty.Students.Count} sinh viên trực thuộc!";
                    return RedirectToAction(nameof(Index));
                }

                _context.Faculty.Remove(faculty);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa Khoa '{faculty.FacultyName}' ({faculty.FacultyId}) thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
