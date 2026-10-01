using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. READ (Xem danh sách với LINQ: Tìm kiếm & Sắp xếp)
        // =======================================================
        // GET: /Employee?searchString=...&sortOrder=...
        [HttpGet]
        public async Task<IActionResult> Index(string? searchString, string? sortOrder)
        {
            // Lưu tham số vào ViewData để tái sử dụng trên View
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["AgeSortParm"] = sortOrder == "age_asc" ? "age_desc" : "age_asc";
            ViewData["IdSortParm"] = sortOrder == "id_desc" ? "id_asc" : "id_desc";

            // 1. Khởi tạo truy vấn LINQ dạng IQueryable
            var query = _context.Employee.AsQueryable();

            // 2. LINQ Filtering (Where, Contains): Lọc theo Tên, Mã NV, hoặc Địa chỉ
            if (!string.IsNullOrEmpty(searchString))
            {
                string search = searchString.Trim();
                query = query.Where(e => (e.FullName != null && e.FullName.Contains(search))
                                      || (e.EmployeeId != null && e.EmployeeId.Contains(search))
                                      || (e.Address != null && e.Address.Contains(search)));
            }

            // 3. LINQ Sorting (OrderBy, OrderByDescending): Sắp xếp linh hoạt
            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(e => e.FullName),
                "age_asc" => query.OrderBy(e => e.Age),
                "age_desc" => query.OrderByDescending(e => e.Age),
                "id_desc" => query.OrderByDescending(e => e.EmployeeId),
                "id_asc" => query.OrderBy(e => e.EmployeeId),
                _ => query.OrderBy(e => e.FullName) // Mặc định sắp xếp theo Tên A-Z
            };

            // 4. LINQ Execution: Thực thi truy vấn bất đồng bộ ra danh sách
            var employees = await query.ToListAsync();
            return View(employees);
        }

        // =======================================================
        // 2. DETAILS (Xem chi tiết với LINQ: FirstOrDefaultAsync)
        // =======================================================
        // GET: /Employee/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // LINQ FirstOrDefaultAsync tìm kiếm bản ghi theo điều kiện
            var employee = await _context.Employee
                .FirstOrDefaultAsync(m => m.EmployeeId == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // =======================================================
        // 3. CREATE (Thêm mới)
        // =======================================================
        // GET: /Employee/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeId,FullName,Age,Address")] Employee employee)
        {
            if (ModelState.IsValid)
            {
                _context.Add(employee);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        // =======================================================
        // 4. UPDATE (Chỉnh sửa)
        // =======================================================
        // GET: /Employee/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employee.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }
            return View(employee);
        }

        // POST: /Employee/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("EmployeeId,FullName,Age,Address")] Employee employee)
        {
            if (id != employee.EmployeeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(employee);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.EmployeeId!))
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
            return View(employee);
        }

        // =======================================================
        // 5. DELETE (Xóa)
        // =======================================================
        // GET: /Employee/Delete/{id}
        [HttpGet]
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employee
                .FirstOrDefaultAsync(m => m.EmployeeId == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // POST: /Employee/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var employee = await _context.Employee.FindAsync(id);
            if (employee != null)
            {
                _context.Employee.Remove(employee);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // LINQ Any: Kiểm tra sự tồn tại của bản ghi
        private bool EmployeeExists(string id)
        {
            return _context.Employee.Any(e => e.EmployeeId == id);
        }
    }
}
