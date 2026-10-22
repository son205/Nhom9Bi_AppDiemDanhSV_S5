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
    public class ClassesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClassesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 4. CRUD: READ (GET ALL - ADMIN, LECTURER)
        // ==========================================
        [HttpGet]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GetAll()
        {
            var classes = await _context.Classes
                .AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.ClassCode,
                    c.ClassName,
                    c.AcademicYear,
                    StudentCount = c.Students.Count(),
                    ClassSessionCount = c.ClassSessions.Count()
                })
                .ToListAsync();

            return Ok(classes);
        }

        // ==========================================
        // 4. CRUD: READ (GET BY ID)
        // ==========================================
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    c.Id,
                    c.ClassCode,
                    c.ClassName,
                    c.AcademicYear,
                    StudentCount = c.Students.Count(),
                    ClassSessionCount = c.ClassSessions.Count()
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound(new { message = "Không tìm thấy lớp học." });
            }

            return Ok(item);
        }

        // ==========================================
        // 4. CRUD: CREATE (POST - ADMIN ONLY)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] StudentClass model)
        {
            if (string.IsNullOrWhiteSpace(model.ClassCode) || string.IsNullOrWhiteSpace(model.ClassName))
            {
                return BadRequest(new { message = "Mã lớp và tên lớp không được để trống." });
            }

            model.ClassCode = model.ClassCode.Trim();
            model.ClassName = model.ClassName.Trim();

            var codeExists = await _context.Classes.AnyAsync(c => c.ClassCode == model.ClassCode);
            if (codeExists)
            {
                return Conflict(new { message = "Mã lớp học đã tồn tại." });
            }

            model.Students = new List<Student>();
            model.ClassSessions = new List<ClassSession>();

            _context.Classes.Add(model);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = model.Id },
                new
                {
                    message = "Tạo lớp học thành công.",
                    model.Id,
                    model.ClassCode,
                    model.ClassName,
                    model.AcademicYear
                });
        }

        // ==========================================
        // 4. CRUD: UPDATE (PUT - ADMIN ONLY)
        // ==========================================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] StudentClass model)
        {
            var existing = await _context.Classes.FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null)
            {
                return NotFound(new { message = "Không tìm thấy lớp học." });
            }

            if (string.IsNullOrWhiteSpace(model.ClassCode) || string.IsNullOrWhiteSpace(model.ClassName))
            {
                return BadRequest(new { message = "Mã lớp và tên lớp không được để trống." });
            }

            var classCode = model.ClassCode.Trim();
            var className = model.ClassName.Trim();

            var codeExists = await _context.Classes.AnyAsync(c => c.ClassCode == classCode && c.Id != id);
            if (codeExists)
            {
                return Conflict(new { message = "Mã lớp học đã được sử dụng." });
            }

            existing.ClassCode = classCode;
            existing.ClassName = className;
            existing.AcademicYear = model.AcademicYear;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật lớp học thành công.",
                existing.Id,
                existing.ClassCode,
                existing.ClassName,
                existing.AcademicYear
            });
        }

        // ==========================================
        // 4. CRUD: DELETE (DELETE - ADMIN ONLY)
        // ==========================================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Classes
                .Include(c => c.Students)
                .Include(c => c.ClassSessions)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (existing == null)
            {
                return NotFound(new { message = "Không tìm thấy lớp học." });
            }

            if (existing.Students.Any() || existing.ClassSessions.Any())
            {
                return Conflict(new
                {
                    message = "Không thể xóa lớp học đã có sinh viên hoặc buổi học. Hãy xử lý dữ liệu liên quan trước."
                });
            }

            _context.Classes.Remove(existing);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa lớp học thành công." });
        }
    }
}
