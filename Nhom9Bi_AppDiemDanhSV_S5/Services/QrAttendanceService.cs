using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Nhom9Bi_AppDiemDanhSV_S5.Services
{
    public class QrAttendanceService : IQrAttendanceService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly byte[] _secretKeyBytes;

        public QrAttendanceService(IConfiguration configuration, IMemoryCache cache)
        {
            _configuration = configuration;
            _cache = cache;
            var secret = _configuration["Jwt:SecretKey"] ?? "Attendance_Default_Secret_Key_32bytes_Long";
            _secretKeyBytes = Encoding.UTF8.GetBytes(secret);
        }

        public string GenerateQrToken(int classSessionId, int validForSeconds = 120)
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + validForSeconds;
            var nonce = Guid.NewGuid().ToString("N")[..8];
            var payload = $"{classSessionId}:{expiresAt}:{nonce}";

            using var hmac = new HMACSHA256(_secretKeyBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var signature = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('=');

            var fullToken = $"{payload}:{signature}";
            var base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullToken));

            // Lưu token mới nhất vào cache cho session này
            var cacheKey = $"QR_SESSION_{classSessionId}";
            _cache.Set(cacheKey, base64Token, TimeSpan.FromSeconds(validForSeconds));

            return base64Token;
        }

        public bool ValidateQrToken(int classSessionId, string qrToken, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(qrToken))
            {
                errorMessage = "Mã QR không được để trống.";
                return false;
            }

            try
            {
                string raw;
                try
                {
                    raw = Encoding.UTF8.GetString(Convert.FromBase64String(qrToken.Trim()));
                }
                catch
                {
                    raw = qrToken.Trim();
                }

                var parts = raw.Split(':');
                if (parts.Length != 4)
                {
                    errorMessage = "Cấu trúc mã QR không hợp lệ.";
                    return false;
                }

                if (!int.TryParse(parts[0], out var tokenSessionId) || tokenSessionId != classSessionId)
                {
                    errorMessage = "Mã QR này không thuộc về buổi học đang chọn.";
                    return false;
                }

                if (!long.TryParse(parts[1], out var expiresAt))
                {
                    errorMessage = "Thời gian hiệu lực của mã QR không hợp lệ.";
                    return false;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now > expiresAt)
                {
                    errorMessage = "Mã QR đã hết hạn. Vui lòng quét lại mã mới từ giảng viên.";
                    return false;
                }

                var nonce = parts[2];
                var providedSig = parts[3];

                var payload = $"{tokenSessionId}:{expiresAt}:{nonce}";
                using var hmac = new HMACSHA256(_secretKeyBytes);
                var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var expectedSig = Convert.ToBase64String(expectedHash).Replace("+", "-").Replace("/", "_").TrimEnd('=');

                if (!string.Equals(providedSig, expectedSig, StringComparison.Ordinal))
                {
                    errorMessage = "Chữ ký số của mã QR không hợp lệ hoặc đã bị chỉnh sửa.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Lỗi xử lý mã QR: {ex.Message}";
                return false;
            }
        }
    }
}
