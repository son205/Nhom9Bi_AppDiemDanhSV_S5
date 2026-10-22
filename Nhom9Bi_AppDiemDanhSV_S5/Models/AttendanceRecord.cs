using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class AttendanceRecord
    {
        public int Id { get; set; }

        public int StudentId { get; set; }

        public int ClassSessionId { get; set; }

        public DateTime AttendanceTime { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(30)]
        public string Method { get; set; } = "QR";

        [StringLength(20)]
        public string Status { get; set; } = "Present";

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? DistanceMeters { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }

        [JsonIgnore]
        public ClassSession? ClassSession { get; set; }
    }
}
