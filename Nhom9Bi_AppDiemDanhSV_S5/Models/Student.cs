using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class Student
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string StudentCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public int ClassId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        [JsonIgnore]
        public ApplicationUser? User { get; set; }

        [JsonIgnore]
        public StudentClass? Class { get; set; }

        [JsonIgnore]
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

        public string? FaceEmbedding { get; set; }

        public bool IsFaceRegistered { get; set; } = false;

        public DateTime? FaceRegisteredAt { get; set; }
    }
}
