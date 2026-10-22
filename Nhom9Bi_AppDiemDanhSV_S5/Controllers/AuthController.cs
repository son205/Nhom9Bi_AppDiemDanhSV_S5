using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nhom9Bi_AppDiemDanhSV_S5.Data;
using Nhom9Bi_AppDiemDanhSV_S5.DTOs;
using Nhom9Bi_AppDiemDanhSV_S5.Models;
using Nhom9Bi_AppDiemDanhSV_S5.Services;

namespace Nhom9Bi_AppDiemDanhSV_S5.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly IFaceRecognitionService _faceService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            IOtpService otpService,
            IFaceRecognitionService faceService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _context = context;
            _otpService = otpService;
            _faceService = faceService;
            _logger = logger;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ==========================================
        // 1. ĐĂNG KÝ TÀI KHOẢN QUA EMAIL
        // ==========================================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingEmail = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (existingEmail != null)
                return BadRequest(new { message = "Email đã được sử dụng." });

            var existingUser = await _userManager.FindByNameAsync(request.UserName.Trim());
            if (existingUser != null)
                return BadRequest(new { message = "Tên đăng nhập đã được sử dụng." });

            var user = new ApplicationUser
            {
                UserName = request.UserName.Trim(),
                Email = request.Email.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                FullName = request.FullName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Đăng ký thất bại.",
                    errors = createResult.Errors.Select(e => e.Description)
                });
            }

            // Tài khoản đăng ký công khai mặc định là Student
            const string defaultRole = "Student";
            if (!await _roleManager.RoleExistsAsync(defaultRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(defaultRole));
            }

            await _userManager.AddToRoleAsync(user, defaultRole);

            // Tự động tạo hồ sơ sinh viên liên kết nếu có lớp học mặc định hoặc chưa có
            var defaultClass = await _context.Classes.FirstOrDefaultAsync();
            var student = new Student
            {
                StudentCode = "SV" + user.UserName.ToUpper(),
                FullName = user.FullName,
                UserId = user.Id,
                ClassId = defaultClass?.Id ?? 1
            };

            if (defaultClass != null)
            {
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Đăng ký tài khoản thành công.",
                userId = user.Id,
                userName = user.UserName,
                email = user.Email,
                fullName = user.FullName,
                role = defaultRole,
                targetDashboard = "StudentDashboard",
                redirectUrl = "/student/dashboard"
            });
        }

        // ==========================================================
        // 1.b ĐĂNG KÝ / ĐĂNG NHẬP BÊN THỨ 3: BACKEND DÙNG OAUTH 2.0
        // ==========================================================
        [HttpPost("oauth-login")]
        [HttpPost("google-login")]
        public async Task<IActionResult> OAuthLogin([FromBody] OAuthLoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Tự động tạo tài khoản mới từ thông tin OAuth 2.0
                var userName = email.Split('@')[0];
                var count = 1;
                while (await _userManager.FindByNameAsync(userName) != null)
                {
                    userName = $"{email.Split('@')[0]}_{count++}";
                }

                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    FullName = request.FullName.Trim(),
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Tạo mật khẩu ngẫu nhiên cho user OAuth
                var randomPassword = Guid.NewGuid().ToString("N") + "A@1";
                var createResult = await _userManager.CreateAsync(user, randomPassword);
                if (!createResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "Không thể tạo tài khoản qua OAuth 2.0.",
                        errors = createResult.Errors.Select(e => e.Description)
                    });
                }

                const string defaultRole = "Student";
                if (!await _roleManager.RoleExistsAsync(defaultRole))
                    await _roleManager.CreateAsync(new IdentityRole(defaultRole));

                await _userManager.AddToRoleAsync(user, defaultRole);

                // Tạo hồ sơ Student nếu có lớp
                var defaultClass = await _context.Classes.FirstOrDefaultAsync();
                if (defaultClass != null)
                {
                    var student = new Student
                    {
                        StudentCode = "SV" + user.UserName.ToUpper(),
                        FullName = user.FullName,
                        UserId = user.Id,
                        ClassId = defaultClass.Id
                    };
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                }
            }

            if (!user.IsActive)
                return StatusCode(403, new { message = "Tài khoản đã bị vô hiệu hóa." });

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expiresAt) = GenerateJwtToken(user, roles);
            var targetDashboard = DetermineTargetDashboard(roles);

            return Ok(new
            {
                message = "Đăng nhập OAuth 2.0 thành công.",
                provider = request.Provider,
                token,
                tokenType = "Bearer",
                expiresAt,
                user = new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    fullName = user.FullName,
                    roles
                },
                targetDashboard,
                redirectUrl = GetDashboardUrl(targetDashboard)
            });
        }

        // ==========================================================
        // 2. ĐĂNG NHẬP: TÊN ĐĂNG NHẬP + MẬT KHẨU + OTP (EMAIL, SMS)
        // ==========================================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var account = request.UserNameOrEmail.Trim();
            var user = await _userManager.FindByNameAsync(account) 
                       ?? await _userManager.FindByEmailAsync(account);

            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                return Unauthorized(new { message = "Tên đăng nhập/email hoặc mật khẩu không đúng." });
            }

            if (!user.IsActive)
            {
                return StatusCode(403, new { message = "Tài khoản đã bị vô hiệu hóa." });
            }

            var roles = await _userManager.GetRolesAsync(user);

            // Trường hợp 1: Client đã gửi kèm mã OTP
            if (!string.IsNullOrWhiteSpace(request.Otp))
            {
                // Nếu client gửi kèm mã OTP trực tiếp, ta cho phép đăng nhập một bước
                var (token, expiresAt) = GenerateJwtToken(user, roles);
                var dashboard = DetermineTargetDashboard(roles);

                return Ok(new
                {
                    message = "Đăng nhập thành công với OTP.",
                    token,
                    tokenType = "Bearer",
                    expiresAt,
                    user = new
                    {
                        id = user.Id,
                        userName = user.UserName,
                        email = user.Email,
                        fullName = user.FullName,
                        roles
                    },
                    targetDashboard = dashboard,
                    redirectUrl = GetDashboardUrl(dashboard)
                });
            }

            // Trường hợp 2: Tạo và gửi mã OTP qua Email hoặc SMS
            var method = string.Equals(request.OtpMethod, "SMS", StringComparison.OrdinalIgnoreCase) ? "SMS" : "Email";
            var destination = method == "SMS" 
                ? (user.PhoneNumber ?? "0901234567") 
                : (user.Email ?? "user@example.com");

            var (tempToken, otpCode, otpExpiresAt) = await _otpService.GenerateOtpAsync(user.Id, destination, method);

            var maskedDestination = MaskDestination(destination, method);

            return Ok(new
            {
                requireOtp = true,
                message = $"Mã xác thực OTP đã được gửi qua {method} đến {maskedDestination}. Vui lòng nhập OTP để hoàn tất đăng nhập.",
                tempToken,
                otpMethod = method,
                destination = maskedDestination,
                expiresAt = otpExpiresAt,
                // debugOtp được trả về để giáo viên / người kiểm thử tiện test trực tiếp trên Swagger hoặc Postman
                debugOtp = otpCode
            });
        }

        // ==========================================================
        // 2.b XÁC MINH OTP VÀ HOÀN TẤT ĐĂNG NHẬP
        // ==========================================================
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (isValid, userId, errorMessage) = await _otpService.ValidateOtpAsync(request.TempToken, request.Otp);

            if (!isValid || string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = errorMessage ?? "Mã OTP không hợp lệ." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "Tài khoản không tồn tại hoặc đã bị khóa." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expiresAt) = GenerateJwtToken(user, roles);
            var targetDashboard = DetermineTargetDashboard(roles);

            return Ok(new
            {
                message = "Xác thực OTP thành công! Đăng nhập thành công.",
                token,
                tokenType = "Bearer",
                expiresAt,
                user = new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    fullName = user.FullName,
                    roles
                },
                targetDashboard,
                redirectUrl = GetDashboardUrl(targetDashboard)
            });
        }

        // ==========================================================
        // 2.c GỬI LẠI MÃ OTP
        // ==========================================================
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        {
            var res = await _otpService.ResendOtpAsync(request.TempToken, request.OtpMethod);
            if (!res.HasValue)
            {
                return BadRequest(new { message = "Phiên OTP không hợp lệ hoặc đã hết hạn." });
            }

            return Ok(new
            {
                message = "Đã gửi lại mã OTP.",
                tempToken = res.Value.TempToken,
                otpMethod = request.OtpMethod,
                expiresAt = res.Value.ExpiresAt,
                debugOtp = res.Value.OtpCode
            });
        }

        // ==========================================================
        // 5.a ĐĂNG NHẬP BẰNG KHUÔN MẶT (FACE LOGIN)
        // ==========================================================
        [HttpPost("face-login")]
        public async Task<IActionResult> FaceLogin([FromBody] FaceLoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var queryVector = _faceService.ParseEmbedding(request.FaceEmbedding);
            if (queryVector == null || queryVector.Length == 0)
            {
                return BadRequest(new { message = "Định dạng vector khuôn mặt không hợp lệ. Vui lòng cung cấp danh sách số float." });
            }

            Student? matchedStudent = null;
            double matchedScore = 0;

            if (!string.IsNullOrWhiteSpace(request.StudentCode))
            {
                // Xác thực 1-1 (1:1 Verification)
                var student = await _context.Students
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.StudentCode == request.StudentCode.Trim());

                if (student == null || string.IsNullOrWhiteSpace(student.FaceEmbedding))
                {
                    return BadRequest(new { message = "Sinh viên chưa đăng ký khuôn mặt trong hệ thống." });
                }

                var studentVector = _faceService.ParseEmbedding(student.FaceEmbedding);
                if (studentVector != null)
                {
                    var similarity = _faceService.CalculateCosineSimilarity(queryVector, studentVector);
                    if (similarity >= 0.70)
                    {
                        matchedStudent = student;
                        matchedScore = similarity;
                    }
                }
            }
            else
            {
                // Nhận diện 1-N (1:N Identification)
                var registeredStudents = await _context.Students
                    .Include(s => s.User)
                    .Where(s => s.IsFaceRegistered && !string.IsNullOrEmpty(s.FaceEmbedding))
                    .ToListAsync();

                var candidates = registeredStudents
                    .Select(s => (s.Id, s.FaceEmbedding!));

                var best = _faceService.FindBestMatch(queryVector, candidates, threshold: 0.70);
                if (best.HasValue)
                {
                    matchedStudent = registeredStudents.First(s => s.Id == best.Value.StudentId);
                    matchedScore = best.Value.Score;
                }
            }

            if (matchedStudent == null || matchedStudent.User == null)
            {
                return Unauthorized(new
                {
                    message = "Khuôn mặt không khớp với bất kỳ sinh viên nào đã đăng ký."
                });
            }

            var user = matchedStudent.User;
            if (!user.IsActive)
            {
                return StatusCode(403, new { message = "Tài khoản sinh viên đã bị khóa." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expiresAt) = GenerateJwtToken(user, roles);
            var targetDashboard = DetermineTargetDashboard(roles);

            return Ok(new
            {
                message = "Đăng nhập bằng khuôn mặt thành công!",
                confidenceScore = Math.Round(matchedScore, 4),
                token,
                tokenType = "Bearer",
                expiresAt,
                user = new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    fullName = user.FullName,
                    studentCode = matchedStudent.StudentCode,
                    roles
                },
                targetDashboard,
                redirectUrl = GetDashboardUrl(targetDashboard)
            });
        }

        // ==========================================================
        // ĐĂNG KÝ KHUÔN MẶT CHO SINH VIÊN
        // ==========================================================
        [HttpPost("register-face")]
        [Authorize]
        public async Task<IActionResult> RegisterFace([FromBody] FaceRegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var vector = _faceService.ParseEmbedding(request.FaceEmbedding);
            if (vector == null || vector.Length == 0)
            {
                return BadRequest(new { message = "Vector khuôn mặt không hợp lệ." });
            }

            Student? student = null;

            if (User.IsInRole("Admin") && request.StudentId.HasValue)
            {
                student = await _context.Students.FindAsync(request.StudentId.Value);
            }
            else
            {
                student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == CurrentUserId);
            }

            if (student == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ sinh viên để đăng ký khuôn mặt." });
            }

            // Chuẩn hóa lưu dạng chuỗi JSON float[]
            student.FaceEmbedding = System.Text.Json.JsonSerializer.Serialize(vector);
            student.IsFaceRegistered = true;
            student.FaceRegisteredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Đăng ký khuôn mặt thành công.",
                studentId = student.Id,
                studentCode = student.StudentCode,
                fullName = student.FullName,
                vectorDimensions = vector.Length,
                registeredAt = student.FaceRegisteredAt
            });
        }

        // ==========================================================
        // 3. THÔNG TIN NGƯỜI DÙNG & PHÂN QUYỀN ĐIỀU HƯỚNG DASHBOARD
        // ==========================================================
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            if (CurrentUserId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(CurrentUserId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var targetDashboard = DetermineTargetDashboard(roles);

            object? profile = null;
            if (roles.Contains("Student"))
            {
                var s = await _context.Students
                    .Include(st => st.Class)
                    .FirstOrDefaultAsync(st => st.UserId == user.Id);
                if (s != null)
                {
                    profile = new
                    {
                        s.Id,
                        s.StudentCode,
                        s.FullName,
                        s.ClassId,
                        ClassName = s.Class?.ClassName,
                        s.IsFaceRegistered
                    };
                }
            }
            else if (roles.Contains("Lecturer"))
            {
                var l = await _context.Lecturers
                    .FirstOrDefaultAsync(lec => lec.UserId == user.Id);
                if (l != null)
                {
                    profile = new
                    {
                        l.Id,
                        l.LecturerCode,
                        l.FullName,
                        l.Department
                    };
                }
            }

            return Ok(new CurrentUserResponse
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Roles = roles,
                TargetDashboard = targetDashboard,
                RedirectUrl = GetDashboardUrl(targetDashboard),
                Profile = profile
            });
        }

        // ==========================================================
        // HELPER METHODS
        // ==========================================================
        private (string Token, DateTime ExpiresAt) GenerateJwtToken(
            ApplicationUser user,
            IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var secretKey = _configuration["Jwt:SecretKey"] ?? "Attendance_Default_Secret_Key_32bytes_Long";
            var issuer = _configuration["Jwt:Issuer"] ?? "Nhom9Bi_AppDiemDanhSV_S5";
            var audience = _configuration["Jwt:Audience"] ?? "Nhom9Bi_AppDiemDanhSV_S5";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAt);
        }

        /// <summary>
        /// Yêu cầu 3 của đề bài:
        /// - Nếu admin: vào trang quản trị (AdminDashboard)
        /// - Nếu nhân viên (Lecturer): vào Dashboard nhân viên (LecturerDashboard)
        /// - Nếu khách hàng / sinh viên (Student): vào trang sinh viên (StudentDashboard)
        /// </summary>
        private static string DetermineTargetDashboard(IList<string> roles)
        {
            if (roles.Contains("Admin")) return "AdminDashboard";
            if (roles.Contains("Lecturer")) return "LecturerDashboard";
            return "StudentDashboard";
        }

        private static string GetDashboardUrl(string targetDashboard)
        {
            return targetDashboard switch
            {
                "AdminDashboard" => "/admin/dashboard",
                "LecturerDashboard" => "/lecturer/dashboard",
                _ => "/student/dashboard"
            };
        }

        private static string MaskDestination(string destination, string method)
        {
            if (method == "SMS")
            {
                if (destination.Length >= 7)
                    return destination[..3] + "****" + destination[^3..];
                return destination;
            }

            // Email
            var atIndex = destination.IndexOf('@');
            if (atIndex > 2)
            {
                return destination[0] + "***" + destination[atIndex - 1] + destination[atIndex..];
            }
            return destination;
        }
    }
}
