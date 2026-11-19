namespace DemoMVC.Models.ViewModels
{
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
    }
}
