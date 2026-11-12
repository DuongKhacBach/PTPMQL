namespace DemoMVC.Models.ViewModels
{
    public class UserWithRoleVM
    {
        public ApplicationUser User { get; set; } = default!;
        public IList<string> Roles { get; set; } = new List<string>();
    }
}

