using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class Create_table_Student : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    StudentId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Faculty = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.StudentId);
                });

            // Chuyển dữ liệu cũ: Mã NV -> Mã SV, Họ tên giữ nguyên (Khoa/SĐT để cập nhật sau)
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""Employees"" (""EmployeeId"" TEXT NOT NULL PRIMARY KEY, ""FullName"" TEXT NOT NULL, ""Age"" INTEGER NOT NULL, ""Address"" TEXT NULL);");
            migrationBuilder.Sql(@"INSERT OR IGNORE INTO ""Students"" (""StudentId"", ""FullName"", ""Faculty"", ""PhoneNumber"")
                                   SELECT ""EmployeeId"", ""FullName"", 'Chưa cập nhật', NULL FROM ""Employees"";");

            // Xóa các bảng cũ không còn sử dụng (Nhân viên / Person)
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Employees"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Persons"";");
            migrationBuilder.Sql(@"DELETE FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" IN ('20260917072003_Create_table_Person', '20261001070653_Add_Employee');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
