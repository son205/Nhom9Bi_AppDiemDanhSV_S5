using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class ClassSession
    {
        public int Id { get; set; }

        public int ClassId { get; set; }

        public int CourseId { get; set; }

        public int LecturerId { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        [StringLength(100)]
        public string? Room { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double AllowedRadiusMeters { get; set; } = 100;

        [StringLength(20)]
        public string Status { get; set; } = "Scheduled";

        [JsonIgnore]
        public StudentClass? Class { get; set; }

        [JsonIgnore]
        public Course? Course { get; set; }

        [JsonIgnore]
        public Lecturer? Lecturer { get; set; }

        [JsonIgnore]
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}
