using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers(); // для Web API

builder.Services.AddDbContext<TripWiseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Добавьте эту строку для сессий
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Добавьте эти строки для API и HttpClient
builder.Services.AddHttpClient<RzdApiService>();
builder.Services.AddScoped<RzdApiService>();

// === ДОБАВЬТЕ ЭТО ДЛЯ АВИАБИЛЕТОВ ===
builder.Services.AddHttpClient<IAviasalesServiceV2, AviasalesServiceV2>();
builder.Services.AddScoped<IAviasalesServiceV2, AviasalesServiceV2>();
// Настройка CORS (уже есть выше, но можно обновить если нужно)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// === КОНЕЦ ДОБАВЛЕНИЯ ДЛЯ АВИАБИЛЕТОВ ===

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

app.UseAuthorization();

// Добавьте CORS (используйте ту политику, которую определили выше)
app.UseCors("AllowAll");

// Добавьте эту строку для использования сессий
app.UseSession();

// Map both MVC controllers and API controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); // Это для API контроллеров

app.Run();