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
    public class AttendanceRecordsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IQrAttendanceService _qrService;
        private readonly IFaceRecognitionService _faceService;

        public AttendanceRecordsController(
            ApplicationDbContext context,
            IQrAttendanceService qrService,
            IFaceRecognitionService faceService)
        {
            _context = context;
            _qrService = qrService;
            _faceService = faceService;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<Lecturer?> GetCurrentLecturerAsync()
        {
            if (CurrentUserId == null) return null;
            return await _context.Lecturers.FirstOrDefaultAsync(l => l.UserId == CurrentUserId);
        }

        private async Task<Student?> GetCurrentStudentAsync()
        {
            if (CurrentUserId == null) return null;
            return await _context.Students.FirstOrDefaultAsync(s => s.UserId == CurrentUserId);
        }

        private IQueryable<AttendanceRecord> VisibleRecords()
        {
            var query = _context.AttendanceRecords.AsNoTracking();

            if (User.IsInRole("Lecturer"))
            {
                query = query.Where(a => a.ClassSession != null && a.ClassSession.Lecturer != null && a.ClassSession.Lecturer.UserId == CurrentUserId);
            }
            else if (User.IsInRole("Student"))
            {
                query = query.Where(a => a.Student != null && a.Student.UserId == CurrentUserId);
            }

            return query;
        }

        // ==========================================
        // 4. CRUD: READ (GET ALL)
        // ==========================================
        [HttpGet]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GetAll()
        {
            var records = await VisibleRecords()
                .Select(a => new
                {
                    a.Id,
                    a.StudentId,
                    StudentCode = a.Student != null ? a.Student.StudentCode : string.Empty,
                    StudentName = a.Student != null ? a.Student.FullName : string.Empty,
                    a.ClassSessionId,
                    CourseName = a.ClassSession != null && a.ClassSession.Course != null ? a.ClassSession.Course.CourseName : string.Empty,
                    a.AttendanceTime,
                    a.Method,
                    a.Status,
                    a.Latitude,
                    a.Longitude,
                    a.DistanceMeters
                })
                .ToListAsync();

            return Ok(records);
        }

        // ==========================================
        // 4. CRUD: READ (GET BY ID)
        // ==========================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var record = await VisibleRecords()
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    a.Id,
                    a.StudentId,
                    StudentCode = a.Student != null ? a.Student.StudentCode : string.Empty,
                    StudentName = a.Student != null ? a.Student.FullName : string.Empty,
                    a.ClassSessionId,
                    CourseName = a.ClassSession != null && a.ClassSession.Course != null ? a.ClassSession.Course.CourseName : string.Empty,
                    a.AttendanceTime,
                    a.Method,
                    a.Status,
                    a.Latitude,
                    a.Longitude,
                    a.DistanceMeters
                })
                .FirstOrDefaultAsync();

            return record == null ? NotFound(new { message = "Không tìm thấy bản ghi điểm danh." }) : Ok(record);
        }

        [HttpGet("session/{sessionId:int}")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GetBySession(int sessionId)
        {
            var sessionExists = await _context.ClassSessions.AnyAsync(s => s.Id == sessionId);
            if (!sessionExists)
                return NotFound(new { message = "Buổi học không tồn tại." });

            var records = await VisibleRecords()
                .Where(a => a.ClassSessionId == sessionId)
                .Select(a => new
                {
                    a.Id,
                    a.StudentId,
                    StudentCode = a.Student != null ? a.Student.StudentCode : string.Empty,
                    StudentName = a.Student != null ? a.Student.FullName : string.Empty,
                    a.ClassSessionId,
                    a.AttendanceTime,
                    a.Method,
                    a.Status,
                    a.Latitude,
                    a.Longitude,
                    a.DistanceMeters
                })
                .ToListAsync();

            return Ok(records);
        }

        [HttpGet("student/{studentId:int}")]
        public async Task<IActionResult> GetByStudent(int studentId)
        {
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null)
                return NotFound(new { message = "Sinh viên không tồn tại." });

            if (User.IsInRole("Student") && student.UserId != CurrentUserId)
                return Forbid();

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null) return Forbid();

                var hasAccess = await _context.ClassSessions.AnyAsync(s =>
                    s.LecturerId == lecturer.Id && s.ClassId == student.ClassId);

                if (!hasAccess) return Forbid();
            }

            var records = await VisibleRecords()
                .Where(a => a.StudentId == studentId)
                .Select(a => new
                {
                    a.Id,
                    a.StudentId,
                    a.ClassSessionId,
                    CourseName = a.ClassSession != null && a.ClassSession.Course != null ? a.ClassSession.Course.CourseName : string.Empty,
                    a.AttendanceTime,
                    a.Method,
                    a.Status,
                    a.Latitude,
                    a.Longitude,
                    a.DistanceMeters
                })
                .ToListAsync();

            return Ok(records);
        }

        // ==========================================================
        // 5.b CHIA SẺ VỊ TRÍ: KIỂM TRA KHOẢNG CÁCH TRƯỚC KHI ĐIỂM DANH
        // ==========================================================
        [HttpGet("check-location")]
        public async Task<IActionResult> CheckLocation([FromQuery] CheckLocationRequest request)
        {
            var session = await _context.ClassSessions.FindAsync(request.ClassSessionId);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (!session.Latitude.HasValue || !session.Longitude.HasValue)
            {
                return BadRequest(new { message = "Buổi học này chưa được cấu hình tọa độ GPS lớp học." });
            }

            var distance = DistanceInMeters(
                session.Latitude.Value,
                session.Longitude.Value,
                request.Latitude,
                request.Longitude);

            var inRadius = distance <= session.AllowedRadiusMeters;

            return Ok(new
            {
                classSessionId = session.Id,
                sessionRoom = session.Room,
                sessionLatitude = session.Latitude.Value,
                sessionLongitude = session.Longitude.Value,
                userLatitude = request.Latitude,
                userLongitude = request.Longitude,
                distanceMeters = Math.Round(distance, 2),
                allowedRadiusMeters = session.AllowedRadiusMeters,
                inRadius,
                message = inRadius 
                    ? "Bạn đang nằm trong phạm vi phòng học để điểm danh." 
                    : $"Bạn đang cách phòng học {Math.Round(distance, 1)}m (Vượt quá bán kính cho phép {session.AllowedRadiusMeters}m)."
            });
        }

        // ==========================================================
        // 5.b CHIA SẺ VỊ TRÍ: ĐIỂM DANH BẰNG GPS
        // ==========================================================
        [HttpPost]
        [HttpPost("gps")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CreateGpsAttendance([FromBody] StudentGpsAttendanceRequest request)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return BadRequest(new { message = "Tài khoản chưa được liên kết hồ sơ sinh viên." });

            var session = await _context.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.ClassSessionId);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (student.ClassId != session.ClassId)
                return Forbid();

            // Sửa lỗi NullReferenceException: Kiểm tra tọa độ phòng học có tồn tại không
            if (!session.Latitude.HasValue || !session.Longitude.HasValue)
            {
                return BadRequest(new { message = "Buổi học này chưa được cấu hình tọa độ GPS phòng học." });
            }

            var now = DateTime.UtcNow;
            if (now < session.StartTime || now > session.EndTime)
            {
                return BadRequest(new { message = "Chưa đến giờ hoặc buổi học đã kết thúc." });
            }

            var duplicate = await _context.AttendanceRecords.AnyAsync(a =>
                a.StudentId == student.Id && a.ClassSessionId == session.Id);

            if (duplicate)
                return Conflict(new { message = "Bạn đã điểm danh buổi học này." });

            var distance = DistanceInMeters(
                session.Latitude.Value,
                session.Longitude.Value,
                request.Latitude,
                request.Longitude);

            if (distance > session.AllowedRadiusMeters)
            {
                return BadRequest(new
                {
                    message = "Bạn đang ở ngoài khu vực điểm danh.",
                    distanceMeters = Math.Round(distance, 2),
                    allowedRadiusMeters = session.AllowedRadiusMeters
                });
            }

            var status = now > session.StartTime.AddMinutes(15) ? "Late" : "Present";

            var record = new AttendanceRecord
            {
                StudentId = student.Id,
                ClassSessionId = session.Id,
                AttendanceTime = now,
                Method = "GPS",
                Status = status,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                DistanceMeters = Math.Round(distance, 2)
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Điểm danh GPS thành công.",
                record.Id,
                record.StudentId,
                studentName = student.FullName,
                record.ClassSessionId,
                record.AttendanceTime,
                record.Method,
                record.Status,
                record.DistanceMeters
            });
        }

        // ==========================================================
        // 5.e NHẬN DIỆN QR: SINH VIÊN QUÉT MÃ QR ĐỂ ĐIỂM DANH
        // ==========================================================
        [HttpPost("scan-qr")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> ScanQrAttendance([FromBody] QrAttendanceRequest request)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return BadRequest(new { message = "Tài khoản chưa được liên kết hồ sơ sinh viên." });

            var session = await _context.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.ClassSessionId);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (student.ClassId != session.ClassId)
                return Forbid();

            // Xác thực mã QR thông qua service chữ ký số
            if (!_qrService.ValidateQrToken(session.Id, request.QrToken, out var errorMessage))
            {
                return BadRequest(new { message = errorMessage ?? "Mã QR không hợp lệ hoặc đã hết hạn." });
            }

            var now = DateTime.UtcNow;
            if (now < session.StartTime || now > session.EndTime)
            {
                return BadRequest(new { message = "Chưa đến giờ hoặc buổi học đã kết thúc." });
            }

            var duplicate = await _context.AttendanceRecords.AnyAsync(a =>
                a.StudentId == student.Id && a.ClassSessionId == session.Id);

            if (duplicate)
                return Conflict(new { message = "Bạn đã điểm danh buổi học này." });

            double? distance = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue &&
                session.Latitude.HasValue && session.Longitude.HasValue)
            {
                distance = Math.Round(DistanceInMeters(
                    session.Latitude.Value,
                    session.Longitude.Value,
                    request.Latitude.Value,
                    request.Longitude.Value), 2);
            }

            var status = now > session.StartTime.AddMinutes(15) ? "Late" : "Present";

            var record = new AttendanceRecord
            {
                StudentId = student.Id,
                ClassSessionId = session.Id,
                AttendanceTime = now,
                Method = "QR",
                Status = status,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                DistanceMeters = distance
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Điểm danh qua mã QR thành công!",
                record.Id,
                record.StudentId,
                studentName = student.FullName,
                record.ClassSessionId,
                record.AttendanceTime,
                record.Method,
                record.Status,
                record.DistanceMeters
            });
        }

        // ==========================================================
        // 5.a ĐIỂM DANH BẰNG KHUÔN MẶT (FACE ATTENDANCE)
        // ==========================================================
        [HttpPost("face")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> FaceAttendance([FromBody] FaceAttendanceRequest request)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return BadRequest(new { message = "Tài khoản chưa được liên kết hồ sơ sinh viên." });

            if (!student.IsFaceRegistered || string.IsNullOrWhiteSpace(student.FaceEmbedding))
            {
                return BadRequest(new { message = "Sinh viên chưa đăng ký khuôn mặt trên hệ thống. Vui lòng đăng ký khuôn mặt trước." });
            }

            var session = await _context.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.ClassSessionId);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (student.ClassId != session.ClassId)
                return Forbid();

            var queryVector = _faceService.ParseEmbedding(request.FaceEmbedding);
            if (queryVector == null)
                return BadRequest(new { message = "Vector khuôn mặt không hợp lệ." });

            var registeredVector = _faceService.ParseEmbedding(student.FaceEmbedding);
            if (registeredVector == null)
                return BadRequest(new { message = "Dữ liệu khuôn mặt đã lưu của sinh viên bị lỗi." });

            var similarity = _faceService.CalculateCosineSimilarity(queryVector, registeredVector);
            if (similarity < 0.70)
            {
                return BadRequest(new
                {
                    message = "Khuôn mặt không khớp với khuôn mặt đã đăng ký của bạn.",
                    similarity = Math.Round(similarity, 4),
                    requiredThreshold = 0.70
                });
            }

            var now = DateTime.UtcNow;
            if (now < session.StartTime || now > session.EndTime)
            {
                return BadRequest(new { message = "Chưa đến giờ hoặc buổi học đã kết thúc." });
            }

            var duplicate = await _context.AttendanceRecords.AnyAsync(a =>
                a.StudentId == student.Id && a.ClassSessionId == session.Id);

            if (duplicate)
                return Conflict(new { message = "Bạn đã điểm danh buổi học này." });

            double? distance = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue &&
                session.Latitude.HasValue && session.Longitude.HasValue)
            {
                distance = Math.Round(DistanceInMeters(
                    session.Latitude.Value,
                    session.Longitude.Value,
                    request.Latitude.Value,
                    request.Longitude.Value), 2);
            }

            var status = now > session.StartTime.AddMinutes(15) ? "Late" : "Present";

            var record = new AttendanceRecord
            {
                StudentId = student.Id,
                ClassSessionId = session.Id,
                AttendanceTime = now,
                Method = "Face",
                Status = status,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                DistanceMeters = distance
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Điểm danh bằng khuôn mặt thành công!",
                confidenceScore = Math.Round(similarity, 4),
                record.Id,
                record.StudentId,
                studentName = student.FullName,
                record.ClassSessionId,
                record.AttendanceTime,
                record.Method,
                record.Status,
                record.DistanceMeters
            });
        }

        // ==========================================================
        // 4. CRUD: GIẢNG VIÊN ĐIỂM DANH THỦ CÔNG
        // ==========================================================
        [HttpPost("manual")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> CreateManual([FromBody] ManualAttendanceRequest request)
        {
            var session = await _context.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.ClassSessionId);
            if (session == null)
                return NotFound(new { message = "Buổi học không tồn tại." });

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null || session.LecturerId != lecturer.Id)
                    return Forbid();
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (student == null)
                return NotFound(new { message = "Sinh viên không tồn tại." });

            if (student.ClassId != session.ClassId)
                return BadRequest(new { message = "Sinh viên không thuộc lớp của buổi học." });

            if (request.Status != "Present" && request.Status != "Late" && request.Status != "Absent")
                return BadRequest(new { message = "Trạng thái phải là Present, Late hoặc Absent." });

            var existing = await _context.AttendanceRecords
                .FirstOrDefaultAsync(a => a.StudentId == student.Id && a.ClassSessionId == session.Id);

            if (existing != null)
            {
                existing.Status = request.Status;
                existing.Method = "Manual";
                existing.AttendanceTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    message = "Đã cập nhật bản ghi điểm danh thủ công.",
                    existing.Id,
                    existing.StudentId,
                    existing.ClassSessionId,
                    existing.Method,
                    existing.Status
                });
            }

            var record = new AttendanceRecord
            {
                StudentId = student.Id,
                ClassSessionId = session.Id,
                AttendanceTime = DateTime.UtcNow,
                Method = "Manual",
                Status = request.Status
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Đã ghi nhận điểm danh thủ công.",
                record.Id,
                record.StudentId,
                record.ClassSessionId,
                record.Method,
                record.Status
            });
        }

        // ==========================================================
        // 4. CRUD: UPDATE BẢN GHI ĐIỂM DANH (PUT)
        // ==========================================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAttendanceRecordRequest request)
        {
            var record = await _context.AttendanceRecords
                .Include(a => a.ClassSession)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (record == null)
                return NotFound(new { message = "Không tìm thấy bản ghi điểm danh." });

            if (User.IsInRole("Lecturer"))
            {
                var lecturer = await GetCurrentLecturerAsync();
                if (lecturer == null || record.ClassSession == null || record.ClassSession.LecturerId != lecturer.Id)
                    return Forbid();
            }

            if (request.Status != "Present" && request.Status != "Late" && request.Status != "Absent")
            {
                return BadRequest(new { message = "Trạng thái phải là Present, Late hoặc Absent." });
            }

            record.Status = request.Status;
            if (!string.IsNullOrWhiteSpace(request.Method))
            {
                record.Method = request.Method;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật bản ghi điểm danh thành công.",
                record.Id,
                record.StudentId,
                record.ClassSessionId,
                record.Status,
                record.Method
            });
        }

        // ==========================================================
        // 4. CRUD: DELETE BẢN GHI ĐIỂM DANH (DELETE)
        // ==========================================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var record = await _context.AttendanceRecords.FindAsync(id);
            if (record == null)
                return NotFound(new { message = "Không tìm thấy bản ghi điểm danh." });

            _context.AttendanceRecords.Remove(record);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa bản ghi điểm danh thành công." });
        }

        // ==========================================
        // HELPER TÍNH KHOẢNG CÁCH HAVERSINE
        // ==========================================
        private static double DistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6371000;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) *
                Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
