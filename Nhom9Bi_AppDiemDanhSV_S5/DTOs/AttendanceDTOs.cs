using System.ComponentModel.DataAnnotations;

namespace Nhom9Bi_AppDiemDanhSV_S5.DTOs
{
    public class StudentGpsAttendanceRequest
    {
        [Required]
        public int ClassSessionId { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }
    }

    public class QrAttendanceRequest
    {
        [Required]
        public int ClassSessionId { get; set; }

        [Required]
        public string QrToken { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    public class FaceAttendanceRequest
    {
        [Required]
        public int ClassSessionId { get; set; }

        [Required]
        public string FaceEmbedding { get; set; } = string.Empty;

        public string? StudentCode { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    public class ManualAttendanceRequest
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassSessionId { get; set; }

        [Required]
        public string Status { get; set; } = "Present";
    }

    public class UpdateAttendanceRecordRequest
    {
        [Required]
        public string Status { get; set; } = "Present";

        public string? Method { get; set; }
    }

    public class ShareLocationRequest
    {
        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        public double? AllowedRadiusMeters { get; set; }
    }

    public class CheckLocationRequest
    {
        [Required]
        public int ClassSessionId { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }
    }

    public class GenerateQrResponse
    {
        public int ClassSessionId { get; set; }
        public string QrToken { get; set; } = string.Empty;
        public string QrPayload { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int ValidForSeconds { get; set; }
    }
}
