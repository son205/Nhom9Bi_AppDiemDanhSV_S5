namespace Nhom9Bi_AppDiemDanhSV_S5.Services
{
    public interface IOtpService
    {
        Task<(string TempToken, string OtpCode, DateTime ExpiresAt)> GenerateOtpAsync(
            string userId, string destination, string method);

        Task<(bool IsValid, string? UserId, string? ErrorMessage)> ValidateOtpAsync(
            string tempToken, string otpCode);

        Task<(string TempToken, string OtpCode, DateTime ExpiresAt)?> ResendOtpAsync(
            string tempToken, string method);
    }
}
