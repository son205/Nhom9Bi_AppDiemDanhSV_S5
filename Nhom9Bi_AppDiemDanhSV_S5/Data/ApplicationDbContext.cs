
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nhom9Bi_AppDiemDanhSV_S5.Models;

namespace Nhom9Bi_AppDiemDanhSV_S5.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Lecturer> Lecturers { get; set; }
        public DbSet<StudentClass> Classes { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<ClassSession> ClassSessions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Student>()
                .HasIndex(s => s.StudentCode)
                .IsUnique();

            builder.Entity<Lecturer>()
                .HasIndex(l => l.LecturerCode)
                .IsUnique();

            builder.Entity<Student>()
                .HasIndex(s => s.UserId)
                .IsUnique();

            builder.Entity<Student>()
                .HasOne(s => s.User)
                .WithOne(u => u.Student)
                .HasForeignKey<Student>(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Lecturer>()
                .HasIndex(l => l.UserId)
                .IsUnique();

            builder.Entity<Lecturer>()
                .HasOne(l => l.User)
                .WithOne(u => u.Lecturer)
                .HasForeignKey<Lecturer>(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Student>()
                .HasOne(s => s.Class)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ClassSession>()
                .HasOne(s => s.Class)
                .WithMany(c => c.ClassSessions)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ClassSession>()
                .HasOne(s => s.Course)
                .WithMany(c => c.ClassSessions)
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ClassSession>()
                .HasOne(s => s.Lecturer)
                .WithMany(l => l.ClassSessions)
                .HasForeignKey(s => s.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AttendanceRecord>()
                .HasIndex(a => new { a.StudentId, a.ClassSessionId })
                .IsUnique();

            builder.Entity<AttendanceRecord>()
                .HasOne(a => a.Student)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AttendanceRecord>()
                .HasOne(a => a.ClassSession)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(a => a.ClassSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
