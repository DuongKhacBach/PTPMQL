using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. READ (Xem danh sách với LINQ: Tìm kiếm, Sắp xếp & Include Khoa)
        // =======================================================
        // GET: /Student?searchString=...&sortOrder=...
        [HttpGet]
        public async Task<IActionResult> Index(string? searchString, string? sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["FacultySortParm"] = sortOrder == "faculty_asc" ? "faculty_desc" : "faculty_asc";
            ViewData["IdSortParm"] = sortOrder == "id_desc" ? "id_asc" : "id_desc";

            // 1. Truy vấn LINQ có Eager Loading bảng Khoa (Include(s => s.Faculty))
            var query = _context.Student
                .Include(s => s.Faculty)
                .AsQueryable();

            // 2. LINQ Filtering (Where, Contains): Lọc theo Tên, Mã SV, Tên Khoa hoặc SĐT
            if (!string.IsNullOrEmpty(searchString))
            {
                string search = searchString.Trim();
                query = query.Where(s => (s.FullName != null && s.FullName.Contains(search))
                                      || (s.StudentId != null && s.StudentId.Contains(search))
                                      || (s.Faculty != null && s.Faculty.FacultyName.Contains(search))
                                      || (s.PhoneNumber != null && s.PhoneNumber.Contains(search)));
            }

            // 3. LINQ Sorting (OrderBy, OrderByDescending): Sắp xếp linh hoạt
            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(s => s.FullName),
                "faculty_asc" => query.OrderBy(s => s.Faculty != null ? s.Faculty.FacultyName : ""),
                "faculty_desc" => query.OrderByDescending(s => s.Faculty != null ? s.Faculty.FacultyName : ""),
                "id_desc" => query.OrderByDescending(s => s.StudentId),
                "id_asc" => query.OrderBy(s => s.StudentId),
                _ => query.OrderBy(s => s.FullName)
            };

            var students = await query.ToListAsync();
            return View(students);
        }

        // =======================================================
        // 2. DETAILS (Xem chi tiết với LINQ & Include)
        // =======================================================
        // GET: /Student/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(string? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Student
                .Include(s => s.Faculty)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // =======================================================
        // 3. API SINH MÃ TỰ ĐỘNG THEO KHOA
        // =======================================================
        // GET: /Student/GetNextStudentId?facultyId=CNTT
        [HttpGet]
        public async Task<IActionResult> GetNextStudentId(string? facultyId)
        {
            if (string.IsNullOrEmpty(facultyId))
            {
                return Json(new { studentId = "", message = "Vui lòng chọn Khoa trước khi sinh mã" });
            }

            var nextId = await GenerateStudentIdAsync(facultyId);
            return Json(new
            {
                studentId = nextId,
                message = string.IsNullOrEmpty(nextId) ? "Không thể sinh mã (khoa không tồn tại hoặc đã hết số thứ tự)" : ""
            });
        }

        // =======================================================
        // 4. CREATE (Thêm mới kèm Dropdown Khoa & Tự động sinh mã)
        // =======================================================
        // GET: /Student/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateFacultyDropDownListAsync();
            return View();
        }

        // POST: /Student/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,FullName,FacultyId,PhoneNumber")] Student student)
        {
            student.StudentId = student.StudentId?.Trim();

            if (string.IsNullOrEmpty(student.StudentId))
            {
                // Không tự nhập => tự động sinh mã theo khoa đã chọn
                if (!string.IsNullOrEmpty(student.FacultyId))
                {
                    student.StudentId = await GenerateStudentIdAsync(student.FacultyId);
                    if (string.IsNullOrEmpty(student.StudentId))
                    {
                        ModelState.AddModelError(nameof(Student.StudentId), "Không thể sinh mã tự động cho khoa này");
                    }
                }
            }
            else if (!StudentIdRegex.IsMatch(student.StudentId))
            {
                ModelState.AddModelError(nameof(Student.StudentId), "Mã sinh viên phải gồm đúng 10 chữ số");
            }
            else
            {
                // Tự nhập => xác định khoa theo mã ngành (01-09) trong mã sinh viên
                string majorCode = "";
                if (student.StudentId.Length >= 4 && student.StudentId[2] == '0' && await _context.Faculty.AnyAsync(f => f.FacultyCode == student.StudentId.Substring(2, 2)))
                {
                    // Dạng rút gọn [Năm 2 số][Ngành 2 số: 01-09]...
                    majorCode = student.StudentId.Substring(2, 2);
                }
                else
                {
                    // Dạng chuẩn 10 số HUMG [Năm 2 số][Hệ 21][Ngành 2 số: 01-09][STT 4 số]
                    majorCode = student.StudentId.Substring(4, 2);
                }

                var faculty = await _context.Faculty.FirstOrDefaultAsync(f => f.FacultyCode == majorCode);
                if (faculty == null)
                {
                    ModelState.AddModelError(nameof(Student.StudentId), $"Mã ngành '{majorCode}' không thuộc khoa nào trong hệ thống");
                }
                else
                {
                    student.FacultyId = faculty.FacultyId;
                    ModelState.Remove(nameof(Student.FacultyId));
                }
            }

            // Kiểm tra trùng Mã sinh viên
            if (!string.IsNullOrEmpty(student.StudentId) && StudentExists(student.StudentId))
            {
                ModelState.AddModelError(nameof(Student.StudentId), "Mã sinh viên đã tồn tại trong hệ thống");
            }

            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            return View(student);
        }

        // =======================================================
        // 5. UPDATE (Chỉnh sửa)
        // =======================================================
        // GET: /Student/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Student.FindAsync(id);
            if (student == null) return NotFound();

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            return View(student);
        }

        // POST: /Student/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("StudentId,FullName,FacultyId,PhoneNumber")] Student student)
        {
            if (id != student.StudentId) return NotFound();

            // Mã 10 số: khoa được chọn phải khớp với 2 số ngành trong mã
            if (StudentIdRegex.IsMatch(id) && !string.IsNullOrEmpty(student.FacultyId))
            {
                var faculty = await _context.Faculty.FindAsync(student.FacultyId);
                if (faculty != null && faculty.FacultyCode != id.Substring(4, 2))
                {
                    ModelState.AddModelError(nameof(Student.FacultyId),
                        $"Mã SV có mã ngành '{id.Substring(4, 2)}' nên không thể chuyển sang khoa {faculty.FacultyName}");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.StudentId!)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            return View(student);
        }

        // =======================================================
        // 6. DELETE (Xóa)
        // =======================================================
        // GET: /Student/Delete/{id}
        [HttpGet]
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Student
                .Include(s => s.Faculty)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // POST: /Student/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student != null)
            {
                _context.Student.Remove(student);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // HÀM HỖ TRỢ NGHIỆP VỤ (HELPER METHODS)
        // =======================================================

        // Mã SV chuẩn: 10 chữ số = [Năm 2 số][Hệ 2 số][Ngành 2 số][STT 4 số]
        private static readonly Regex StudentIdRegex = new(@"^\d{10}$");
        private const string DefaultYear = "21";   // Dùng khi khoa chưa có sinh viên nào
        private const string DefaultSystem = "21"; // Hệ Đại học chính quy

        // Sinh mã tự động:
        // - Khoa đã có SV (VD: 2221050629) => giữ nguyên 6 số đầu "222105", STT + 1 => 2221050630
        // - Có nhiều tiền tố (VD: 2221050629 và 2821050743) => lấy mã lớn nhất (khóa mới nhất) => 2821050744
        // - Khoa chưa có SV => 21 + 21 + [mã ngành] + 0001
        private async Task<string> GenerateStudentIdAsync(string facultyId)
        {
            var faculty = await _context.Faculty.FindAsync(facultyId);
            if (faculty == null) return string.Empty;

            var ids = await _context.Student
                .Where(s => s.FacultyId == facultyId && s.StudentId != null && s.StudentId.Length == 10)
                .Select(s => s.StudentId!)
                .ToListAsync();

            // Tìm mã sinh viên mới nhất của khoa (10 số, chứa đúng mã ngành của khoa)
            var latest = ids
                .Where(id => StudentIdRegex.IsMatch(id) && 
                            (id.Substring(4, 2) == faculty.FacultyCode || 
                            (id[2] == '0' && id.Substring(2, 2) == faculty.FacultyCode)))
                .OrderByDescending(id => id, StringComparer.Ordinal)
                .FirstOrDefault();

            string prefix;
            int next;
            if (latest != null)
            {
                if (latest[2] == '0' && latest.Substring(2, 2) == faculty.FacultyCode)
                {
                    // Dạng rút gọn [Năm 2 số][Ngành 2 số][STT 6 số]
                    prefix = latest.Substring(0, 4);
                    next = int.Parse(latest.Substring(4)) + 1;
                    while (next <= 999999 && StudentExists($"{prefix}{next:D6}")) next++;
                    return next > 999999 ? string.Empty : $"{prefix}{next:D6}";
                }
                else
                {
                    // Dạng chuẩn 10 số HUMG: giữ nguyên 6 số đầu [Năm 2 số][Hệ 21][Ngành 2 số], tăng STT 4 số cuối thêm 1
                    prefix = latest.Substring(0, 6);
                    next = int.Parse(latest.Substring(6)) + 1;
                }
            }
            else
            {
                // Khoa chưa có sinh viên nào => mặc định: 21 + 21 + [mã ngành] + 0001
                prefix = $"{DefaultYear}{DefaultSystem}{faculty.FacultyCode}";
                next = 1;
            }

            while (next <= 9999 && StudentExists($"{prefix}{next:D4}")) next++;
            return next > 9999 ? string.Empty : $"{prefix}{next:D4}";
        }

        // Đổ danh sách Khoa vào SelectList (hiển thị "05 · Công nghệ thông tin") và danh sách thô cho JavaScript
        private async Task PopulateFacultyDropDownListAsync(object? selectedFaculty = null)
        {
            var faculties = await _context.Faculty.OrderBy(f => f.FacultyCode).ToListAsync();
            ViewBag.Faculties = faculties;
            ViewBag.FacultyList = new SelectList(
                faculties.Select(f => new { f.FacultyId, Text = $"{f.FacultyCode} · {f.FacultyName}" }),
                "FacultyId", "Text", selectedFaculty);
        }

        private bool StudentExists(string id)
        {
            return _context.Student.Any(s => s.StudentId == id);
        }
    }
}
