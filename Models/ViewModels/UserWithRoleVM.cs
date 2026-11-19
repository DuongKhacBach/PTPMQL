namespace DemoMVC.Models.ViewModels
{
    public class UserWithRoleVM
    {
        public int Stt { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public IList<string> Roles { get; set; } = new List<string>();
    }
}
