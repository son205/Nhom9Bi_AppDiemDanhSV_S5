using System.ComponentModel.DataAnnotations;

namespace Nhom9Bi_AppDiemDanhSV_S5.DTOs
{
    public class RegisterRequest
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
    }

    public class LoginRequest
    {
        [Required]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Phương thức nhận OTP: "Email" hoặc "SMS"
        /// </summary>
        public string OtpMethod { get; set; } = "Email";

        /// <summary>
        /// Mã OTP (nếu đã có hoặc nhập cùng lúc)
        /// </summary>
        public string? Otp { get; set; }
    }

    public class VerifyOtpRequest
    {
        [Required]
        public string TempToken { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Otp { get; set; } = string.Empty;
    }

    public class ResendOtpRequest
    {
        [Required]
        public string TempToken { get; set; } = string.Empty;

        public string OtpMethod { get; set; } = "Email";
    }

    public class OAuthLoginRequest
    {
        /// <summary>
        /// Provider name, ví dụ: "Google", "OAuth2", "Microsoft"
        /// </summary>
        [Required]
        public string Provider { get; set; } = "Google";

        /// <summary>
        /// OAuth2 ID Token hoặc Access Token nhận được từ client/mobile
        /// </summary>
        public string? IdToken { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? ProviderKey { get; set; }
    }

    public class FaceLoginRequest
    {
        /// <summary>
        /// Vector đặc trưng khuôn mặt (dạng chuỗi JSON float[] hoặc danh sách phân tách dấu phẩy)
        /// </summary>
        [Required]
        public string FaceEmbedding { get; set; } = string.Empty;

        /// <summary>
        /// Mã sinh viên (tùy chọn để xác thực 1-1, hoặc để trống để nhận diện 1-N)
        /// </summary>
        public string? StudentCode { get; set; }
    }

    public class FaceRegisterRequest
    {
        [Required]
        public string FaceEmbedding { get; set; } = string.Empty;

        public int? StudentId { get; set; }
    }

    public class CurrentUserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();

        /// <summary>
        /// Điều hướng theo phân quyền đề bài:
        /// - Admin: "AdminDashboard" (Trang quản trị)
        /// - Lecturer: "LecturerDashboard" (Dashboard nhân viên/giảng viên)
        /// - Student: "StudentDashboard" (Trang sinh viên/khách hàng)
        /// </summary>
        public string TargetDashboard { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;

        public object? Profile { get; set; }
    }
}
