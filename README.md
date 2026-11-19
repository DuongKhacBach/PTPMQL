# DemoMVC — Hệ thống Quản lý Sinh viên & Khoa (HUMG)

Dự án thực hành môn **Phát triển Phần mềm Quản lý** xây dựng bằng **ASP.NET Core MVC (.NET 10.0)**, tích hợp **Entity Framework Core (SQLite)**, **ASP.NET Core Identity**, **MailKit**, **ClosedXML** và **Bootstrap 5 Modal + AJAX**, triển khai CI/CD tự động qua GitHub Actions lên MonsterASP.NET.

---

## Công nghệ sử dụng

- **Framework:** `.NET 10.0` (ASP.NET Core MVC & Razor Pages)
- **CSDL & ORM:** SQLite (`App.db`) + Entity Framework Core `10.0.12` (Code-First Migrations)
- **Xác thực & Phân quyền:** ASP.NET Core Identity `10.0.12` + Policy-based Dynamic Permission Claims (`AspNetRoleClaims`)
- **Email SMTP:** `MailKit` `5.0.0` (`StartTls`, tự động lưu `.eml` dự phòng vào `mailsSave/` khi chưa có cấu hình SMTP thật)
- **Excel:** `ClosedXML` `0.105.1` (Nhập 2 bước kèm xác nhận ghi đè/ghi bổ sung & Xuất `.xlsx`)
- **Giao diện:** Bootstrap 5 + Bootstrap Icons + jQuery Unobtrusive Validation + Fetch API AJAX

---

## Chức năng chính (Hoàn thiện STT 1–15)

1. **Quản lý Sinh viên (`StudentController`) & Khoa (`FacultyController`)**
   - Đầy đủ CRUD trên **Bootstrap 5 Modal + AJAX** không tải lại trang (`_CreateModal`, `_EditModal`, `_DetailsModal`, `_DeleteModal`).
   - Tự động sinh mã sinh viên 10 số chuẩn HUMG theo Khoa (`GetNextStudentId`), ô nhập nhanh mã Khoa, tự nhận diện Khoa khi gõ Mã SV và khóa chỉ nhập chữ số cho SĐT.
   - Tìm kiếm, lọc theo Khoa, sắp xếp tên tiếng Việt chuẩn (`CultureInfo("vi-VN")` theo tên chính) và phân trang AJAX (`PaginatedList<T>`).
2. **Xác thực & Email (`Areas/Identity/Pages/Account/`)**
   - Đăng nhập (khóa tạm 5 phút sau 5 lần sai -> `Lockout`), Đăng ký (gán mặc định Role `SinhVien` và gửi email xác nhận -> `RegisterConfirmation`, `ConfirmEmail`), Gửi lại email xác nhận (`ResendEmailConfirmation`), Quên mật khẩu & Đặt lại mật khẩu (`ForgotPassword`, `ResetPassword`).
   - Seed sẵn 3 tài khoản mẫu (`Admin`, `GiangVien`, `SinhVien`) đã xác thực email. Thông tin tài khoản mẫu trên trang Login chỉ hiển thị ở môi trường `Development`.
3. **Phân quyền động theo Action (`RoleController` & `AccountController`)**
   - CRUD Role, xem số quyền (Claims) & số tài khoản thuộc Role, tìm kiếm & phân trang AJAX.
   - Gán nhiều Role cho tài khoản (`Account/AssignRole`), lọc theo Role, không truy vấn N+1.
   - Màn hình phân quyền động (`Role/AssignClaim`) gom nhóm **21 Permission Claims** theo từng Controller (`Student`, `Faculty`, `Role`, `Account`) áp dụng cho mọi Action bằng `[Authorize(Policy = ...)]` và ẩn/hiện menu/nút bấm động qua `IAuthorizationService`.
4. **Nhập / Xuất Excel cho toàn bộ bảng nghiệp vụ (`Students` & `Faculties`)**
   - Xuất file `.xlsx` theo đúng bộ lọc hiện tại cho cả Sinh viên và Khoa.
   - Tải file mẫu `.xlsx` chuẩn cho cả Sinh viên (100 SV HUMG) và Khoa.
   - **Nhập Excel 2 bước bằng AJAX:**
     - **Bước 1:** Phân tích và thống kê tổng dòng, hợp lệ, thêm mới, trùng khóa chính (liệt kê tối đa 10 mã trùng minh họa) và dòng lỗi.
     - **Bước 2:** Bắt buộc xác nhận chế độ **Chỉ ghi bổ sung (`append`)** hoặc **Ghi đè dữ liệu trùng & thêm mới (`overwrite`)**.

---

## Hướng dẫn chạy dự án

```powershell
# Khôi phục gói & build
dotnet build

# Chạy ứng dụng
dotnet run
```

### Cấu hình gửi Email Gmail thật (An toàn, không đẩy mật khẩu lên GitHub)

Không điền mật khẩu thật trực tiếp vào `appsettings.json`. Hãy dùng **.NET User Secrets** trên máy cá nhân:

```powershell
dotnet user-secrets init
dotnet user-secrets set "MailSettings:Mail" "ten-gmail-cua-ban@gmail.com"
dotnet user-secrets set "MailSettings:Password" "mat-khau-ung-dung-16-ky-tu"
```

Trên máy chủ Production (MonsterASP.NET / IIS), khai báo 2 biến môi trường:
- `MailSettings__Mail`
- `MailSettings__Password`
