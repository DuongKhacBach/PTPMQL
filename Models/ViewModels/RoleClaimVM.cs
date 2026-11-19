namespace DemoMVC.Models.ViewModels
{
    public class RoleClaimItemVM
    {
        public string Type { get; set; } = "Permission";
        public string Value { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public class RoleClaimVM
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<RoleClaimItemVM> Claims { get; set; } = new();
    }
}
