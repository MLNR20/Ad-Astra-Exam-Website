using AAExamManagementSystem.Data;
using AAExamManagementSystem.Hubs;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Models.Mapping;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Keep local and unspecified-kind DateTime values working: Npgsql 6+ otherwise only accepts UTC DateTimes.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Secrets such as the Gmail app password live in a git-ignored .env at the repository root.
builder.Configuration.AddEnvFile(builder.Environment.ContentRootPath);

// Exam slots are booked in local time (Philippine time by default), whatever time zone the host runs in.
ExamClock.Configure(builder.Configuration["Exam:TimeZone"]);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/ApplicantLogin");
    options.Conventions.AllowAnonymousToPage("/Signup");
    options.Conventions.AllowAnonymousToPage("/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/ForgotPasswordConfirmation");
    options.Conventions.AllowAnonymousToPage("/ResetPassword");
    options.Conventions.AllowAnonymousToPage("/ResetPasswordConfirmation");
});
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Login";

    // The auth cookie is short-lived; RefreshTokenMiddleware renews it from the 7-day refresh token.
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;

    // API callers get a status code rather than a redirect to the login page.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddScoped<IClaimsTransformation, ActiveRoleClaimsTransformation>();
builder.Services.AddScoped<ActiveNameValidator>();
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, UniqueActiveNameAdapterProvider>();

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ISectionService, SectionService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IChoiceService, ChoiceService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<QuestionChoiceService>();
builder.Services.AddScoped<IQuestionChoiceService>(sp => sp.GetRequiredService<QuestionChoiceService>());
builder.Services.AddScoped<NotificationService>();

builder.Services.Configure<RefreshTokenSettings>(builder.Configuration.GetSection("RefreshToken"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.PostConfigure<EmailSettings>(settings =>
    settings.Password = builder.Configuration["ad-astra-exam-password"]);
builder.Services.AddSingleton<IRegistrationNotifier, SmtpRegistrationNotifier>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<RefreshTokenMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();
// Anonymous and database-free, so an uptime monitor can keep a free-tier host awake.
app.MapGet("/healthz", () => Results.Ok("healthy"))
   .AllowAnonymous();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
