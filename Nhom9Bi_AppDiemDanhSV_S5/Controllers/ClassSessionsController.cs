using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    public class ClassSessionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IQrAttendanceService _qrService;

        public ClassSessionsController(
            ApplicationDbContext context,
            IQrAttendanceService qrService)
        {
            _context = context;
            _qrService = qrService;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<Lecturer?> GetCurrentLecturerAsync()
        {
            if (CurrentUserId == null) return null;
            return await _context.Lecturers.FirstOrDefaultAsync(l => l.UserId == CurrentUserId);
        }

        private IQueryable<ClassSession> VisibleSessions()
        {
            var query = _context.ClassSessions.AsNoTracking();

            if (User.IsInRole("Lecturer"))
            {
                query = query.Where(s => s.Lecturer != null && s.Lecturer.UserId == CurrentUserId);
            }
            else if (User.IsInRole("Student"))
            {
                query = query.Where(s => s.Class != null && s.Class.Students.Any(st => st.UserId == CurrentUserId));
            }

            return query;
        }

        // ==========================================
        // 4. CRUD: READ (GET ALL)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var sessions = await VisibleSessions()
                .Select(s => new
                {
                    s.Id,
                    s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : string.Empty,
                    s.CourseId,
                    CourseName = s.Course != null ? s.Course.CourseName : string.Empty,
                    s.LecturerId,
                    LecturerName = s.Lecturer != null ? s.Lecturer.FullName : string.Empty,
                    s.StartTime,
                    s.EndTime,
                    s.Room,
                    s.Latitude,
                    s.Longitude,
                    s.AllowedRadiusMeters,
                    s.Status,
                    AttendanceCount = s.AttendanceRecords.Count()
                })
                .ToListAsync();

            return Ok(sessions);
        }

        // ==========================================
        // 4. CRUD: READ (GET BY ID)
        // ==========================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var session = await VisibleSessions()
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    s.Id,
                    s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : string.Empty,
                    s.CourseId,
                    CourseName = s.Course != null ? s.Course.CourseName : string.Empty,
                    s.LecturerId,
                    LecturerName = s.Lecturer != null ? s.Lecturer.FullName : string.Empty,
                    s.StartTime,
                    s.EndTime,
                    s.Room,
                    s.Latitude,
                    s.Longitude,
                    s.AllowedRadiusMeters,
                    s.Status,
                    AttendanceCount = s.AttendanceRecords.Count()
                })
                .FirstOrDefaultAsync();

            return session == null ? NotFound(new { message = "Không tìm thấy buổi học." }) : Ok(session);
        }

        // ==========================================
        // 4. CRUD: CREATE (POST)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Create([FromBody] ClassSession request)
        {
            if (request.EndTime <= request.StartTime)
                return BadRequest(new { message = "Thời gian kết thúc phải sau thời gian bắt đầu." });

            if (request.AllowedRadiusMeters <= 0)
                return BadRequest(new { message = "Bán kính điểm danh phải lớn hơn 0." });

            if (request.Latitude.HasValue && (request.Latitude < -90 || request.Latitude > 90))
                return BadRequest(new { message = "Vĩ độ không hợp lệ (-90 đến 90)." });

            if (request.Longitude.HasValue && (request.Longitude < -180 || request.Longitude > 180))
                return BadRequest(new { message = "Kinh độ không hợp lệ (-180 đến 180)." });

            var lecturerId = request.LecturerId;
            if (User.IsInRole("Lecturer"))
            {
                var currentLecturer = await GetCurrentLecturerAsync();
                if (currentLecturer == null) return Forbid();
                lecturerId = currentLecturer.Id;
            }

            var valid = await _context.Classes.AnyAsync(c => c.Id == request.ClassId)
                && await _context.Courses.AnyAsync(c => c.Id == request.CourseId)
                && await _context.Lecturers.AnyAsync(l => l.Id == lecturerId);

            if (!valid)
            {
                return BadRequest(new { message = "Lớp, môn học hoặc giảng viên không tồn tại." });
            }

            var session = new ClassSession
            {
                ClassId = request.ClassId,
                CourseId = request.CourseId,
                LecturerId = lecturerId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Room = request.Room,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                AllowedRadiusMeters = request.AllowedRadiusMeters,
                Status = "Scheduled"
            };

            _context.ClassSessions.Add(session);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById),
                new { id = session.Id },
                new { session.Id, session.Status, message = "Tạo buổi học thành công." });
        }

        // ==========================================
        // 4. CRUD: UPDATE (PUT)
        // ==========================================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassSession request)
        {
            var session = await _context.ClassSessions.FindAsync(id);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null || session.LecturerId != lecturer.Id)
                    return Forbid();
            }

            if (request.EndTime <= request.StartTime || request.AllowedRadiusMeters <= 0)
                return BadRequest(new { message = "Thông tin buổi học không hợp lệ." });

            var valid = await _context.Classes.AnyAsync(c => c.Id == request.ClassId)
                && await _context.Courses.AnyAsync(c => c.Id == request.CourseId)
                && await _context.Lecturers.AnyAsync(l => l.Id == request.LecturerId);

            if (!valid)
                return BadRequest(new { message = "Lớp, môn học hoặc giảng viên không tồn tại." });

            session.ClassId = request.ClassId;
            session.CourseId = request.CourseId;
            session.LecturerId = request.LecturerId;
            session.StartTime = request.StartTime;
            session.EndTime = request.EndTime;
            session.Room = request.Room;
            session.Latitude = request.Latitude;
            session.Longitude = request.Longitude;
            session.AllowedRadiusMeters = request.AllowedRadiusMeters;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật buổi học thành công." });
        }

        // ==========================================
        // 4. CRUD: DELETE (DELETE)
        // ==========================================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var session = await _context.ClassSessions.FindAsync(id);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (await _context.AttendanceRecords.AnyAsync(a => a.ClassSessionId == id))
            {
                return Conflict(new { message = "Không thể xóa buổi học đã có bản ghi điểm danh." });
            }

            _context.ClassSessions.Remove(session);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa buổi học thành công." });
        }

        // ==========================================================
        // 5.b CHIA SẺ VỊ TRÍ: GIẢNG VIÊN CẬP NHẬT TỌA ĐỘ VỊ TRÍ PHÒNG HỌC
        // ==========================================================
        [HttpPut("{id:int}/location")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> ShareLocation(int id, [FromBody] ShareLocationRequest request)
        {
            var session = await _context.ClassSessions.FindAsync(id);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null || session.LecturerId != lecturer.Id)
                    return Forbid();
            }

            session.Latitude = request.Latitude;
            session.Longitude = request.Longitude;
            if (request.AllowedRadiusMeters.HasValue && request.AllowedRadiusMeters.Value > 0)
            {
                session.AllowedRadiusMeters = request.AllowedRadiusMeters.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Đã chia sẻ/cập nhật vị trí GPS lớp học thành công.",
                classSessionId = session.Id,
                session.Room,
                session.Latitude,
                session.Longitude,
                session.AllowedRadiusMeters
            });
        }

        // ==========================================================
        // 5.e NHẬN DIỆN QR: GIẢNG VIÊN TẠO MÃ QR ĐIỂM DANH ĐỘNG
        // ==========================================================
        [HttpPost("{id:int}/generate-qr")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GenerateQr(int id, [FromQuery] int validForSeconds = 120)
        {
            var session = await _context.ClassSessions
                .Include(s => s.Class)
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null || session.LecturerId != lecturer.Id)
                    return Forbid();
            }

            var now = DateTime.UtcNow;
            if (now > session.EndTime)
            {
                return BadRequest(new { message = "Buổi học đã kết thúc, không thể tạo mã điểm danh." });
            }

            if (validForSeconds <= 0 || validForSeconds > 600)
            {
                validForSeconds = 120; // Mặc định 2 phút
            }

            var qrToken = _qrService.GenerateQrToken(session.Id, validForSeconds);
            var expiresAt = DateTime.UtcNow.AddSeconds(validForSeconds);

            return Ok(new GenerateQrResponse
            {
                ClassSessionId = session.Id,
                QrToken = qrToken,
                QrPayload = qrToken,
                ExpiresAt = expiresAt,
                ValidForSeconds = validForSeconds
            });
        }
    }
}
