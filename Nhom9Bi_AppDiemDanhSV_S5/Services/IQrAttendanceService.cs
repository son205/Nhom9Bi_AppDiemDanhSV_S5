namespace Nhom9Bi_AppDiemDanhSV_S5.Services
{
    public interface IQrAttendanceService
    {
        string GenerateQrToken(int classSessionId, int validForSeconds = 120);

        bool ValidateQrToken(int classSessionId, string qrToken, out string? errorMessage);
    }
}
