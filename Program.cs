using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
builder.Services.AddControllers(); // для Web API
builder.Services.AddHttpClient<RzdApiService>(); // для вашего сервиса
builder.Services.AddScoped<RzdApiService>(); // регистрация сервиса

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Добавьте CORS перед UseAuthorization или после
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Добавьте эту строку для использования сессий
app.UseSession();

// Map both MVC controllers and API controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); // Это для API контроллеров (атрибут [ApiController])

app.Run();