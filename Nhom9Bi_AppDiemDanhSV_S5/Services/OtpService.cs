using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Nhom9Bi_AppDiemDanhSV_S5.Services
{
    public class OtpSession
    {
        public string UserId { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Method { get; set; } = "Email";
        public DateTime ExpiresAt { get; set; }
        public int AttemptCount { get; set; } = 0;
    }

    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpService> _logger;
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        public OtpService(IMemoryCache cache, ILogger<OtpService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<(string TempToken, string OtpCode, DateTime ExpiresAt)> GenerateOtpAsync(
            string userId, string destination, string method)
        {
            var tempToken = Guid.NewGuid().ToString("N");
            var otpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var expiresAt = DateTime.UtcNow.Add(DefaultExpiry);

            var session = new OtpSession
            {
                UserId = userId,
                OtpCode = otpCode,
                Destination = destination,
                Method = method,
                ExpiresAt = expiresAt
            };

            _cache.Set(tempToken, session, DefaultExpiry);

            // Gửi OTP (Email hoặc SMS). Trong môi trường Dev/Test ta ghi log chi tiết.
            if (string.Equals(method, "SMS", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(">>> [SMS OTP SERVICE] Gui ma OTP den so {Destination}: {OtpCode} (Hieu luc 5 phut)", destination, otpCode);
                Console.WriteLine($">>> [SMS OTP SERVICE] Gui ma OTP den so {destination}: {otpCode}");
            }
            else
            {
                _logger.LogInformation(">>> [EMAIL OTP SERVICE] Gui ma OTP den email {Destination}: {OtpCode} (Hieu luc 5 phut)", destination, otpCode);
                Console.WriteLine($">>> [EMAIL OTP SERVICE] Gui ma OTP den email {destination}: {otpCode}");
            }

            return Task.FromResult((tempToken, otpCode, expiresAt));
        }

        public Task<(bool IsValid, string? UserId, string? ErrorMessage)> ValidateOtpAsync(
            string tempToken, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(tempToken) || string.IsNullOrWhiteSpace(otpCode))
            {
                return Task.FromResult<(bool, string?, string?)>((false, null, "Mã OTP và phiên xác thực không hợp lệ."));
            }

            if (!_cache.TryGetValue(tempToken, out OtpSession? session) || session == null)
            {
                return Task.FromResult<(bool, string?, string?)>((false, null, "Phiên OTP đã hết hạn hoặc không tồn tại. Vui lòng gửi lại OTP."));
            }

            if (DateTime.UtcNow > session.ExpiresAt)
            {
                _cache.Remove(tempToken);
                return Task.FromResult<(bool, string?, string?)>((false, null, "Mã OTP đã hết hiệu lực."));
            }

            session.AttemptCount++;
            if (session.AttemptCount > 5)
            {
                _cache.Remove(tempToken);
                return Task.FromResult<(bool, string?, string?)>((false, null, "Bạn đã nhập sai OTP quá số lần cho phép (5 lần). Vui lòng yêu cầu lại OTP."));
            }

            if (!string.Equals(session.OtpCode.Trim(), otpCode.Trim(), StringComparison.Ordinal))
            {
                return Task.FromResult<(bool, string?, string?)>((false, null, "Mã OTP không chính xác."));
            }

            // OTP hợp lệ -> xóa khỏi cache để không dùng lại (One-Time Password)
            _cache.Remove(tempToken);
            return Task.FromResult<(bool, string?, string?)>((true, session.UserId, null));
        }

        public Task<(string TempToken, string OtpCode, DateTime ExpiresAt)?> ResendOtpAsync(
            string tempToken, string method)
        {
            if (!_cache.TryGetValue(tempToken, out OtpSession? session) || session == null)
            {
                return Task.FromResult<(string, string, DateTime)?>(null);
            }

            _cache.Remove(tempToken);
            var result = GenerateOtpAsync(session.UserId, session.Destination, method).Result;
            return Task.FromResult<(string, string, DateTime)?>(result);
        }
    }
}
