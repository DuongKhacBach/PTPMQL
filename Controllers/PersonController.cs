using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class PersonController : Controller
    {
        // Khai báo ApplicationDbContext để kết nối và thao tác CSDL SQLite
        private readonly ApplicationDbContext _context;

        // Constructor: Nhận ApplicationDbContext qua Dependency Injection
        public PersonController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. READ (Xem danh sách) - GET: /Person hoặc /Person/Index
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Lấy toàn bộ danh sách gồm 4 trường (PersonId, FullName, Address, School)
            var persons = await _context.Person.ToListAsync();
            return View(persons);
        }

        // =======================================================
        // 2. CREATE (Thêm mới)
        // =======================================================
        // GET: /Person/Create -> Hiển thị form thêm mới
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Person/Create -> Nhận 4 trường dữ liệu từ form gửi lên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PersonId,FullName,Address,School")] Person person)
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
        // GET: /Person/Edit/{id} -> Lấy thông tin bản ghi theo id
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

        // POST: /Person/Edit/{id} -> Cập nhật 4 trường dữ liệu vào CSDL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PersonId,FullName,Address,School")] Person person)
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
        // GET: /Person/Delete/{id} -> Hiển thị thông tin để xác nhận xóa
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

        // POST: /Person/Delete/{id} -> Thực thi xóa bản ghi khỏi SQLite
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

        // Kiểm tra sự tồn tại của PersonId
        private bool PersonExists(string id)
        {
            return _context.Person.Any(e => e.PersonId == id);
        }
    }
}
