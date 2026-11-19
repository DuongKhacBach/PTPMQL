namespace DemoMVC.Models.Process
{
    public class MailSettings
    {
        public string Mail { get; set; } = string.Empty;
        public string DisplayName { get; set; } = "DemoMVC HUMG";
        public string Password { get; set; } = string.Empty;
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
    }
}
