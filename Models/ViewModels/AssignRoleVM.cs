namespace DemoMVC.Models.ViewModels
{
    public class RoleVM
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class AssignRoleVM
    {
        public string UserId { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string? FullName { get; set; }
        public IList<string> SelectedRoles { get; set; } = new List<string>();
        public IList<RoleVM>? AllRoles { get; set; } = new List<RoleVM>();
    }
}

