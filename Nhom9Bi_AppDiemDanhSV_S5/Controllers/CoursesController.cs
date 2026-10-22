using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom9Bi_AppDiemDanhSV_S5.Data;
using Nhom9Bi_AppDiemDanhSV_S5.Models;

namespace Nhom9Bi_AppDiemDanhSV_S5.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 4. CRUD: READ (GET ALL)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var courses = await _context.Courses
                .AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.CourseCode,
                    c.CourseName,
                    c.Credits,
                    ClassSessionCount = c.ClassSessions.Count()
                })
                .ToListAsync();

            return Ok(courses);
        }

        // ==========================================
        // 4. CRUD: READ (GET BY ID)
        // ==========================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var course = await _context.Courses
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    c.Id,
                    c.CourseCode,
                    c.CourseName,
                    c.Credits,
                    ClassSessionCount = c.ClassSessions.Count()
                })
                .FirstOrDefaultAsync();

            if (course == null)
                return NotFound(new { message = "Không tìm thấy môn học." });

            return Ok(course);
        }

        // ==========================================
        // 4. CRUD: CREATE (POST - ADMIN ONLY)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Course request)
        {
            if (string.IsNullOrWhiteSpace(request.CourseCode) || string.IsNullOrWhiteSpace(request.CourseName))
            {
                return BadRequest(new { message = "Mã môn học và tên môn học không được để trống." });
            }

            request.CourseCode = request.CourseCode.Trim();
            request.CourseName = request.CourseName.Trim();

            if (await _context.Courses.AnyAsync(c => c.CourseCode == request.CourseCode))
            {
                return Conflict(new { message = "Mã môn học đã tồn tại." });
            }

            var course = new Course
            {
                CourseCode = request.CourseCode,
                CourseName = request.CourseName,
                Credits = request.Credits
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = course.Id },
                new { course.Id, course.CourseCode, course.CourseName, course.Credits, message = "Tạo môn học thành công." });
        }

        // ==========================================
        // 4. CRUD: UPDATE (PUT - ADMIN ONLY)
        // ==========================================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Course request)
        {
            var course = await _context.Courses.FindAsync(id);

            if (course == null)
                return NotFound(new { message = "Không tìm thấy môn học." });

            if (string.IsNullOrWhiteSpace(request.CourseCode) || string.IsNullOrWhiteSpace(request.CourseName))
            {
                return BadRequest(new { message = "Mã môn học và tên môn học không được để trống." });
            }

            var code = request.CourseCode.Trim();
            if (await _context.Courses.AnyAsync(c => c.CourseCode == code && c.Id != id))
            {
                return Conflict(new { message = "Mã môn học đã được sử dụng." });
            }

            course.CourseCode = code;
            course.CourseName = request.CourseName.Trim();
            course.Credits = request.Credits;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật môn học thành công.",
                course.Id,
                course.CourseCode,
                course.CourseName,
                course.Credits
            });
        }

        // ==========================================
        // 4. CRUD: DELETE (DELETE - ADMIN ONLY)
        // ==========================================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.Courses.FindAsync(id);

            if (course == null)
                return NotFound(new { message = "Không tìm thấy môn học." });

            var hasSessions = await _context.ClassSessions.AnyAsync(s => s.CourseId == id);
            if (hasSessions)
            {
                return Conflict(new { message = "Không thể xóa môn học đang được sử dụng trong buổi học." });
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa môn học thành công." });
        }
    }
}
