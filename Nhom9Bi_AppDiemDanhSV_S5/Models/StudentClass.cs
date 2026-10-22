using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class StudentClass
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string ClassCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ClassName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? AcademicYear { get; set; }

        [JsonIgnore]
        public ICollection<Student> Students { get; set; } = new List<Student>();

        [JsonIgnore]
        public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    }
}
