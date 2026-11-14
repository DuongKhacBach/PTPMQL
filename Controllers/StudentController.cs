using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly StringComparer VietnameseComparer = StringComparer.Create(new CultureInfo("vi-VN"), ignoreCase: true);

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. READ (Xem danh sách với LINQ: Tìm kiếm, Lọc Khoa, Sắp xếp, Phân trang & Include Khoa)
        // =======================================================
        // GET: /Student?searchString=...&facultyId=...&sortOrder=...&pageNumber=1&pageSize=5
        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien,SinhVien")]
        public async Task<IActionResult> Index(string? searchString, string? facultyId, string? sortOrder, int? pageNumber, int? pageSize)
        {
            int currentPageSize = pageSize ?? 5;
            int currentPageNumber = pageNumber ?? 1;

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentFaculty"] = facultyId;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CurrentPageSize"] = currentPageSize;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["FacultySortParm"] = sortOrder == "faculty_asc" ? "faculty_desc" : "faculty_asc";
            ViewData["IdSortParm"] = sortOrder == "id_desc" ? "id_asc" : "id_desc";

            await PopulateFacultyDropDownListAsync(facultyId);

            var sortedStudents = await GetFilteredAndSortedStudentsAsync(searchString, facultyId, sortOrder);

            // 4. LINQ Pagination (Skip & Take qua PaginatedList)
            var paginatedStudents = PaginatedList<Student>.Create(sortedStudents, currentPageNumber, currentPageSize);
            return View(paginatedStudents);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> ExportExcel(string? searchString, string? facultyId, string? sortOrder)
        {
            var students = await GetFilteredAndSortedStudentsAsync(searchString, facultyId, sortOrder);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachSinhVien");

            // Hàng tiêu đề gồm 6 cột
            var headers = new[] { "STT", "Mã Sinh Viên", "Họ và Tên", "Số Điện Thoại", "Mã Khoa", "Tên Khoa" };
            for (var column = 0; column < headers.Length; column++)
            {
                var cell = worksheet.Cell(1, column + 1);
                cell.Value = headers[column];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Đổ dữ liệu sinh viên
            for (var row = 0; row < students.Count; row++)
            {
                var student = students[row];
                int currentRow = row + 2;

                worksheet.Cell(currentRow, 1).Value = row + 1;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 2).Value = student.StudentId ?? string.Empty;
                worksheet.Cell(currentRow, 3).Value = student.FullName ?? string.Empty;
                worksheet.Cell(currentRow, 4).Value = student.PhoneNumber ?? string.Empty;
                worksheet.Cell(currentRow, 5).Value = student.FacultyId ?? string.Empty;
                worksheet.Cell(currentRow, 6).Value = student.Faculty?.FacultyName ?? "Chưa phân khoa";
            }

            // Kẻ khung viền cho toàn bộ vùng bảng dữ liệu
            int lastRow = Math.Max(1, students.Count + 1);
            var tableRange = worksheet.Range(1, 1, lastRow, headers.Length);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachSinhVien_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // =======================================================
        // 1B. IMPORT EXCEL & TẢI FILE MẪU 100 SINH VIÊN HUMG
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> ImportExcel(IFormFile? excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một file Excel (.xlsx) để tải lên.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(excelFile.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Định dạng file không hợp lệ. Hệ thống chỉ hỗ trợ file Excel (.xlsx).";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var faculties = await _context.Faculty.AsNoTracking().ToListAsync();
                var facultyById = faculties.ToDictionary(f => f.FacultyId.ToUpperInvariant(), f => f.FacultyId);
                var facultyByCode = faculties.ToDictionary(f => f.FacultyCode, f => f.FacultyId);
                var facultyByName = faculties.ToDictionary(f => f.FacultyName.Trim(), f => f.FacultyId, StringComparer.OrdinalIgnoreCase);

                var existingStudents = await _context.Student.ToDictionaryAsync(
                    s => s.StudentId!,
                    s => s,
                    StringComparer.OrdinalIgnoreCase);

                int addedCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;
                bool canUpdate = User.IsInRole("Admin") || User.IsInRole("GiangVien");

                using var stream = new MemoryStream();
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    var col1 = row.Cell(1).GetString().Trim();
                    string studentId;
                    string fullName;
                    string phoneNumber;
                    string rawFaculty;

                    // Hỗ trợ linh hoạt cấu trúc 6 cột (STT ở cột 1 < 6 ký tự) và 4 cột
                    if (col1.Length < 6)
                    {
                        studentId = row.Cell(2).GetString().Trim();
                        fullName = row.Cell(3).GetString().Trim();
                        phoneNumber = row.Cell(4).GetString().Trim();
                        rawFaculty = row.Cell(5).GetString().Trim();
                    }
                    else
                    {
                        studentId = col1;
                        fullName = row.Cell(2).GetString().Trim();
                        phoneNumber = row.Cell(3).GetString().Trim();
                        rawFaculty = row.Cell(4).GetString().Trim();
                    }

                    if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(fullName))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Nếu cột 3/4 lại là tên Khoa hoặc mã Khoa (VD: file Excel chỉ có 3 cột: Mã SV | Họ tên | Khoa)
                    if (string.IsNullOrWhiteSpace(rawFaculty) && !string.IsNullOrWhiteSpace(phoneNumber) && !phoneNumber.All(char.IsDigit))
                    {
                        rawFaculty = phoneNumber;
                        phoneNumber = string.Empty;
                    }

                    // Chỉ giữ lại ký tự chữ số cho Số điện thoại
                    var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
                    if (digitsOnly.Length == 9)
                    {
                        digitsOnly = "0" + digitsOnly;
                    }
                    phoneNumber = PhoneRegex.IsMatch(digitsOnly) ? digitsOnly : GenerateDefaultPhoneNumber(studentId);

                    // Chuẩn hóa và kiểm tra khóa ngoại FacultyId
                    string? resolvedFacultyId = null;
                    if (!string.IsNullOrWhiteSpace(rawFaculty))
                    {
                        var upperFac = rawFaculty.ToUpperInvariant();
                        if (facultyById.TryGetValue(upperFac, out var matchedId))
                        {
                            resolvedFacultyId = matchedId;
                        }
                        else if (facultyByName.TryGetValue(rawFaculty, out var matchedByName))
                        {
                            resolvedFacultyId = matchedByName;
                        }
                        else
                        {
                            var paddedCode = rawFaculty.Length == 1 && char.IsDigit(rawFaculty[0])
                                ? "0" + rawFaculty
                                : rawFaculty;
                            if (facultyByCode.TryGetValue(paddedCode, out var matchedByCode))
                            {
                                resolvedFacultyId = matchedByCode;
                            }
                        }
                    }

                    // Nếu để trống cột Khoa hoặc chưa ánh xạ được, suy ra từ 2 chữ số mã ngành ở vị trí thứ 5-6 của StudentId 10 số
                    if (string.IsNullOrEmpty(resolvedFacultyId) && studentId.Length == 10 && StudentIdRegex.IsMatch(studentId))
                    {
                        var majorCode = studentId.Substring(4, 2);
                        if (facultyByCode.TryGetValue(majorCode, out var matchedFromId))
                        {
                            resolvedFacultyId = matchedFromId;
                        }
                        else if (studentId[2] == '0' && facultyByCode.TryGetValue(studentId.Substring(2, 2), out var matchedShortId))
                        {
                            resolvedFacultyId = matchedShortId;
                        }
                    }

                    if (string.IsNullOrEmpty(resolvedFacultyId))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Xử lý chống trùng khóa chính (StudentId)
                    if (!existingStudents.TryGetValue(studentId, out var existingStudent))
                    {
                        var newStudent = new Student
                        {
                            StudentId = studentId,
                            FullName = fullName,
                            PhoneNumber = phoneNumber,
                            FacultyId = resolvedFacultyId
                        };
                        _context.Student.Add(newStudent);
                        existingStudents[studentId] = newStudent;
                        addedCount++;
                    }
                    else if (canUpdate)
                    {
                        existingStudent.FullName = fullName;
                        existingStudent.PhoneNumber = phoneNumber;
                        existingStudent.FacultyId = resolvedFacultyId;
                        updatedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Nhập Excel thành công: Thêm mới {addedCount} sinh viên, cập nhật {updatedCount} sinh viên (bỏ qua {skippedCount} dòng không hợp lệ).";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi đọc file Excel: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> DownloadSampleExcel()
        {
            using var workbook = await BuildSample100StudentsWorkbookAsync(_context);

            // Lưu sẵn 1 bản vật lý tại thư mục gốc của project
            var physicalFilePath = Path.Combine(Directory.GetCurrentDirectory(), "DanhSach_100_SinhVien_HUMG.xlsx");
            workbook.SaveAs(physicalFilePath);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DanhSach_100_SinhVien_HUMG.xlsx");
        }

        public static async Task<XLWorkbook> BuildSample100StudentsWorkbookAsync(ApplicationDbContext context)
        {
            var faculties = await context.Faculty
                .OrderBy(f => f.FacultyCode)
                .AsNoTracking()
                .ToListAsync();

            if (faculties.Count == 0)
            {
                faculties = new List<Faculty>
                {
                    new() { FacultyId = "MO",   FacultyCode = "01", FacultyName = "Mỏ" },
                    new() { FacultyId = "DC",   FacultyCode = "02", FacultyName = "Địa chất" },
                    new() { FacultyId = "DK",   FacultyCode = "03", FacultyName = "Dầu khí" },
                    new() { FacultyId = "KT",   FacultyCode = "04", FacultyName = "Kinh tế & Quản trị kinh doanh" },
                    new() { FacultyId = "CNTT", FacultyCode = "05", FacultyName = "Công nghệ thông tin" },
                    new() { FacultyId = "CD",   FacultyCode = "06", FacultyName = "Cơ - Điện" },
                    new() { FacultyId = "XD",   FacultyCode = "07", FacultyName = "Xây dựng" },
                    new() { FacultyId = "MT",   FacultyCode = "08", FacultyName = "Môi trường" },
                    new() { FacultyId = "TDBD", FacultyCode = "09", FacultyName = "Trắc địa - Bản đồ và Quản lý đất đai" }
                };
            }

            var existingIds = (await context.Student
                .Where(s => s.StudentId != null)
                .Select(s => s.StudentId!)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string[] lastNames = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Vũ", "Phan", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
            string[] middleNames = { "Văn", "Thị", "Minh", "Đức", "Hoàng", "Ngọc", "Quang", "Thanh", "Tuấn", "Hải", "Thu", "Phương", "Gia", "Bảo" };
            string[] firstNames = { "An", "Bình", "Cường", "Dũng", "Đạt", "Giang", "Hải", "Hiếu", "Hùng", "Hưng", "Khánh", "Khoa", "Lâm", "Linh", "Long", "Mai", "Nam", "Nga", "Nhung", "Oanh", "Phúc", "Phương", "Quân", "Quỳnh", "Sơn", "Thảo", "Thắng", "Thịnh", "Trang", "Trung", "Tú", "Tuấn", "Tùng", "Việt", "Vy", "Yến" };
            string[] phonePrefixes = { "091", "094", "096", "097", "098", "032", "033", "034", "035", "036", "037", "038", "039", "086", "090", "093" };
            string[] cohortYears = { "21", "22", "23", "24" };

            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSach100SV_HUMG");

            var headers = new[] { "STT", "Mã Sinh Viên", "Họ và Tên", "Số Điện Thoại", "Mã Khoa", "Tên Khoa" };
            for (int col = 0; col < headers.Length; col++)
            {
                worksheet.Cell(1, col + 1).Value = headers[col];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var perFacultySeq = new Dictionary<string, int>();

            for (int i = 0; i < 100; i++)
            {
                var faculty = faculties[i % faculties.Count];
                var year = cohortYears[i % cohortYears.Length];
                var prefix = $"{year}21{faculty.FacultyCode}";

                if (!perFacultySeq.TryGetValue(prefix, out int seq))
                {
                    seq = 801;
                }

                string studentId;
                do
                {
                    studentId = $"{prefix}{seq:D4}";
                    seq++;
                } while (existingIds.Contains(studentId));

                perFacultySeq[prefix] = seq;
                existingIds.Add(studentId);

                var fullName = $"{lastNames[i % lastNames.Length]} {middleNames[(i * 3 + 1) % middleNames.Length]} {firstNames[(i * 7 + 2) % firstNames.Length]}";
                var phonePrefix = phonePrefixes[i % phonePrefixes.Length];
                var phoneNumber = $"{phonePrefix}{(2000000 + i * 73519) % 10000000:D7}";

                int currentRow = i + 2;
                worksheet.Cell(currentRow, 1).Value = i + 1;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 2).SetValue(studentId);
                worksheet.Cell(currentRow, 3).SetValue(fullName);
                worksheet.Cell(currentRow, 4).SetValue(phoneNumber);
                worksheet.Cell(currentRow, 5).SetValue(faculty.FacultyId);
                worksheet.Cell(currentRow, 6).SetValue(faculty.FacultyName);
            }

            var tableRange = worksheet.Range(1, 1, 101, headers.Length);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Columns().AdjustToContents();

            return workbook;
        }

        private async Task<List<Student>> GetFilteredAndSortedStudentsAsync(string? searchString, string? facultyId, string? sortOrder)
        {
            var query = _context.Student
                .Include(s => s.Faculty)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(facultyId))
            {
                var fac = facultyId.Trim();
                query = query.Where(s => s.FacultyId == fac);
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();
                query = query.Where(s => (s.FullName != null && s.FullName.Contains(search))
                                      || (s.StudentId != null && s.StudentId.Contains(search))
                                      || (s.Faculty != null && s.Faculty.FacultyName.Contains(search))
                                      || (s.PhoneNumber != null && s.PhoneNumber.Contains(search)));
            }

            var list = await query.ToListAsync();

            return sortOrder switch
            {
                "name_desc" => list
                    .OrderByDescending(s => GetGivenName(s.FullName), VietnameseComparer)
                    .ThenByDescending(s => s.FullName ?? string.Empty, VietnameseComparer)
                    .ToList(),
                "faculty_asc" => list
                    .OrderBy(s => s.Faculty?.FacultyName ?? string.Empty, VietnameseComparer)
                    .ThenBy(s => GetGivenName(s.FullName), VietnameseComparer)
                    .ToList(),
                "faculty_desc" => list
                    .OrderByDescending(s => s.Faculty?.FacultyName ?? string.Empty, VietnameseComparer)
                    .ThenBy(s => GetGivenName(s.FullName), VietnameseComparer)
                    .ToList(),
                "id_desc" => list.OrderByDescending(s => s.StudentId, StringComparer.OrdinalIgnoreCase).ToList(),
                "id_asc" => list.OrderBy(s => s.StudentId, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => list
                    .OrderBy(s => GetGivenName(s.FullName), VietnameseComparer)
                    .ThenBy(s => s.FullName ?? string.Empty, VietnameseComparer)
                    .ToList()
            };
        }

        private static string GetGivenName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
            var trimmed = fullName.Trim();
            int lastSpaceIndex = trimmed.LastIndexOf(' ');
            return lastSpaceIndex >= 0 ? trimmed.Substring(lastSpaceIndex + 1) : trimmed;
        }

        // =======================================================
        // 2. DETAILS (Xem chi tiết với LINQ & Include)
        // =======================================================
        // GET: /Student/Details/{id}
        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien,SinhVien")]
        public async Task<IActionResult> Details(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var student = await _context.Student
                .Include(s => s.Faculty)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // =======================================================
        // 3. API SINH MÃ TỰ ĐỘNG THEO KHOA
        // =======================================================
        // GET: /Student/GetNextStudentId?facultyId=CNTT
        [HttpGet]
        [Authorize(Roles = "Admin,GiangVien")]
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
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> Create()
        {
            await PopulateFacultyDropDownListAsync();
            return View();
        }

        // POST: /Student/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> Create([Bind("StudentId,FullName,FacultyId,PhoneNumber")] Student student)
        {
            student.StudentId = student.StudentId?.Trim();
            student.FullName = student.FullName?.Trim();
            student.PhoneNumber = new string((student.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(student.PhoneNumber))
            {
                ModelState.AddModelError(nameof(Student.PhoneNumber), "Vui lòng nhập Số điện thoại");
            }
            else if (!PhoneRegex.IsMatch(student.PhoneNumber))
            {
                ModelState.AddModelError(nameof(Student.PhoneNumber), "SĐT chỉ được nhập chữ số, bắt đầu bằng 0 và gồm 10-11 số");
            }

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
                string majorCode;
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

                var faculty = await _context.Faculty.AsNoTracking().FirstOrDefaultAsync(f => f.FacultyCode == majorCode);
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
            if (!string.IsNullOrEmpty(student.StudentId) && await StudentExistsAsync(student.StudentId))
            {
                ModelState.AddModelError(nameof(Student.StudentId), "Mã sinh viên đã tồn tại trong hệ thống");
            }

            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm sinh viên '{student.FullName}' (Mã SV: {student.StudentId}) thành công.";
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
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> Edit(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var student = await _context.Student.FindAsync(id);
            if (student == null) return NotFound();

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            return View(student);
        }

        // POST: /Student/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> Edit(string id, [Bind("StudentId,FullName,FacultyId,PhoneNumber")] Student student)
        {
            if (id != student.StudentId) return NotFound();

            student.FullName = student.FullName?.Trim();
            student.PhoneNumber = new string((student.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(student.PhoneNumber))
            {
                ModelState.AddModelError(nameof(Student.PhoneNumber), "Vui lòng nhập Số điện thoại");
            }
            else if (!PhoneRegex.IsMatch(student.PhoneNumber))
            {
                ModelState.AddModelError(nameof(Student.PhoneNumber), "SĐT chỉ được nhập chữ số, bắt đầu bằng 0 và gồm 10-11 số");
            }

            // Mã 10 số: khoa được chọn phải khớp với 2 số ngành trong mã
            if (StudentIdRegex.IsMatch(id) && !string.IsNullOrEmpty(student.FacultyId))
            {
                var faculty = await _context.Faculty.AsNoTracking().FirstOrDefaultAsync(f => f.FacultyId == student.FacultyId);
                if (faculty != null)
                {
                    string expectedCode = (id[2] == '0' && id.Substring(2, 2) == faculty.FacultyCode)
                        ? id.Substring(2, 2)
                        : id.Substring(4, 2);

                    if (faculty.FacultyCode != expectedCode)
                    {
                        ModelState.AddModelError(nameof(Student.FacultyId),
                            $"Mã SV có mã ngành '{expectedCode}' nên không thể chuyển sang khoa {faculty.FacultyName}");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật thông tin sinh viên '{student.FullName}' (Mã SV: {student.StudentId}).";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await StudentExistsAsync(student.StudentId!)) return NotFound();
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var student = await _context.Student
                .Include(s => s.Faculty)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // POST: /Student/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student != null)
            {
                _context.Student.Remove(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa sinh viên '{student.FullName}' (Mã SV: {student.StudentId}) khỏi hệ thống.";
            }
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // HÀM HỖ TRỢ NGHIỆP VỤ (HELPER METHODS)
        // =======================================================

        // Mã SV chuẩn: 10 chữ số = [Năm 2 số][Hệ 2 số][Ngành 2 số][STT 4 số]
        private static readonly Regex StudentIdRegex = new(@"^\d{10}$", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"^0\d{9,10}$", RegexOptions.Compiled);
        private static readonly string[] DefaultPhonePrefixes = { "091", "094", "096", "097", "098", "032", "033", "034", "035", "036", "037", "038", "039", "086", "090", "093" };
        private const string DefaultYear = "21";   // Dùng khi khoa chưa có sinh viên nào
        private const string DefaultSystem = "21"; // Hệ Đại học chính quy

        public static string GenerateDefaultPhoneNumber(string? seed)
        {
            int hash = Math.Abs((seed ?? "0").GetHashCode());
            string prefix = DefaultPhonePrefixes[hash % DefaultPhonePrefixes.Length];
            int tail = 1000000 + (hash % 9000000);
            return $"{prefix}{tail:D7}";
        }

        public static async Task FixInvalidPhoneNumbersAsync(ApplicationDbContext context)
        {
            var students = await context.Student.ToListAsync();
            bool hasChanges = false;

            foreach (var s in students)
            {
                var raw = s.PhoneNumber?.Trim() ?? string.Empty;
                var digits = new string(raw.Where(char.IsDigit).ToArray());
                if (digits.Length == 9)
                {
                    digits = "0" + digits;
                }

                if (!PhoneRegex.IsMatch(digits))
                {
                    s.PhoneNumber = GenerateDefaultPhoneNumber(s.StudentId);
                    hasChanges = true;
                }
                else if (s.PhoneNumber != digits)
                {
                    s.PhoneNumber = digits;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }
        }

        // Sinh mã tự động:
        // - Khoa đã có SV (VD: 2221050629) => giữ nguyên 6 số đầu "222105", STT + 1 => 2221050630
        // - Có nhiều tiền tố (VD: 2221050629 và 2821050743) => lấy mã lớn nhất (khóa mới nhất) => 2821050744
        // - Khoa chưa có SV => 21 + 21 + [mã ngành] + 0001
        private async Task<string> GenerateStudentIdAsync(string facultyId)
        {
            var faculty = await _context.Faculty.AsNoTracking().FirstOrDefaultAsync(f => f.FacultyId == facultyId);
            if (faculty == null) return string.Empty;

            var existingIds = (await _context.Student
                .Where(s => s.StudentId != null && s.StudentId.Length == 10)
                .Select(s => s.StudentId!)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Tìm mã sinh viên mới nhất của khoa (10 số, chứa đúng mã ngành của khoa)
            var latest = existingIds
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
                    while (next <= 999999 && existingIds.Contains($"{prefix}{next:D6}")) next++;
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

            while (next <= 9999 && existingIds.Contains($"{prefix}{next:D4}")) next++;
            return next > 9999 ? string.Empty : $"{prefix}{next:D4}";
        }

        // Đổ danh sách Khoa vào SelectList (hiển thị "05 · Công nghệ thông tin") và danh sách thô cho JavaScript
        private async Task PopulateFacultyDropDownListAsync(object? selectedFaculty = null)
        {
            var faculties = await _context.Faculty
                .OrderBy(f => f.FacultyCode)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.Faculties = faculties;
            ViewBag.FacultyList = new SelectList(
                faculties.Select(f => new { f.FacultyId, Text = $"{f.FacultyCode} · {f.FacultyName}" }),
                "FacultyId", "Text", selectedFaculty);
        }

        private Task<bool> StudentExistsAsync(string id)
        {
            return _context.Student.AnyAsync(s => s.StudentId == id);
        }
    }
}
