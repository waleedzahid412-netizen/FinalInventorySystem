using System;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using InventorySystem.Configuration;
using InventorySystem.Data;
using InventorySystem.Helpers;
using InventorySystem.Middleware;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Implementations;
using InventorySystem.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ===== DATABASE =====
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ===== CONFIGURATION BINDING =====
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
builder.Services.Configure<JwtSettings>(jwtSettingsSection);
var jwtSettings = jwtSettingsSection.Get<JwtSettings>() ?? new JwtSettings();
builder.Services.Configure<InvoicePrintSettings>(
    builder.Configuration.GetSection(InvoicePrintSettings.SectionName));
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection(SecuritySettings.SectionName));
var securitySettings = builder.Configuration.GetSection(SecuritySettings.SectionName).Get<SecuritySettings>()
    ?? new SecuritySettings();

builder.Services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();

builder.Services.AddRateLimiter(options =>
{
    var loginRateLimit = securitySettings.LoginRateLimit;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginRateLimit.PermitLimit,
                Window = TimeSpan.FromSeconds(loginRateLimit.WindowSeconds),
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var http = context.HttpContext;
        if (http.Request.Method == HttpMethods.Post &&
            http.Request.Path.StartsWithSegments("/Auth/Login"))
        {
            http.Response.Redirect("/Auth/Login?rateLimited=1");
            return;
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await http.Response.WriteAsync(UserFacingErrorMessages.LoginRateLimited, cancellationToken);
    };
});

// ===== AUTHENTICATION & AUTHORIZATION =====
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var key = Encoding.ASCII.GetBytes(jwtSettings.SecretKey);
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Extract JWT from HTTP-only cookie. Expired/invalid tokens must not
    // surface as 500s — redirect browsers to login and return 401 for AJAX.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.TryGetValue(JwtCookieHelper.CookieName, out var token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            // JwtBearer rethrows unless Result is set. Fail so [Authorize]
            // challenges and OnChallenge can send the user to login.
            if (!context.Response.HasStarted)
            {
                JwtCookieHelper.DeleteToken(context.Response);
            }

            context.Fail(context.Exception);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            context.HandleResponse();

            if (!JwtCookieHelper.IsBrowserNavigation(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(
                    "{\"success\":false,\"message\":\"Your session has expired. Please sign in again.\"}");
            }

            context.Response.Redirect(
                JwtCookieHelper.BuildLoginRedirect(context.Request, context.AuthenticateFailure));
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// ===== DEPENDENCY INJECTION =====
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IUserPermissionContext, UserPermissionContext>();
builder.Services.AddScoped<InventorySystem.Filters.PermissionAuthorizationFilter>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserCompanyAccessService, UserCompanyAccessService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IUnitRepository, UnitRepository>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepository>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IFifoCostingService, FifoCostingService>();
builder.Services.AddScoped<ISalesRepository, SalesRepository>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IPromotionDiscountService, PromotionDiscountService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReturnRepository, ReturnRepository>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ICompanyStockReportRepository, CompanyStockReportRepository>();
builder.Services.AddScoped<ICompanyStockReportService, CompanyStockReportService>();
builder.Services.AddScoped<IBookerRepository, BookerRepository>();
builder.Services.AddScoped<IBookerService, BookerService>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ILoadSheetService, LoadSheetService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICompanyContext, CompanyContext>();
builder.Services.AddScoped<ICompanyScopeCookieService, CompanyScopeCookieService>();

// Configure QuestPDF License
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add MVC Controllers & Views
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<InventorySystem.Filters.PermissionAuthorizationFilter>();
    options.Filters.Add<InventorySystem.Filters.MissingUserIdentityExceptionFilter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<CompanyScopeAutoDefaultMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
