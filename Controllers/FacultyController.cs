using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.Process;
using DemoMVC.Models.ViewModels;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class FacultyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly Regex FacultyCodeRegex = new(@"^\d{2}$", RegexOptions.Compiled);
        private static readonly Regex FacultyIdRegex = new(@"^[A-Z0-9_-]{1,10}$", RegexOptions.Compiled);

        public FacultyController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Faculty?searchString=...&pageNumber=1&pageSize=5
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.Index)]
        public async Task<IActionResult> Index(string? searchString, int? pageNumber, int? pageSize)
        {
            int currentPageSize = pageSize is > 0 and <= 100 ? pageSize.Value : 5;
            int currentPageNumber = pageNumber is > 0 ? pageNumber.Value : 1;

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentPageSize"] = currentPageSize;

            var faculties = await _context.Faculty
                .Include(f => f.Students)
                .AsNoTracking()
                .OrderBy(f => f.FacultyCode)
                .ToListAsync();

            var indexedFaculties = faculties
                .Select((f, index) => new SttRowVM<Faculty>
                {
                    Stt = index + 1,
                    Item = f
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                bool hasStt = int.TryParse(keyword, out int searchStt) && searchStt > 0;

                indexedFaculties = indexedFaculties.Where(x =>
                    (hasStt && x.Stt == searchStt) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.FacultyId, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.FacultyCode, keyword) ||
                    VietnameseTextHelper.ContainsMatch(x.Item.FacultyName, keyword))
                    .ToList();
            }

            var paginatedFaculties = PaginatedList<SttRowVM<Faculty>>.Create(indexedFaculties, currentPageNumber, currentPageSize);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_FacultyTablePartial", paginatedFaculties);
            }

            return View(paginatedFaculties);
        }

        // GET: /Faculty/Details/{id}
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.Details)]
        public async Task<IActionResult> Details(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FacultyId == id);

            if (faculty == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_DetailsModal", faculty);
            }

            return View(faculty);
        }

        // GET: /Faculty/Create
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.Create)]
        public IActionResult Create()
        {
            if (Request.IsAjaxRequest())
            {
                return PartialView("_CreateModal", new Faculty());
            }
            return View();
        }

        // POST: /Faculty/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Faculty.Create)]
        public async Task<IActionResult> Create([Bind("FacultyId,FacultyCode,FacultyName")] Faculty faculty)
        {
            faculty.FacultyId = faculty.FacultyId?.Trim().ToUpperInvariant() ?? string.Empty;
            faculty.FacultyCode = faculty.FacultyCode?.Trim() ?? string.Empty;
            faculty.FacultyName = faculty.FacultyName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(faculty.FacultyId))
            {
                ModelState.AddModelError(nameof(Faculty.FacultyId), "Vui lòng nhập Mã Khoa.");
            }
            else if (await _context.Faculty.AnyAsync(f => f.FacultyId == faculty.FacultyId))
            {
                ModelState.AddModelError(nameof(Faculty.FacultyId), "Mã Khoa đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(faculty.FacultyCode) &&
                await _context.Faculty.AnyAsync(f => f.FacultyCode == faculty.FacultyCode))
            {
                ModelState.AddModelError(nameof(Faculty.FacultyCode), "Mã số ngành 2 chữ số này đã được sử dụng.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(faculty);
                await _context.SaveChangesAsync();
                var successMsg = $"Đã thêm Khoa '{faculty.FacultyName}' ({faculty.FacultyId}) thành công.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message = successMsg });
                }

                TempData["SuccessMessage"] = successMsg;
                return RedirectToAction(nameof(Index));
            }

            if (Request.IsAjaxRequest())
            {
                return PartialView("_CreateModal", faculty);
            }
            return View(faculty);
        }

        // GET: /Faculty/Edit/{id}
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.Edit)]
        public async Task<IActionResult> Edit(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var faculty = await _context.Faculty.FindAsync(id);
            if (faculty == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_EditModal", faculty);
            }
            return View(faculty);
        }

        // POST: /Faculty/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Faculty.Edit)]
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
                    var successMsg = $"Đã cập nhật thông tin Khoa '{faculty.FacultyName}' ({faculty.FacultyId}).";

                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = true, message = successMsg });
                    }

                    TempData["SuccessMessage"] = successMsg;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Faculty.AnyAsync(e => e.FacultyId == faculty.FacultyId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            if (Request.IsAjaxRequest())
            {
                return PartialView("_EditModal", faculty);
            }
            return View(faculty);
        }

        // GET: /Faculty/Delete/{id}
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.Delete)]
        public async Task<IActionResult> Delete(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FacultyId == id);

            if (faculty == null) return NotFound();

            if (Request.IsAjaxRequest())
            {
                return PartialView("_DeleteModal", faculty);
            }
            return View(faculty);
        }

        // POST: /Faculty/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Faculty.Delete)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var faculty = await _context.Faculty
                .Include(f => f.Students)
                .FirstOrDefaultAsync(f => f.FacultyId == id);

            if (faculty == null)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Không tìm thấy Khoa cần xóa." });
                }
                return RedirectToAction(nameof(Index));
            }

            if (faculty.Students != null && faculty.Students.Any())
            {
                var errMsg = $"Không thể xóa khoa '{faculty.FacultyName}' vì đang có {faculty.Students.Count} sinh viên trực thuộc!";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = errMsg });
                }
                TempData["ErrorMessage"] = errMsg;
                return RedirectToAction(nameof(Index));
            }

            _context.Faculty.Remove(faculty);
            await _context.SaveChangesAsync();
            var successMsg = $"Đã xóa Khoa '{faculty.FacultyName}' ({faculty.FacultyId}) thành công.";

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = successMsg });
            }

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // EXPORT EXCEL KHOA
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.ExportExcel)]
        public async Task<IActionResult> ExportExcel(string? searchString)
        {
            var faculties = await _context.Faculty
                .Include(f => f.Students)
                .AsNoTracking()
                .OrderBy(f => f.FacultyCode)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                bool hasStt = int.TryParse(keyword, out int searchStt) && searchStt > 0;

                faculties = faculties
                    .Select((f, index) => new { Stt = index + 1, Item = f })
                    .Where(x =>
                        (hasStt && x.Stt == searchStt) ||
                        VietnameseTextHelper.ContainsMatch(x.Item.FacultyId, keyword) ||
                        VietnameseTextHelper.ContainsMatch(x.Item.FacultyCode, keyword) ||
                        VietnameseTextHelper.ContainsMatch(x.Item.FacultyName, keyword))
                    .Select(x => x.Item)
                    .ToList();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachKhoa");

            var headers = new[] { "STT", "Mã Khoa", "Mã số ngành (2 số)", "Tên Khoa", "Số lượng Sinh viên" };
            for (int col = 0; col < headers.Length; col++)
            {
                worksheet.Cell(1, col + 1).Value = headers[col];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (int i = 0; i < faculties.Count; i++)
            {
                var f = faculties[i];
                int row = i + 2;
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(row, 2).Style.NumberFormat.Format = "@";
                worksheet.Cell(row, 2).SetValue(f.FacultyId);
                worksheet.Cell(row, 3).Style.NumberFormat.Format = "@";
                worksheet.Cell(row, 3).SetValue(f.FacultyCode);
                worksheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(row, 4).SetValue(f.FacultyName);
                worksheet.Cell(row, 5).Value = f.Students?.Count ?? 0;
                worksheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int lastRow = Math.Max(1, faculties.Count + 1);
            var tableRange = worksheet.Range(1, 1, lastRow, headers.Length);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachKhoa_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // =======================================================
        // IMPORT EXCEL KHOA (STT 15) + DOWNLOAD FILE MẪU
        // =======================================================
        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.ImportExcel)]
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
        [Authorize(Policy = SystemPermissions.Faculty.ImportExcel)]
        public async Task<IActionResult> PreviewImportExcel(IFormFile? excelFile)
        {
            var (preview, _) = await ParseFacultiesExcelAsync(excelFile);
            return Json(preview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = SystemPermissions.Faculty.ImportExcel)]
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

            var (preview, parsedRows) = await ParseFacultiesExcelAsync(excelFile);
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
                const string emptyMsg = "Không có dòng dữ liệu Khoa hợp lệ nào để nhập.";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = emptyMsg });
                }
                TempData["ErrorMessage"] = emptyMsg;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existingFaculties = await _context.Faculty.ToDictionaryAsync(
                    f => f.FacultyId,
                    f => f,
                    StringComparer.OrdinalIgnoreCase);

                int addedCount = 0;
                int updatedCount = 0;
                int skippedDuplicateCount = 0;

                foreach (var row in parsedRows)
                {
                    if (!existingFaculties.TryGetValue(row.FacultyId, out var existing))
                    {
                        var newFaculty = new Faculty
                        {
                            FacultyId = row.FacultyId,
                            FacultyCode = row.FacultyCode,
                            FacultyName = row.FacultyName
                        };
                        _context.Faculty.Add(newFaculty);
                        existingFaculties[row.FacultyId] = newFaculty;
                        addedCount++;
                    }
                    else if (normalizedMode == "overwrite")
                    {
                        existing.FacultyCode = row.FacultyCode;
                        existing.FacultyName = row.FacultyName;
                        updatedCount++;
                    }
                    else
                    {
                        skippedDuplicateCount++;
                    }
                }

                await _context.SaveChangesAsync();

                var modeLabel = normalizedMode == "overwrite" ? "Ghi đè & thêm mới" : "Chỉ ghi bổ sung";
                var detailParts = new List<string> { $"Thêm mới {addedCount} Khoa" };
                if (normalizedMode == "overwrite")
                {
                    detailParts.Add($"Ghi đè cập nhật {updatedCount} Khoa trùng mã");
                }
                else
                {
                    detailParts.Add($"Giữ nguyên/bỏ qua {skippedDuplicateCount} Khoa trùng mã");
                }
                if (preview.ErrorCount > 0)
                {
                    detailParts.Add($"Bỏ qua {preview.ErrorCount} dòng không hợp lệ");
                }

                var message = $"Nhập Excel Khoa ({modeLabel}) thành công: {string.Join(", ", detailParts)}.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, message });
                }

                TempData["SuccessMessage"] = message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var errMsg = $"Lỗi khi lưu dữ liệu Khoa từ Excel: {ex.Message}";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = errMsg });
                }
                TempData["ErrorMessage"] = errMsg;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        [Authorize(Policy = SystemPermissions.Faculty.ImportExcel)]
        public async Task<IActionResult> DownloadSampleExcel()
        {
            var faculties = await _context.Faculty
                .OrderBy(f => f.FacultyCode)
                .AsNoTracking()
                .ToListAsync();

            var sampleRows = faculties
                .Select(f => (f.FacultyId, f.FacultyCode, f.FacultyName))
                .ToList();

            // Bổ sung 2 khoa minh họa chưa có trong DB để dễ kiểm thử chế độ ghi bổ sung & ghi đè
            if (!faculties.Any(f => f.FacultyId == "KHCB" || f.FacultyCode == "10"))
            {
                sampleRows.Add(("KHCB", "10", "Khoa học cơ bản"));
            }
            if (!faculties.Any(f => f.FacultyId == "NN" || f.FacultyCode == "11"))
            {
                sampleRows.Add(("NN", "11", "Ngoại ngữ"));
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachKhoa_HUMG");

            var headers = new[] { "STT", "Mã Khoa", "Mã số ngành (2 số)", "Tên Khoa" };
            for (int col = 0; col < headers.Length; col++)
            {
                worksheet.Cell(1, col + 1).Value = headers[col];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (int i = 0; i < sampleRows.Count; i++)
            {
                int currentRow = i + 2;
                worksheet.Cell(currentRow, 1).Value = i + 1;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "@";
                worksheet.Cell(currentRow, 2).SetValue(sampleRows[i].FacultyId);
                worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = "@";
                worksheet.Cell(currentRow, 3).SetValue(sampleRows[i].FacultyCode);
                worksheet.Cell(currentRow, 4).SetValue(sampleRows[i].FacultyName);
            }

            int lastRow = Math.Max(1, sampleRows.Count + 1);
            var tableRange = worksheet.Range(1, 1, lastRow, headers.Length);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Mau_DanhSach_Khoa_HUMG.xlsx");
        }

        private sealed record ParsedFacultyRow(string FacultyId, string FacultyCode, string FacultyName);

        private async Task<(ExcelImportPreviewVM Preview, List<ParsedFacultyRow> ValidRows)> ParseFacultiesExcelAsync(IFormFile? excelFile)
        {
            var preview = new ExcelImportPreviewVM();
            var validRows = new List<ParsedFacultyRow>();

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
                var existingFaculties = await _context.Faculty.AsNoTracking().ToListAsync();
                var existingIdSet = existingFaculties
                    .Select(f => f.FacultyId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingCodeToId = existingFaculties
                    .ToDictionary(f => f.FacultyCode, f => f.FacultyId, StringComparer.OrdinalIgnoreCase);

                var seenIdsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenCodeToIdInFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                var asciiCodeWithLetterRegex = new Regex(@"^(?=.*[A-Za-z])[A-Za-z0-9_-]{1,10}$", RegexOptions.Compiled);
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

                    string? facultyId = null;
                    string? facultyCode = null;
                    string? facultyName = null;
                    var twoDigitCandidates = new List<string>();
                    var idCandidates = new List<string>();
                    var nameCandidates = new List<string>();

                    // Tự động phân loại từng ô không rỗng trên dòng (không phụ thuộc thứ tự cột)
                    foreach (var cellVal in usedCells)
                    {
                        if (FacultyCodeRegex.IsMatch(cellVal))
                        {
                            twoDigitCandidates.Add(cellVal);
                            continue;
                        }

                        if (shortIntRegex.IsMatch(cellVal))
                        {
                            // STT ngắn (1-4 chữ số, không bắt đầu bằng 0 và khác 2 chữ số mã ngành)
                            continue;
                        }

                        if (asciiCodeWithLetterRegex.IsMatch(cellVal))
                        {
                            idCandidates.Add(cellVal.ToUpperInvariant());
                            continue;
                        }

                        if (cellVal.Any(char.IsLetter))
                        {
                            nameCandidates.Add(cellVal);
                        }
                    }

                    // Chọn FacultyCode (nếu có nhiều số 2 chữ số do STT >= 10 trùng, ưu tiên số đứng sau hoặc bắt đầu bằng '0')
                    if (twoDigitCandidates.Count == 1)
                    {
                        facultyCode = twoDigitCandidates[0];
                    }
                    else if (twoDigitCandidates.Count > 1)
                    {
                        var zeroPrefixed = twoDigitCandidates.FirstOrDefault(c => c.StartsWith('0'));
                        facultyCode = zeroPrefixed ?? twoDigitCandidates[^1];
                    }

                    // Chọn FacultyId & FacultyName
                    if (idCandidates.Count > 0)
                    {
                        // Nếu có cả 1 mã ngắn (CNTT, MO...) và có thể tên khoa không dấu, ưu tiên mã ngắn nhất / toàn chữ hoa làm FacultyId
                        var sortedIds = idCandidates
                            .OrderBy(c => c.Length)
                            .ThenBy(c => c, StringComparer.Ordinal)
                            .ToList();
                        facultyId = sortedIds[0];
                        for (int k = 1; k < sortedIds.Count; k++)
                        {
                            nameCandidates.Add(sortedIds[k]);
                        }
                    }

                    if (nameCandidates.Count > 0)
                    {
                        facultyName = nameCandidates
                            .OrderByDescending(n => n.Contains(' '))
                            .ThenByDescending(n => n.Length)
                            .First();
                    }

                    var rowIssues = new List<string>();
                    if (string.IsNullOrWhiteSpace(facultyId) || !FacultyIdRegex.IsMatch(facultyId))
                    {
                        rowIssues.Add($"Thiếu/sai Mã Khoa ('{facultyId ?? "trống"}', tối đa 10 ký tự chữ/số không dấu)");
                    }
                    if (string.IsNullOrWhiteSpace(facultyCode) || !FacultyCodeRegex.IsMatch(facultyCode))
                    {
                        rowIssues.Add($"Thiếu/sai Mã số ngành 2 chữ số ('{facultyCode ?? "trống"}')");
                    }
                    if (string.IsNullOrWhiteSpace(facultyName) || facultyName.Length > 150)
                    {
                        rowIssues.Add("Thiếu/sai Tên Khoa (tối đa 150 ký tự)");
                    }

                    if (rowIssues.Count == 0)
                    {
                        if (!seenIdsInFile.Add(facultyId!))
                        {
                            rowIssues.Add($"Mã Khoa '{facultyId}' bị trùng lặp trong cùng file Excel");
                        }
                        else if (seenCodeToIdInFile.TryGetValue(facultyCode!, out var otherInFile) &&
                            !string.Equals(otherInFile, facultyId, StringComparison.OrdinalIgnoreCase))
                        {
                            rowIssues.Add($"Mã số ngành '{facultyCode}' trùng với Khoa '{otherInFile}' trong file Excel");
                        }
                        else if (existingCodeToId.TryGetValue(facultyCode!, out var otherInDb) &&
                            !string.Equals(otherInDb, facultyId, StringComparison.OrdinalIgnoreCase))
                        {
                            rowIssues.Add($"Mã số ngành '{facultyCode}' đã thuộc về Khoa '{otherInDb}' trong CSDL");
                        }
                    }

                    if (rowIssues.Count > 0)
                    {
                        var issueText = string.Join("; ", rowIssues);
                        AddSampleError(preview, $"Dòng {rowNumber}: {issueText}.");
                        AddPreviewRow(preview, new ExcelPreviewRowVM
                        {
                            RowNumber = rowNumber,
                            PrimaryKey = facultyId,
                            Col2 = facultyCode,
                            Col3 = facultyName,
                            Status = "Lỗi",
                            WarningMessage = issueText
                        });
                        continue;
                    }

                    seenCodeToIdInFile[facultyCode!] = facultyId!;
                    validRows.Add(new ParsedFacultyRow(facultyId!, facultyCode!, facultyName!));
                    preview.ValidRows++;

                    bool isDup = existingIdSet.Contains(facultyId!);
                    if (isDup)
                    {
                        preview.DuplicateCount++;
                        if (preview.SampleDuplicateIds.Count < 10)
                        {
                            preview.SampleDuplicateIds.Add($"{facultyId} ({facultyName})");
                        }
                    }
                    else
                    {
                        preview.NewCount++;
                    }

                    AddPreviewRow(preview, new ExcelPreviewRowVM
                    {
                        RowNumber = rowNumber,
                        PrimaryKey = facultyId,
                        Col2 = facultyCode,
                        Col3 = facultyName,
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
    }
}
