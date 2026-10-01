using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class PersonController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PersonController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. READ (Xem danh sách với LINQ: Tìm kiếm & Sắp xếp)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? searchString, string? sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["IdSortParm"] = sortOrder == "id_desc" ? "id_asc" : "id_desc";

            // 1. LINQ IQueryable
            var query = _context.Person.AsQueryable();

            // 2. LINQ Filtering (Where, Contains)
            if (!string.IsNullOrEmpty(searchString))
            {
                string search = searchString.Trim();
                query = query.Where(p => (p.FullName != null && p.FullName.Contains(search))
                                      || (p.PersonId != null && p.PersonId.Contains(search))
                                      || (p.Address != null && p.Address.Contains(search)));
            }

            // 3. LINQ Sorting (OrderBy, OrderByDescending)
            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(p => p.FullName),
                "id_desc" => query.OrderByDescending(p => p.PersonId),
                "id_asc" => query.OrderBy(p => p.PersonId),
                _ => query.OrderBy(p => p.FullName)
            };

            var persons = await query.ToListAsync();
            return View(persons);
        }

        // =======================================================
        // 2. CREATE (Thêm mới)
        // =======================================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PersonId,FullName,Address")] Person person)
        {
            if (ModelState.IsValid)
            {
                _context.Add(person);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(person);
        }

        // =======================================================
        // 3. UPDATE (Chỉnh sửa)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var person = await _context.Person.FindAsync(id);
            if (person == null)
            {
                return NotFound();
            }
            return View(person);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PersonId,FullName,Address")] Person person)
        {
            if (id != person.PersonId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(person);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PersonExists(person.PersonId!))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(person);
        }

        // =======================================================
        // 4. DELETE (Xóa)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var person = await _context.Person.FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null)
            {
                return NotFound();
            }

            return View(person);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var person = await _context.Person.FindAsync(id);
            if (person != null)
            {
                _context.Person.Remove(person);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PersonExists(string id)
        {
            return _context.Person.Any(e => e.PersonId == id);
        }
    }
}
