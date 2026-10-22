using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class Course
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string CourseName { get; set; } = string.Empty;

        public int Credits { get; set; }

        [JsonIgnore]
        public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    }
}
