using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class Lecturer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string LecturerCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Department { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        [JsonIgnore]
        public ApplicationUser? User { get; set; }

        [JsonIgnore]
        public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    }
}
