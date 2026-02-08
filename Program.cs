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
builder.Services.AddScoped<IFlightOrderService, FlightOrderService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
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

// CORS middleware - ДОЛЖНО БЫТЬ ПОСЛЕ UseRouting и ДО UseAuthorization
app.UseCors("AllowAll");


app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();