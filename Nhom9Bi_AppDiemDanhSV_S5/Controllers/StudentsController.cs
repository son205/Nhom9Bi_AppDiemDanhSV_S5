using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom9Bi_AppDiemDanhSV_S5.Data;
using Nhom9Bi_AppDiemDanhSV_S5.DTOs;
using Nhom9Bi_AppDiemDanhSV_S5.Models;
using Nhom9Bi_AppDiemDanhSV_S5.Services;

namespace Nhom9Bi_AppDiemDanhSV_S5.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFaceRecognitionService _faceService;

        public StudentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFaceRecognitionService faceService)
        {
            _context = context;
            _userManager = userManager;
            _faceService = faceService;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<Lecturer?> GetCurrentLecturerAsync()
        {
            if (CurrentUserId == null) return null;
            return await _context.Lecturers.FirstOrDefaultAsync(l => l.UserId == CurrentUserId);
        }

        // ==========================================
        // 4. CRUD: READ (GET ALL)
        // ==========================================
        [HttpGet]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GetAll()
        {
            var query = _context.Students.AsNoTracking();

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null) return Forbid();

                var classIds = _context.ClassSessions
                    .Where(s => s.LecturerId == lecturer.Id)
                    .Select(s => s.ClassId)
                    .Distinct();

                query = query.Where(s => classIds.Contains(s.ClassId));
            }

            var students = await query
                .Select(s => new
                {
                    s.Id,
                    s.StudentCode,
                    s.FullName,
                    s.Gender,
                    s.DateOfBirth,
                    s.Address,
                    s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : string.Empty,
                    s.UserId,
                    s.IsFaceRegistered,
                    s.FaceRegisteredAt
                })
                .ToListAsync();

            return Ok(students);
        }

        // ==========================================
        // LẤY HỒ SƠ SINH VIÊN HIỆN TẠI (ME)
        // ==========================================
        [HttpGet("me")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyProfile()
        {
            if (CurrentUserId == null) return Unauthorized();

            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Class)
                .Where(s => s.UserId == CurrentUserId)
                .Select(s => new
                {
                    s.Id,
                    s.StudentCode,
                    s.FullName,
                    s.Gender,
                    s.DateOfBirth,
                    s.Address,
                    s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : string.Empty,
                    s.UserId,
                    s.IsFaceRegistered,
                    s.FaceRegisteredAt
                })
                .FirstOrDefaultAsync();

            if (student == null)
                return NotFound(new { message = "Chưa tìm thấy hồ sơ sinh viên liên kết với tài khoản này." });

            return Ok(student);
        }

        // ==========================================
        // 4. CRUD: READ (GET BY ID)
        // ==========================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    s.Id,
                    s.StudentCode,
                    s.FullName,
                    s.Gender,
                    s.DateOfBirth,
                    s.Address,
                    s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : string.Empty,
                    s.UserId,
                    s.IsFaceRegistered,
                    s.FaceRegisteredAt
                })
                .FirstOrDefaultAsync();

            if (student == null) return NotFound(new { message = "Không tìm thấy sinh viên." });

            if (User.IsInRole("Student"))
            {
                if (student.UserId != CurrentUserId) return Forbid();
                return Ok(student);
            }

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null) return Forbid();

                var allowed = await _context.ClassSessions.AnyAsync(s =>
                    s.LecturerId == lecturer.Id && s.ClassId == student.ClassId);

                if (!allowed) return Forbid();
            }

            return Ok(student);
        }

        // ==========================================
        // 4. CRUD: CREATE (POST - ADMIN ONLY)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Student student)
        {
            if (string.IsNullOrWhiteSpace(student.StudentCode) ||
                string.IsNullOrWhiteSpace(student.FullName) ||
                string.IsNullOrWhiteSpace(student.UserId))
            {
                return BadRequest(new { message = "Thiếu thông tin mã sinh viên, họ tên hoặc UserId." });
            }

            student.StudentCode = student.StudentCode.Trim();
            student.FullName = student.FullName.Trim();

            if (await _context.Students.AnyAsync(s => s.StudentCode == student.StudentCode))
                return Conflict(new { message = "Mã sinh viên đã tồn tại." });

            if (!await _context.Classes.AnyAsync(c => c.Id == student.ClassId))
                return BadRequest(new { message = "Lớp không tồn tại." });

            var user = await _userManager.FindByIdAsync(student.UserId);
            if (user == null)
                return BadRequest(new { message = "Tài khoản người dùng không tồn tại." });

            if (!await _userManager.IsInRoleAsync(user, "Student"))
                return BadRequest(new { message = "Tài khoản chưa được cấp vai trò Student." });

            if (await _context.Students.AnyAsync(s => s.UserId == student.UserId))
                return Conflict(new { message = "Tài khoản này đã được liên kết với một hồ sơ sinh viên khác." });

            student.Id = 0;
            student.User = null;
            student.Class = null;
            student.AttendanceRecords = new List<AttendanceRecord>();
            student.IsFaceRegistered = false;
            student.FaceRegisteredAt = null;

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById),
                new { id = student.Id },
                new { student.Id, student.StudentCode, student.FullName, message = "Thêm sinh viên thành công." });
        }

        // ==========================================
        // 4. CRUD: UPDATE (PUT - ADMIN ONLY)
        // ==========================================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Student request)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound(new { message = "Không tìm thấy sinh viên." });

            if (string.IsNullOrWhiteSpace(request.StudentCode) ||
                string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(new { message = "Thiếu mã hoặc họ tên sinh viên." });
            }

            var studentCode = request.StudentCode.Trim();
            if (await _context.Students.AnyAsync(s => s.Id != id && s.StudentCode == studentCode))
            {
                return Conflict(new { message = "Mã sinh viên đã tồn tại." });
            }

            if (!await _context.Classes.AnyAsync(c => c.Id == request.ClassId))
            {
                return BadRequest(new { message = "Lớp không tồn tại." });
            }

            student.StudentCode = studentCode;
            student.FullName = request.FullName.Trim();
            student.Gender = request.Gender;
            student.DateOfBirth = request.DateOfBirth;
            student.Address = request.Address;
            student.ClassId = request.ClassId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật sinh viên thành công.", student.Id, student.StudentCode, student.FullName });
        }

        // ==========================================
        // 4. CRUD: DELETE (DELETE - ADMIN ONLY)
        // ==========================================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound(new { message = "Không tìm thấy sinh viên." });

            if (await _context.AttendanceRecords.AnyAsync(a => a.StudentId == id))
            {
                return Conflict(new { message = "Không thể xóa sinh viên đã có dữ liệu điểm danh." });
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa sinh viên thành công." });
        }

        // ==========================================
        // 5.a ĐĂNG KÝ KHUÔN MẶT CHO SINH VIÊN QUA ID
        // ==========================================
        [HttpPost("{id:int}/face")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> UpdateFaceEmbedding(int id, [FromBody] FaceRegisterRequest request)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound(new { message = "Không tìm thấy sinh viên." });

            if (User.IsInRole("Student") && student.UserId != CurrentUserId)
                return Forbid();

            var vector = _faceService.ParseEmbedding(request.FaceEmbedding);
            if (vector == null || vector.Length == 0)
                return BadRequest(new { message = "Vector khuôn mặt không hợp lệ." });

            student.FaceEmbedding = System.Text.Json.JsonSerializer.Serialize(vector);
            student.IsFaceRegistered = true;
            student.FaceRegisteredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật dữ liệu khuôn mặt thành công.",
                student.Id,
                student.StudentCode,
                dimensions = vector.Length,
                student.FaceRegisteredAt
            });
        }
    }
}
