using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
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
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. READ (Danh sách LINQ: Tìm kiếm, Lọc Khoa, Sắp xếp, Phân trang)
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.Index)]
        public async Task<IActionResult> Index(string? searchString, string? facultyId, string? sortOrder, int? pageNumber, int? pageSize)
        {
            int currentPageSize = pageSize is > 0 and <= 100 ? pageSize.Value : 5;
            int currentPageNumber = pageNumber is > 0 ? pageNumber.Value : 1;

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentFaculty"] = facultyId;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CurrentPageSize"] = currentPageSize;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["FacultySortParm"] = sortOrder == "faculty_asc" ? "faculty_desc" : "faculty_asc";
            ViewData["IdSortParm"] = sortOrder == "id_desc" ? "id_asc" : "id_desc";

            await PopulateFacultyDropDownListAsync(facultyId);

            var sortedStudents = await GetFilteredAndSortedStudentsAsync(searchString, facultyId, sortOrder);
            var paginatedStudents = PaginatedList<SttRowVM<Student>>.Create(sortedStudents, currentPageNumber, currentPageSize);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_StudentTablePartial", paginatedStudents);
            }

            return View(paginatedStudents);
        }

        // =======================================================
        // 1A. EXPORT EXCEL
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.ExportExcel)]
        public async Task<IActionResult> ExportExcel(string? searchString, string? facultyId, string? sortOrder)
        {
            var students = await GetFilteredAndSortedStudentsAsync(searchString, facultyId, sortOrder);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachSinhVien");

            var headers = new[] { "STT", "Mã Sinh Viên", "Họ và Tên", "Số Điện Thoại", "Mã Khoa", "Tên Khoa" };
            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (var row = 0; row < students.Count; row++)
            {
                var student = students[row].Item;
                int currentRow = row + 2;

                worksheet.Cell(currentRow, 1).Value = students[row].Stt;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "@";
                worksheet.Cell(currentRow, 2).SetValue(student.StudentId ?? string.Empty);
                worksheet.Cell(currentRow, 3).SetValue(student.FullName ?? string.Empty);
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "@";
                worksheet.Cell(currentRow, 4).SetValue(student.PhoneNumber ?? string.Empty);
                worksheet.Cell(currentRow, 5).SetValue(student.FacultyId ?? string.Empty);
                worksheet.Cell(currentRow, 6).SetValue(student.Faculty?.FacultyName ?? "Chưa phân khoa");
            }

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
        // 1B. IMPORT EXCEL (2 BƯỚC: XEM TRƯỚC & XÁC NHẬN GHI ĐÈ / GHI BỔ SUNG)
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.ImportExcel)]
        public IActionResult ImportExcel()
        {
            if (Request.IsAjaxRequest())
            {
                return PartialView("_ImportExcelModal");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Student.ImportExcel)]
        public async Task<IActionResult> PreviewImportExcel(IFormFile? excelFile)
        {
            var (preview, _) = await ParseStudentsExcelAsync(excelFile);
            return Json(preview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Student.ImportExcel)]
        public async Task<IActionResult> ImportExcel(IFormFile? excelFile, string? importMode)
        {
            var normalizedMode = importMode?.Trim().ToLowerInvariant();
            if (normalizedMode != "append" && normalizedMode != "overwrite")
            {
                const string modeError = "Vui lòng chọn chế độ nhập dữ liệu: 'Chỉ ghi bổ sung' hoặc 'Ghi đè dữ liệu trùng & thêm mới'.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = modeError });
                }
                TempData["ErrorMessage"] = modeError;
                return RedirectToAction(nameof(Index));
            }

            var (preview, parsedRows) = await ParseStudentsExcelAsync(excelFile);
            if (!preview.Success)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = preview.ErrorMessage });
                }
                TempData["ErrorMessage"] = preview.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            if (parsedRows.Count == 0)
            {
                const string emptyMsg = "Không có dòng dữ liệu hợp lệ nào để nhập vào hệ thống.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = emptyMsg });
                }
                TempData["ErrorMessage"] = emptyMsg;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existingStudents = await _context.Student.ToDictionaryAsync(
                    s => s.StudentId!,
                    s => s,
                    StringComparer.OrdinalIgnoreCase);

                int addedCount = 0;
                int updatedCount = 0;
                int skippedDuplicateCount = 0;

                foreach (var row in parsedRows)
                {
                    if (!existingStudents.TryGetValue(row.StudentId, out var existing))
                    {
                        var newStudent = new Student
                        {
                            StudentId = row.StudentId,
                            FullName = row.FullName,
                            PhoneNumber = row.PhoneNumber,
                            FacultyId = row.FacultyId
                        };
                        _context.Student.Add(newStudent);
                        existingStudents[row.StudentId] = newStudent;
                        addedCount++;
                    }
                    else if (normalizedMode == "overwrite")
                    {
                        existing.FullName = row.FullName;
                        existing.PhoneNumber = row.PhoneNumber;
                        existing.FacultyId = row.FacultyId;
                        updatedCount++;
                    }
                    else
                    {
                        skippedDuplicateCount++;
                    }
                }

                await _context.SaveChangesAsync();

                var modeLabel = normalizedMode == "overwrite" ? "Ghi đè & thêm mới" : "Chỉ ghi bổ sung";
                var detailParts = new List<string> { $"Thêm mới {addedCount} sinh viên" };
                if (normalizedMode == "overwrite")
                {
                    detailParts.Add($"Ghi đè cập nhật {updatedCount} sinh viên trùng mã");
                }
                else
                {
                    detailParts.Add($"Giữ nguyên/bỏ qua {skippedDuplicateCount} sinh viên trùng mã");
                }
                if (preview.ErrorCount > 0)
                {
                    detailParts.Add($"Bỏ qua {preview.ErrorCount} dòng không hợp lệ");
                }

                var message = $"Nhập Excel ({modeLabel}) thành công: {string.Join(", ", detailParts)}.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message });
                }

                TempData["SuccessMessage"] = message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var errMsg = $"Lỗi khi lưu dữ liệu Excel: {ex.Message}";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = errMsg });
                }
                TempData["ErrorMessage"] = errMsg;
                return RedirectToAction(nameof(Index));
            }
        }

        private sealed record ParsedStudentRow(string StudentId, string FullName, string PhoneNumber, string FacultyId);

        private async Task<(ExcelImportPreviewVM Preview, List<ParsedStudentRow> ValidRows)> ParseStudentsExcelAsync(IFormFile? excelFile)
        {
            var preview = new ExcelImportPreviewVM();
            var validRows = new List<ParsedStudentRow>();

            if (excelFile == null || excelFile.Length == 0)
            {
                preview.Success = false;
                preview.ErrorMessage = "Vui lòng chọn một file Excel (.xlsx) để tải lên.";
                return (preview, validRows);
            }

            var extension = Path.GetExtension(excelFile.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                preview.Success = false;
                preview.ErrorMessage = "Định dạng file không hợp lệ. Hệ thống chỉ hỗ trợ file Excel (.xlsx).";
                return (preview, validRows);
            }

            try
            {
                var faculties = await _context.Faculty.AsNoTracking().ToListAsync();
                var facultyById = faculties.ToDictionary(f => f.FacultyId.ToUpperInvariant(), f => f.FacultyId);
                var facultyNameById = faculties.ToDictionary(f => f.FacultyId, f => f.FacultyName, StringComparer.OrdinalIgnoreCase);
                var facultyByCode = faculties.ToDictionary(f => f.FacultyCode, f => f.FacultyId);
                var facultyByName = faculties.ToDictionary(f => f.FacultyName.Trim(), f => f.FacultyId, StringComparer.OrdinalIgnoreCase);
                var facultyByUnaccentName = faculties.ToDictionary(
                    f => VietnameseTextHelper.RemoveDiacritics(f.FacultyName.Trim()),
                    f => f.FacultyId,
                    StringComparer.OrdinalIgnoreCase);

                var existingStudentIds = (await _context.Student
                    .AsNoTracking()
                    .Where(s => s.StudentId != null)
                    .Select(s => s.StudentId!)
                    .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var seenIdsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using var stream = new MemoryStream();
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                if (!workbook.Worksheets.Any())
                {
                    preview.Success = false;
                    preview.ErrorMessage = "File Excel không có trang tính (worksheet) nào.";
                    return (preview, validRows);
                }

                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1);

                var autoStudentIdRegex = new Regex(@"^2\d{7,11}$", RegexOptions.Compiled);
                var autoPhoneRegex = new Regex(@"^0\d{8,10}$", RegexOptions.Compiled);
                var autoPhoneMissingZeroRegex = new Regex(@"^[13-9]\d{8}$", RegexOptions.Compiled);
                var shortIntRegex = new Regex(@"^[1-9]\d{0,3}$", RegexOptions.Compiled);

                foreach (var row in rows)
                {
                    int rowNumber = row.RowNumber();
                    var usedCells = row.CellsUsed()
                        .Select(c => c.GetFormattedString().Trim())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();

                    if (usedCells.Count == 0)
                    {
                        continue;
                    }

                    preview.TotalDataRows++;

                    string? studentId = null;
                    string? fullName = null;
                    string? phoneNumber = null;
                    string? resolvedFacultyId = null;
                    string? rawFacultyText = null;
                    var textCandidates = new List<string>();

                    // Tự động phân loại từng ô không rỗng trên dòng theo quy luật định dạng (không phụ thuộc thứ tự cột)
                    foreach (var cellVal in usedCells)
                    {
                        var compactDigits = cellVal.Replace(" ", "").Replace(".", "").Replace("-", "");

                        // 1. Mã sinh viên: chuỗi toàn chữ số bắt đầu bằng '2', độ dài 8–12 (chuẩn HUMG 10 số)
                        if (studentId == null && autoStudentIdRegex.IsMatch(compactDigits))
                        {
                            studentId = compactDigits;
                            continue;
                        }

                        // 2. Số điện thoại: toàn chữ số bắt đầu bằng '0', 9–11 số (hoặc 9 số bị Excel cắt mất số 0 đầu)
                        if (phoneNumber == null && autoPhoneRegex.IsMatch(compactDigits))
                        {
                            phoneNumber = compactDigits;
                            continue;
                        }
                        if (phoneNumber == null && autoPhoneMissingZeroRegex.IsMatch(compactDigits))
                        {
                            phoneNumber = "0" + compactDigits;
                            continue;
                        }

                        // 3. Mã Khoa (CNTT, MO, TDBD...) hoặc Mã số ngành 2 chữ số (01..09)
                        var upperVal = cellVal.ToUpperInvariant();
                        if (facultyById.TryGetValue(upperVal, out var matchedById))
                        {
                            resolvedFacultyId ??= matchedById;
                            rawFacultyText ??= cellVal;
                            continue;
                        }
                        if (cellVal.Length == 2 && facultyByCode.TryGetValue(cellVal, out var matchedByCode))
                        {
                            resolvedFacultyId ??= matchedByCode;
                            rawFacultyText ??= cellVal;
                            continue;
                        }

                        // 4. Tên Khoa (khớp tên khoa trong CSDL, kể cả không dấu)
                        if (facultyByName.TryGetValue(cellVal, out var matchedByName))
                        {
                            resolvedFacultyId ??= matchedByName;
                            rawFacultyText ??= cellVal;
                            continue;
                        }
                        var unaccentCell = VietnameseTextHelper.RemoveDiacritics(cellVal);
                        if (facultyByUnaccentName.TryGetValue(unaccentCell, out var matchedByUnaccentName))
                        {
                            resolvedFacultyId ??= matchedByUnaccentName;
                            rawFacultyText ??= cellVal;
                            continue;
                        }

                        // 5. STT: số nguyên ngắn (1–4 chữ số), không phải Mã SV và không bắt đầu bằng '0'
                        if (shortIntRegex.IsMatch(cellVal))
                        {
                            continue;
                        }

                        // 6. Họ và tên: chuỗi chứa chữ cái, không thuần số và không trùng Mã/Tên khoa
                        if (cellVal.Any(char.IsLetter) && !cellVal.All(char.IsDigit))
                        {
                            textCandidates.Add(cellVal);
                        }
                    }

                    // Ưu tiên chọn chuỗi có nhiều từ (họ + tên) nhất làm Họ và tên
                    if (textCandidates.Count > 0)
                    {
                        fullName = textCandidates
                            .OrderByDescending(t => t.Contains(' '))
                            .ThenByDescending(t => t.Length)
                            .First();
                    }

                    // Suy luận bổ sung Khoa từ 2 chữ số ngành (vị trí index 4, độ dài 2) của Mã SV 10 số
                    if (string.IsNullOrEmpty(resolvedFacultyId) && !string.IsNullOrEmpty(studentId) && StudentIdRegex.IsMatch(studentId))
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

                    var facultyDisplay = !string.IsNullOrEmpty(resolvedFacultyId) && facultyNameById.TryGetValue(resolvedFacultyId, out var fName)
                        ? $"{resolvedFacultyId} · {fName}"
                        : (rawFacultyText ?? string.Empty);

                    // Kiểm tra tính hợp lệ của từng trường đã nhận diện
                    var rowIssues = new List<string>();
                    if (string.IsNullOrWhiteSpace(studentId) || studentId.Length < 2 || studentId.Length > 20)
                    {
                        rowIssues.Add("Thiếu/sai Mã SV (số bắt đầu bằng '2')");
                    }
                    if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
                    {
                        rowIssues.Add("Thiếu/sai Họ và tên");
                    }
                    if (string.IsNullOrWhiteSpace(phoneNumber) || !PhoneRegex.IsMatch(phoneNumber))
                    {
                        rowIssues.Add($"Thiếu/sai SĐT ({phoneNumber ?? "trống"}, cần 10–11 số bắt đầu bằng 0)");
                    }
                    if (string.IsNullOrEmpty(resolvedFacultyId))
                    {
                        rowIssues.Add($"Không nhận diện được Khoa{(string.IsNullOrEmpty(rawFacultyText) ? "" : $" ('{rawFacultyText}')")}");
                    }
                    if (rowIssues.Count == 0 && !seenIdsInFile.Add(studentId!))
                    {
                        rowIssues.Add($"Mã SV '{studentId}' bị trùng lặp trong cùng file Excel");
                    }

                    if (rowIssues.Count > 0)
                    {
                        var issueText = string.Join("; ", rowIssues);
                        AddSampleError(preview, $"Dòng {rowNumber}: {issueText}.");
                        AddPreviewRow(preview, new ExcelPreviewRowVM
                        {
                            RowNumber = rowNumber,
                            PrimaryKey = studentId,
                            Col2 = fullName,
                            Col3 = phoneNumber,
                            Col4 = facultyDisplay,
                            Status = "Lỗi",
                            WarningMessage = issueText
                        });
                        continue;
                    }

                    validRows.Add(new ParsedStudentRow(studentId!, fullName!, phoneNumber!, resolvedFacultyId!));
                    preview.ValidRows++;

                    bool isDup = existingStudentIds.Contains(studentId!);
                    if (isDup)
                    {
                        preview.DuplicateCount++;
                        if (preview.SampleDuplicateIds.Count < 10)
                        {
                            preview.SampleDuplicateIds.Add($"{studentId} ({fullName})");
                        }
                    }
                    else
                    {
                        preview.NewCount++;
                    }

                    AddPreviewRow(preview, new ExcelPreviewRowVM
                    {
                        RowNumber = rowNumber,
                        PrimaryKey = studentId,
                        Col2 = fullName,
                        Col3 = phoneNumber,
                        Col4 = facultyDisplay,
                        Status = isDup ? "Trùng mã" : "Mới",
                        WarningMessage = isDup ? "Đã tồn tại trong CSDL" : null
                    });
                }

                preview.Success = true;
            }
            catch (Exception ex)
            {
                preview.Success = false;
                preview.ErrorMessage = $"Không thể đọc file Excel: {ex.Message}";
            }

            return (preview, validRows);
        }

        private static void AddSampleError(ExcelImportPreviewVM preview, string message)
        {
            preview.ErrorCount++;
            if (preview.SampleErrors.Count < 10)
            {
                preview.SampleErrors.Add(message);
            }
        }

        private static void AddPreviewRow(ExcelImportPreviewVM preview, ExcelPreviewRowVM row)
        {
            if (preview.PreviewRows.Count < 100)
            {
                preview.PreviewRows.Add(row);
            }
        }

        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.ImportExcel)]
        public async Task<IActionResult> DownloadSampleExcel()
        {
            using var workbook = await BuildSample100StudentsWorkbookAsync(_context);

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
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "@";
                worksheet.Cell(currentRow, 2).SetValue(studentId);
                worksheet.Cell(currentRow, 3).SetValue(fullName);
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "@";
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

        private async Task<List<SttRowVM<Student>>> GetFilteredAndSortedStudentsAsync(string? searchString, string? facultyId, string? sortOrder)
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

            var list = await query.ToListAsync();

            var sortedList = sortOrder switch
            {
                "name_desc" => list
                    .OrderByDescending(s => VietnameseTextHelper.GetGivenName(s.FullName), VietnameseTextHelper.VietnameseComparer)
                    .ThenByDescending(s => s.FullName ?? string.Empty, VietnameseTextHelper.VietnameseComparer)
                    .ToList(),
                "faculty_asc" => list
                    .OrderBy(s => s.Faculty?.FacultyName ?? string.Empty, VietnameseTextHelper.VietnameseComparer)
                    .ThenBy(s => VietnameseTextHelper.GetGivenName(s.FullName), VietnameseTextHelper.VietnameseComparer)
                    .ToList(),
                "faculty_desc" => list
                    .OrderByDescending(s => s.Faculty?.FacultyName ?? string.Empty, VietnameseTextHelper.VietnameseComparer)
                    .ThenBy(s => VietnameseTextHelper.GetGivenName(s.FullName), VietnameseTextHelper.VietnameseComparer)
                    .ToList(),
                "id_desc" => list.OrderByDescending(s => s.StudentId, StringComparer.OrdinalIgnoreCase).ToList(),
                "id_asc" => list.OrderBy(s => s.StudentId, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => list
                    .OrderBy(s => VietnameseTextHelper.GetGivenName(s.FullName), VietnameseTextHelper.VietnameseComparer)
                    .ThenBy(s => s.FullName ?? string.Empty, VietnameseTextHelper.VietnameseComparer)
                    .ToList()
            };

            // Gán số thứ tự STT liên tục 1..N trước khi lọc theo từ khóa & phân trang
            var indexedList = sortedList
                .Select((student, index) => new SttRowVM<Student>
                {
                    Stt = index + 1,
                    Item = student
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();
                bool hasStt = int.TryParse(search, out int searchStt) && searchStt > 0;

                indexedList = indexedList.Where(x =>
                    (hasStt && x.Stt == searchStt) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.FullName, search) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.StudentId, search) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.Faculty?.FacultyName, search) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.FacultyId, search) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.PhoneNumber, search))
                    .ToList();
            }

            return indexedList;
        }

        // =======================================================
        // 2. DETAILS
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.Details)]
        public async Task<IActionResult> Details(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var student = await _context.Student
                .Include(s => s.Faculty)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_DetailsModal", student);
            }

            return View(student);
        }

        // =======================================================
        // 3. API SINH MÃ TỰ ĐỘNG THEO KHOA
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.Create)]
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
        // 4. CREATE
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.Create)]
        public async Task<IActionResult> Create()
        {
            await PopulateFacultyDropDownListAsync();
            if (Request.IsAjaxRequest())
            {
                return PartialView("_CreateModal", new Student());
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Student.Create)]
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
                string majorCode;
                if (student.StudentId.Length >= 4 && student.StudentId[2] == '0' && await _context.Faculty.AnyAsync(f => f.FacultyCode == student.StudentId.Substring(2, 2)))
                {
                    majorCode = student.StudentId.Substring(2, 2);
                }
                else
                {
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

            if (!string.IsNullOrEmpty(student.StudentId) && await StudentExistsAsync(student.StudentId))
            {
                ModelState.AddModelError(nameof(Student.StudentId), "Mã sinh viên đã tồn tại trong hệ thống");
            }

            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();
                var successMsg = $"Đã thêm sinh viên '{student.FullName}' (Mã SV: {student.StudentId}) thành công.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message = successMsg });
                }

                TempData["SuccessMessage"] = successMsg;
                return RedirectToAction(nameof(Index));
            }

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            if (Request.IsAjaxRequest())
            {
                return PartialView("_CreateModal", student);
            }
            return View(student);
        }

        // =======================================================
        // 5. UPDATE
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.Edit)]
        public async Task<IActionResult> Edit(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var student = await _context.Student.FindAsync(id);
            if (student == null) return NotFound();

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            if (Request.IsAjaxRequest())
            {
                return PartialView("_EditModal", student);
            }
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Student.Edit)]
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
                    var successMsg = $"Đã cập nhật thông tin sinh viên '{student.FullName}' (Mã SV: {student.StudentId}).";

                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = true, message = successMsg });
                    }

                    TempData["SuccessMessage"] = successMsg;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await StudentExistsAsync(student.StudentId!)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateFacultyDropDownListAsync(student.FacultyId);
            if (Request.IsAjaxRequest())
            {
                return PartialView("_EditModal", student);
            }
            return View(student);
        }

        // =======================================================
        // 6. DELETE
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Student.Delete)]
        public async Task<IActionResult> Delete(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var student = await _context.Student
                .Include(s => s.Faculty)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_DeleteModal", student);
            }
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Student.Delete)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student == null)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Không tìm thấy sinh viên cần xóa." });
                }
                return RedirectToAction(nameof(Index));
            }

            _context.Student.Remove(student);
            await _context.SaveChangesAsync();
            var successMsg = $"Đã xóa sinh viên '{student.FullName}' (Mã SV: {student.StudentId}) khỏi hệ thống.";

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = successMsg });
            }

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // HÀM HỖ TRỢ NGHIỆP VỤ (HELPER METHODS)
        // =======================================================
        private static readonly Regex StudentIdRegex = new(@"^\d{10}$", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"^0\d{9,10}$", RegexOptions.Compiled);
        private static readonly string[] DefaultPhonePrefixes = { "091", "094", "096", "097", "098", "032", "033", "034", "035", "036", "037", "038", "039", "086", "090", "093" };
        private const string DefaultYear = "21";
        private const string DefaultSystem = "21";

        public static string GenerateDefaultPhoneNumber(string? seed)
        {
            int hash = Math.Abs((seed ?? "0").GetHashCode());
            string prefix = DefaultPhonePrefixes[hash % DefaultPhonePrefixes.Length];
            int tail = 1000000 + (hash % 9000000);
            return $"{prefix}{tail:D7}";
        }

        public static async Task FixInvalidPhoneNumbersAsync(ApplicationDbContext context)
        {
            // Kiểm tra nhanh bằng AsNoTracking trước; chỉ load tracked entities khi thực sự có bản ghi cần chuẩn hóa
            var snapshot = await context.Student
                .AsNoTracking()
                .Select(s => new { s.StudentId, s.PhoneNumber })
                .ToListAsync();

            var idsNeedingFix = new List<string>();
            foreach (var item in snapshot)
            {
                var raw = item.PhoneNumber?.Trim() ?? string.Empty;
                var digits = new string(raw.Where(char.IsDigit).ToArray());
                if (digits.Length == 9) digits = "0" + digits;

                if (!PhoneRegex.IsMatch(digits) || item.PhoneNumber != digits)
                {
                    if (!string.IsNullOrEmpty(item.StudentId))
                    {
                        idsNeedingFix.Add(item.StudentId);
                    }
                }
            }

            if (idsNeedingFix.Count == 0) return;

            var studentsToFix = await context.Student
                .Where(s => s.StudentId != null && idsNeedingFix.Contains(s.StudentId))
                .ToListAsync();

            foreach (var s in studentsToFix)
            {
                var raw = s.PhoneNumber?.Trim() ?? string.Empty;
                var digits = new string(raw.Where(char.IsDigit).ToArray());
                if (digits.Length == 9) digits = "0" + digits;

                s.PhoneNumber = PhoneRegex.IsMatch(digits)
                    ? digits
                    : GenerateDefaultPhoneNumber(s.StudentId);
            }

            await context.SaveChangesAsync();
        }

        private async Task<string> GenerateStudentIdAsync(string facultyId)
        {
            var faculty = await _context.Faculty.AsNoTracking().FirstOrDefaultAsync(f => f.FacultyId == facultyId);
            if (faculty == null) return string.Empty;

            var existingIds = (await _context.Student
                .Where(s => s.StudentId != null && s.StudentId.Length == 10)
                .Select(s => s.StudentId!)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    prefix = latest.Substring(0, 4);
                    next = int.Parse(latest.Substring(4)) + 1;
                    while (next <= 999999 && existingIds.Contains($"{prefix}{next:D6}")) next++;
                    return next > 999999 ? string.Empty : $"{prefix}{next:D6}";
                }
                else
                {
                    prefix = latest.Substring(0, 6);
                    next = int.Parse(latest.Substring(6)) + 1;
                }
            }
            else
            {
                prefix = $"{DefaultYear}{DefaultSystem}{faculty.FacultyCode}";
                next = 1;
            }

            while (next <= 9999 && existingIds.Contains($"{prefix}{next:D4}")) next++;
            return next > 9999 ? string.Empty : $"{prefix}{next:D4}";
        }

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
