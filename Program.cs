using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Services;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация логгирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Добавление контроллеров
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddControllers();

// HTTP клиент
builder.Services.AddHttpClient();

// Кэширование
builder.Services.AddMemoryCache();

// Сессии
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // Или None для кросдоменных запросов
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Или Always для HTTPS
});

// CORS - ДОЛЖНО БЫТЬ ЗДЕСЬ, в ConfigureServices
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// База данных
builder.Services.AddDbContext<TripWiseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрация сервисов
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IFileService, FileService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });


// API сервисы
builder.Services.AddScoped<RzdApiService>();
builder.Services.AddHttpClient<RzdApiService>();

// Авиабилеты - RealisticFlightService
builder.Services.AddScoped<IFlightService, RealisticFlightService>();

// Сборка приложения
var app = builder.Build();

// Конфигурация пайплайна HTTP запросов
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("AllowAll");


app.UseSession();
app.UseAuthorization();

// В Program.cs, перед app.MapControllerRoute():
// В Program.cs, перед app.MapControllerRoute():
app.Use(async (context, next) =>
{
    // Проверяем, есть ли кука "Запомнить меня"
    if (context.Session.GetInt32("UserId") == null)
    {
        var authToken = context.Request.Cookies["AuthToken"];
        var rememberMe = context.Request.Cookies["RememberMe"];
        var userEmail = context.Request.Cookies["UserEmail"];

        if (rememberMe == "true" && !string.IsNullOrEmpty(authToken))
        {
            try
            {
                using var scope = context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TripWiseContext>();

                // Находим токен в БД
                var userToken = await dbContext.UserAuthTokens
                    .Include(t => t.User)
                    .ThenInclude(u => u.IdRoleNavigation)
                    .FirstOrDefaultAsync(t =>
                        t.Token == authToken &&
                        t.ExpiresAt > DateTime.UtcNow);

                if (userToken != null && userToken.User != null)
                {
                    // Восстанавливаем сессию
                    context.Session.SetInt32("UserId", userToken.User.IdUser);
                    context.Session.SetString("UserName", $"{userToken.User.LastName} {userToken.User.FirstName}");
                    context.Session.SetString("UserEmail", userToken.User.Email);
                    context.Session.SetInt32("UserRole", userToken.User.IdRole);

                    // Также устанавливаем куку UserEmail на будущее
                    context.Response.Cookies.Append("UserEmail", userToken.User.Email,
                        new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(30),
                            HttpOnly = true,
                            IsEssential = true
                        });
                }
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Ошибка автоматического входа");
            }
        }
    }

    await next();
});
app.MapControllerRoute(
    name: "favorites",
    pattern: "Favorites",
    defaults: new { controller = "FavoritesPage", action = "Index" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();