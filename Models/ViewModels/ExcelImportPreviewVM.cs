namespace DemoMVC.Models.ViewModels
{
    public class ExcelPreviewRowVM
    {
        public int RowNumber { get; set; }
        public string? PrimaryKey { get; set; }
        public string? Col2 { get; set; }
        public string? Col3 { get; set; }
        public string? Col4 { get; set; }
        public string Status { get; set; } = string.Empty; // "Mới", "Trùng mã", "Lỗi"
        public string? WarningMessage { get; set; }
    }

    public class ExcelImportPreviewVM
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalDataRows { get; set; }
        public int ValidRows { get; set; }
        public int NewCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> SampleDuplicateIds { get; set; } = new();
        public List<string> SampleErrors { get; set; } = new();
        public List<ExcelPreviewRowVM> PreviewRows { get; set; } = new();
    }
}
