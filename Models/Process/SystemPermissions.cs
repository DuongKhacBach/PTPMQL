namespace DemoMVC.Models.Process
{
    public record PermissionDefinition(
        string Value,
        string Group,
        string DisplayName,
        string Description);

    public static class SystemPermissions
    {
        public const string ClaimType = "Permission";

        public static class Student
        {
            public const string Index = "Student.Index";
            public const string Details = "Student.Details";
            public const string Create = "Student.Create";
            public const string Edit = "Student.Edit";
            public const string Delete = "Student.Delete";
            public const string ExportExcel = "Student.ExportExcel";
            public const string ImportExcel = "Student.ImportExcel";
        }

        public static class Faculty
        {
            public const string Index = "Faculty.Index";
            public const string Details = "Faculty.Details";
            public const string Create = "Faculty.Create";
            public const string Edit = "Faculty.Edit";
            public const string Delete = "Faculty.Delete";
            public const string ExportExcel = "Faculty.ExportExcel";
            public const string ImportExcel = "Faculty.ImportExcel";
        }

        public static class Role
        {
            public const string Index = "Role.Index";
            public const string Create = "Role.Create";
            public const string Edit = "Role.Edit";
            public const string Delete = "Role.Delete";
            public const string AssignClaim = "Role.AssignClaim";
        }

        public static class Account
        {
            public const string Index = "Account.Index";
            public const string AssignRole = "Account.AssignRole";
            public const string Approve = "Account.Approve";
            public const string Delete = "Account.Delete";
        }

        private static readonly IReadOnlyList<PermissionDefinition> AllPermissions = new List<PermissionDefinition>
        {
            // Quản lý Sinh viên
            new(Student.Index, "Quản lý Sinh viên", "Xem danh sách Sinh viên", "Truy cập trang danh sách, tìm kiếm, lọc và sắp xếp sinh viên"),
            new(Student.Details, "Quản lý Sinh viên", "Xem chi tiết Sinh viên", "Xem thông tin hồ sơ chi tiết của từng sinh viên"),
            new(Student.Create, "Quản lý Sinh viên", "Thêm mới Sinh viên", "Tạo mới hồ sơ sinh viên và tự động sinh mã sinh viên"),
            new(Student.Edit, "Quản lý Sinh viên", "Chỉnh sửa Sinh viên", "Cập nhật thông tin sinh viên hiện có"),
            new(Student.Delete, "Quản lý Sinh viên", "Xóa Sinh viên", "Xóa bản ghi sinh viên khỏi hệ thống"),
            new(Student.ExportExcel, "Quản lý Sinh viên", "Xuất Excel Sinh viên", "Xuất danh sách sinh viên ra tệp Excel (.xlsx)"),
            new(Student.ImportExcel, "Quản lý Sinh viên", "Nhập Excel Sinh viên", "Tải tệp mẫu và nhập dữ liệu sinh viên từ tệp Excel (.xlsx)"),

            // Quản lý Khoa
            new(Faculty.Index, "Quản lý Khoa", "Xem danh sách Khoa", "Truy cập trang danh sách, tìm kiếm và phân trang các Khoa"),
            new(Faculty.Details, "Quản lý Khoa", "Xem chi tiết Khoa", "Xem thông tin chi tiết và số lượng sinh viên thuộc Khoa"),
            new(Faculty.Create, "Quản lý Khoa", "Thêm mới Khoa", "Tạo mới Khoa đào tạo và mã ngành 2 chữ số"),
            new(Faculty.Edit, "Quản lý Khoa", "Chỉnh sửa Khoa", "Cập nhật tên Khoa và mã số ngành"),
            new(Faculty.Delete, "Quản lý Khoa", "Xóa Khoa", "Xóa Khoa không có sinh viên trực thuộc"),
            new(Faculty.ExportExcel, "Quản lý Khoa", "Xuất Excel Khoa", "Xuất danh sách Khoa và thống kê số lượng sinh viên ra tệp Excel (.xlsx)"),
            new(Faculty.ImportExcel, "Quản lý Khoa", "Nhập Excel Khoa", "Tải tệp mẫu và nhập danh sách Khoa từ tệp Excel (.xlsx)"),

            // Quản lý Role
            new(Role.Index, "Quản lý Role", "Xem danh sách Role", "Xem danh sách vai trò, số quyền và số tài khoản thuộc vai trò"),
            new(Role.Create, "Quản lý Role", "Thêm mới Role", "Tạo vai trò (nhóm quyền) mới"),
            new(Role.Edit, "Quản lý Role", "Chỉnh sửa Role", "Đổi tên vai trò hiện có"),
            new(Role.Delete, "Quản lý Role", "Xóa Role", "Xóa vai trò khỏi hệ thống"),
            new(Role.AssignClaim, "Quản lý Role", "Phân quyền động (Claims)", "Tích chọn/gỡ bỏ quyền chi tiết theo từng Action cho Role"),

            // Quản lý Tài khoản
            new(Account.Index, "Quản lý Tài khoản", "Xem danh sách Tài khoản", "Xem, tìm kiếm và lọc danh sách tài khoản người dùng"),
            new(Account.AssignRole, "Quản lý Tài khoản", "Gán Role cho Tài khoản", "Gán hoặc gỡ bỏ vai trò (Role) của tài khoản người dùng"),
            new(Account.Approve, "Quản lý Tài khoản", "Duyệt tài khoản Giảng viên", "Phê duyệt tư cách và gán Role Giảng viên cho tài khoản đăng ký mới"),
            new(Account.Delete, "Quản lý Tài khoản", "Xóa tài khoản Sinh viên", "Xóa tài khoản sinh viên hoặc tài khoản chưa duyệt (bảo vệ Admin/Giảng viên)")
        };

        public static IReadOnlyList<PermissionDefinition> GetAll() => AllPermissions;

        public static IReadOnlyList<string> GetDefaultPermissionsForRole(string roleName)
        {
            return roleName switch
            {
                "Admin" => AllPermissions.Select(p => p.Value).ToList(),
                "GiangVien" => new List<string>
                {
                    Student.Index,
                    Student.Details,
                    Student.Create,
                    Student.Edit,
                    Student.ExportExcel,
                    Student.ImportExcel,
                    Faculty.Index,
                    Faculty.Details,
                    Faculty.ExportExcel
                },
                "SinhVien" => new List<string>
                {
                    Student.Index,
                    Student.Details,
                    Faculty.Index,
                    Faculty.Details
                },
                _ => new List<string>()
            };
        }
    }
}
