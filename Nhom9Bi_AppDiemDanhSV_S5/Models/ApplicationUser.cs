using Microsoft.AspNetCore.Identity;

namespace Nhom9Bi_AppDiemDanhSV_S5.Models
{
    public class ApplicationUser : IdentityUser
    {
      
            public string FullName { get; set; } = string.Empty;

            public bool IsActive { get; set; } = true;

            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            public Student? Student { get; set; }

            public Lecturer? Lecturer { get; set; }
        
    }
}
