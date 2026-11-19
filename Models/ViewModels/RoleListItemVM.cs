namespace DemoMVC.Models.ViewModels
{
    public class RoleListItemVM
    {
        public int Stt { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ClaimCount { get; set; }
        public int UserCount { get; set; }
    }
}
