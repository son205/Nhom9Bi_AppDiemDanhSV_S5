using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Nhom9Bi_AppDiemDanhSV_S5.Data;
using Nhom9Bi_AppDiemDanhSV_S5.Models;
using Nhom9Bi_AppDiemDanhSV_S5.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers & JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 2. Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Memory Cache & Application Services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IOtpService, OtpService>();
builder.Services.AddSingleton<IQrAttendanceService, QrAttendanceService>();
builder.Services.AddSingleton<IFaceRecognitionService, FaceRecognitionService>();

// 4. CORS configuration (Hỗ trợ gọi từ Mobile / Frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDataProtection();

// 5. Identity Configuration
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 6. JWT Configuration
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "TBIVLVHSTHWQHLRKLUWTPWPORHUOLRPGXOVNVSQTQTACOEKSC";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Nhom9Bi_AppDiemDanhSV_S5";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Nhom9Bi_AppDiemDanhSV_S5";

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey phai co it nhat 32 byte.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// 7. Swagger Documentation with JWT Authorization Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nhom9Bi Attendance API",
        Version = "v1",
        Description = "Hệ thống Backend API App Điểm Danh Sinh Viên.\n\n" +
                      "- 1. Đăng ký tài khoản (Email, OAuth 2.0 / Google)\n" +
                      "- 2. Đăng nhập (Username/Password + OTP qua Email/SMS)\n" +
                      "- 3. Phân quyền người dùng (Admin, Giảng viên/Nhân viên, Sinh viên) và điều hướng Dashboard\n" +
                      "- 4. CRUD đầy đủ (Lớp học, Môn học, Giảng viên, Sinh viên, Buổi học, Điểm danh)\n" +
                      "- 5. 3 chức năng nâng cao: (a) Đăng nhập khuôn mặt, (b) Chia sẻ vị trí GPS, (e) Nhận diện QR Code."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token để xác thực: Bearer {token}"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

// 8. Khởi tạo Seed Data (Role, Admin, Lớp học & Dữ liệu mẫu nếu cần)
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    // 8.1 Tạo các Roles
    string[] roles = { "Admin", "Lecturer", "Student" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // 8.2 Tạo tài khoản Admin mặc định
    var adminUserName = configuration["AdminSeed:UserName"] ?? "admin";
    var adminEmail = configuration["AdminSeed:Email"] ?? "admin@example.com";
    var adminPassword = configuration["AdminSeed:Password"] ?? "Admin@123456";

    var adminUser = await userManager.FindByNameAsync(adminUserName);
    if (adminUser == null)
    {
        var existingEmail = await userManager.FindByEmailAsync(adminEmail);
        if (existingEmail == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine(">>> Đã khởi tạo tài khoản Admin mặc định (admin / Admin@123456)");
            }
        }
    }
    else if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // 8.3 Tạo dữ liệu lớp học và môn học mẫu nếu database chưa có
    StudentClass? sampleClass = await dbContext.Classes.FirstOrDefaultAsync();
    if (sampleClass == null)
    {
        sampleClass = new StudentClass
        {
            ClassCode = "CNTT-K15",
            ClassName = "Công nghệ thông tin K15",
            AcademicYear = "2023-2027"
        };
        dbContext.Classes.Add(sampleClass);
        await dbContext.SaveChangesAsync();

        var sampleCourse = new Course
        {
            CourseCode = "MBL301",
            CourseName = "Lập trình ứng dụng di động",
            Credits = 3
        };
        dbContext.Courses.Add(sampleCourse);
        await dbContext.SaveChangesAsync();

        Console.WriteLine(">>> Đã tạo lớp học và môn học mẫu.");
    }

    // 8.4 Tạo tài khoản Giảng viên mẫu nếu chưa có
    var lecturerUserName = "lecturer";
    var lecturerEmail = "lecturer@example.com";
    var lecturerUser = await userManager.FindByNameAsync(lecturerUserName);
    if (lecturerUser == null)
    {
        lecturerUser = new ApplicationUser
        {
            UserName = lecturerUserName,
            Email = lecturerEmail,
            EmailConfirmed = true,
            FullName = "ThS. Nguyen Van Giang Vien",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var createResult = await userManager.CreateAsync(lecturerUser, "Lecturer@123456");
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(lecturerUser, "Lecturer");
            var lecturerRecord = new Lecturer
            {
                LecturerCode = "GV001",
                FullName = lecturerUser.FullName,
                Department = "Khoa Cong nghe Thong tin",
                UserId = lecturerUser.Id
            };
            dbContext.Lecturers.Add(lecturerRecord);
            await dbContext.SaveChangesAsync();
            Console.WriteLine(">>> Đã tạo tài khoản Giảng viên mẫu (lecturer / Lecturer@123456)");
        }
    }
}

// 9. Pipeline Configuration
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nhom9Bi Attendance API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
