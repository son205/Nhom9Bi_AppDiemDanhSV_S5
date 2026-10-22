using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom9Bi_AppDiemDanhSV_S5.Data;
using Nhom9Bi_AppDiemDanhSV_S5.Models;

namespace Nhom9Bi_AppDiemDanhSV_S5.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Lecturer")]
    public class LecturersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LecturersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ==========================================
        // 4. CRUD: READ (GET ALL)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var query = _context.Lecturers.AsNoTracking();

            if (User.IsInRole("Lecturer"))
                query = query.Where(l => l.UserId == CurrentUserId);

            var lecturers = await query
                .Select(l => new
                {
                    l.Id,
                    l.LecturerCode,
                    l.FullName,
                    l.Department,
                    l.UserId
                })
                .ToListAsync();

            return Ok(lecturers);
        }

        // ==========================================
        // LẤY HỒ SƠ GIẢNG VIÊN HIỆN TẠI (ME)
        // ==========================================
        [HttpGet("me")]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> GetMyProfile()
        {
            if (CurrentUserId == null) return Unauthorized();

            var lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l => l.UserId == CurrentUserId)
                .Select(l => new
                {
                    l.Id,
                    l.LecturerCode,
                    l.FullName,
                    l.Department,
                    l.UserId
                })
                .FirstOrDefaultAsync();

            if (lecturer == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ giảng viên liên kết." });

            return Ok(lecturer);
        }

        // ==========================================
        // 4. CRUD: READ (GET BY ID)
        // ==========================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l => l.Id == id)
                .Select(l => new
                {
                    l.Id,
                    l.LecturerCode,
                    l.FullName,
                    l.Department,
                    l.UserId
                })
                .FirstOrDefaultAsync();

            if (lecturer == null) return NotFound(new { message = "Không tìm thấy giảng viên." });

            if (User.IsInRole("Lecturer") && lecturer.UserId != CurrentUserId)
                return Forbid();

            return Ok(lecturer);
        }

        // ==========================================
        // 4. CRUD: CREATE (POST - ADMIN ONLY)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Lecturer request)
        {
            if (string.IsNullOrWhiteSpace(request.LecturerCode) ||
                string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.UserId))
                return BadRequest(new { message = "Thiếu thông tin mã giảng viên, họ tên hoặc UserId." });

            request.LecturerCode = request.LecturerCode.Trim();
            request.FullName = request.FullName.Trim();

            if (await _context.Lecturers.AnyAsync(l => l.LecturerCode == request.LecturerCode))
                return Conflict(new { message = "Mã giảng viên đã tồn tại." });

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(new { message = "Tài khoản không tồn tại." });

            if (!await _userManager.IsInRoleAsync(user, "Lecturer"))
                return BadRequest(new { message = "Tài khoản chưa được cấp vai trò Lecturer." });

            if (await _context.Lecturers.AnyAsync(l => l.UserId == request.UserId))
                return Conflict(new { message = "Tài khoản này đã liên kết với một giảng viên khác." });

            request.Id = 0;
            request.User = null;
            request.ClassSessions = new List<ClassSession>();

            _context.Lecturers.Add(request);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById),
                new { id = request.Id },
                new { request.Id, request.LecturerCode, request.FullName, message = "Thêm giảng viên thành công." });
        }

        // ==========================================
        // 4. CRUD: UPDATE (PUT - ADMIN ONLY)
        // ==========================================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Lecturer request)
        {
            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer == null) return NotFound(new { message = "Không tìm thấy giảng viên." });

            if (string.IsNullOrWhiteSpace(request.LecturerCode) ||
                string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Thiếu mã hoặc họ tên giảng viên." });

            var lecturerCode = request.LecturerCode.Trim();
            if (await _context.Lecturers.AnyAsync(l => l.Id != id && l.LecturerCode == lecturerCode))
                return Conflict(new { message = "Mã giảng viên đã tồn tại." });

            lecturer.LecturerCode = lecturerCode;
            lecturer.FullName = request.FullName.Trim();
            lecturer.Department = request.Department;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật giảng viên thành công.", lecturer.Id, lecturer.LecturerCode, lecturer.FullName });
        }

        // ==========================================
        // 4. CRUD: DELETE (DELETE - ADMIN ONLY)
        // ==========================================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer == null) return NotFound(new { message = "Không tìm thấy giảng viên." });

            if (await _context.ClassSessions.AnyAsync(s => s.LecturerId == id))
            {
                return Conflict(new { message = "Không thể xóa giảng viên đang phụ trách các buổi học." });
            }

            _context.Lecturers.Remove(lecturer);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa giảng viên thành công." });
        }
    }
}
