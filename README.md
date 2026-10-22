# HƯỚNG DẪN CÀI ĐẶT VÀ CHẠY DỰ ÁN BACKEND APP ĐIỂM DANH SINH VIÊN

Dự án Backend API bằng **ASP.NET Core Web API (.NET 10)** cho Ứng dụng Di Động Điểm Danh Sinh Viên (Nhóm 9Bi).

---

## 1. Yêu cầu môi trường
- **.NET SDK 10** (hoặc Visual Studio 2022/2025 có hỗ trợ .NET 10 / .NET 8).
- **SQL Server** (SQL Express hoặc SQL Server Developer/Standard).

---

## 2. Cấu hình chuỗi kết nối Database
Mở file `Nhom9Bi_AppDiemDanhSV_S5/appsettings.json`, kiểm tra và chỉnh lại tên Server SQL Server theo máy của bạn (nếu khác):
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=Nhom9Bi_AppDiemDanhSV_S5;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```
*(Thay `.\\SQLEXPRESS` bằng tên instance SQL Server trên máy bạn, ví dụ: `localhost` hoặc `TENTHIETBI\\SQLEXPRESS`).*

---

## 3. Khởi tạo Database
Mở Terminal tại thư mục gốc và chạy lệnh:
```bash
dotnet ef database update --project Nhom9Bi_AppDiemDanhSV_S5
```
Hệ thống sẽ tự động tạo cơ sở dữ liệu `Nhom9Bi_AppDiemDanhSV_S5` và toàn bộ các bảng cần thiết.

---

## 4. Khởi chạy Backend
Chạy lệnh:
```bash
dotnet run --project Nhom9Bi_AppDiemDanhSV_S5
```
Ứng dụng sẽ tự động khởi tạo dữ liệu mẫu ban đầu (Tài khoản Admin, Giảng viên, Lớp học, Môn học).

- **Đường dẫn Swagger UI:**  
  `http://localhost:5003/swagger`  hoặc  `https://localhost:7290/swagger`

---

## 5. Tài khoản mẫu có sẵn

| Vai trò | Tên đăng nhập | Mật khẩu | Chức năng / Dashboard |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin` | `Admin@123456` | Quản trị toàn hệ thống (`AdminDashboard`) |
| **Giảng viên** | `lecturer` | `Lecturer@123456` | Quản lý buổi học, điểm danh, QR (`LecturerDashboard`) |
| **Sinh viên** | Đăng ký qua email `/register` hoặc Google `/oauth-login` | *(Tùy chọn)* | Điểm danh GPS, QR, Khuôn mặt (`StudentDashboard`) |

---

## 6. Các chức năng API chính đáp ứng đề bài
1. **Đăng ký tài khoản:**
   - Qua Email: `POST /api/Auth/register`
   - Qua bên thứ 3 (OAuth 2.0 / Google): `POST /api/Auth/oauth-login`
2. **Đăng nhập OTP (SMS OTP, Email OTP):**
   - Bước 1: `POST /api/Auth/login` $\rightarrow$ Nhận `debugOtp` và `tempToken`.
   - Bước 2: `POST /api/Auth/verify-otp` $\rightarrow$ Nhận JWT Bearer Token.
3. **Phân quyền & Điều hướng Dashboard:**
   - Endpoint `GET /api/Auth/me` trả về `targetDashboard` và `redirectUrl` (`AdminDashboard`, `LecturerDashboard`, `StudentDashboard`).
4. **CRUD đầy đủ:** Lớp học (Classes), Môn học (Courses), Giảng viên (Lecturers), Sinh viên (Students), Buổi học (ClassSessions), Bản ghi điểm danh (AttendanceRecords).
5. **3 Chức năng nâng cao chọn lọc:**
   - **(a) Đăng nhập khuôn mặt:** `POST /api/Auth/face-login` (nhận diện Cosine Similarity / Euclidean distance), đăng ký tại `POST /api/Auth/register-face`.
   - **(b) Chia sẻ vị trí GPS:** `PUT /api/ClassSessions/{id}/location` (giảng viên chia sẻ vị trí phòng học thời gian thực), `GET /api/AttendanceRecords/check-location` (kiểm tra khoảng cách), `POST /api/AttendanceRecords/gps` (điểm danh GPS).
   - **(e) Nhận diện QR Code:** `POST /api/ClassSessions/{id}/generate-qr` (sinh mã QR động ký HMAC-SHA256 có thời hạn), `POST /api/AttendanceRecords/scan-qr` (quét mã điểm danh).

---

## 7. Chạy kiểm thử tự động (Integration Test)
Bạn có thể chạy file kiểm thử tự động để kiểm tra nhanh toàn bộ các chức năng:
```powershell
powershell -ExecutionPolicy Bypass -File .\test_api.ps1
```
