using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class FacultyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FacultyController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Faculty
        public async Task<IActionResult> Index()
        {
            var faculties = await _context.Faculty
                .Include(f => f.Students)
                .OrderBy(f => f.FacultyCode)
                .ToListAsync();
            return View(faculties);
        }

        // GET: /Faculty/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Faculty/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FacultyId,FacultyCode,FacultyName")] Faculty faculty)
        {
            if (_context.Faculty.Any(f => f.FacultyId == faculty.FacultyId))
            {
                ModelState.AddModelError("FacultyId", "Mã Khoa đã tồn tại.");
            }
            if (_context.Faculty.Any(f => f.FacultyCode == faculty.FacultyCode))
            {
                ModelState.AddModelError("FacultyCode", "Mã số ngành 2 chữ số này đã được sử dụng.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(faculty);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(faculty);
        }

        // GET: /Faculty/Edit/{id}
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null) return NotFound();

            var faculty = await _context.Faculty.FindAsync(id);
            if (faculty == null) return NotFound();

            return View(faculty);
        }

        // POST: /Faculty/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("FacultyId,FacultyCode,FacultyName")] Faculty faculty)
        {
            if (id != faculty.FacultyId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(faculty);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Faculty.Any(e => e.FacultyId == faculty.FacultyId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(faculty);
        }

        // GET: /Faculty/Delete/{id}
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null) return NotFound();

            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .FirstOrDefaultAsync(m => m.FacultyId == id);

            if (faculty == null) return NotFound();

            return View(faculty);
        }

        // POST: /Faculty/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
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
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
