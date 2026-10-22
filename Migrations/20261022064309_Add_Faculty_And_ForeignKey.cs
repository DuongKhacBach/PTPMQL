using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class Add_Faculty_And_ForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Students");

            migrationBuilder.AddColumn<string>(
                name: "FacultyId",
                table: "Students",
                type: "TEXT",
                nullable: false,
                defaultValue: "CNTT");

            migrationBuilder.CreateTable(
                name: "Faculties",
                columns: table => new
                {
                    FacultyId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FacultyCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    FacultyName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faculties", x => x.FacultyId);
                });

            migrationBuilder.InsertData(
                table: "Faculties",
                columns: new[] { "FacultyId", "FacultyCode", "FacultyName" },
                values: new object[,]
                {
                    { "CD", "06", "Cơ - Điện" },
                    { "CNTT", "05", "Công nghệ thông tin" },
                    { "DC", "02", "Địa chất" },
                    { "DK", "03", "Dầu khí" },
                    { "KT", "04", "Kinh tế & Quản trị kinh doanh" },
                    { "MO", "01", "Mỏ" },
                    { "MT", "08", "Môi trường" },
                    { "TDBD", "09", "Trắc địa - Bản đồ và Quản lý đất đai" },
                    { "XD", "07", "Xây dựng" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_FacultyId",
                table: "Students",
                column: "FacultyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Faculties_FacultyId",
                table: "Students",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "FacultyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Faculties_FacultyId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Faculties");

            migrationBuilder.DropIndex(
                name: "IX_Students_FacultyId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FacultyId",
                table: "Students");

            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "Students",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
