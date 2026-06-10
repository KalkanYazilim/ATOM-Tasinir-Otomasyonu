using ATOM.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IAtomDataService, FileAtomDataService>();
builder.Services.AddScoped<IBildirimService, BildirimService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStokService, StokService>();
builder.Services.AddScoped<ITasinirKayitService, TasinirKayitService>();
builder.Services.AddHostedService<SeedDataHostedService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/giris";
        options.AccessDeniedPath = "/erisim-yok";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "ATOM.Auth";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapAreaControllerRoute("talep", "Talep", "talep/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("ihale", "Ihale", "ihale/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("kayit", "Kayit", "kayit/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("depo", "Depo", "depo/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("zimmet", "Zimmet", "zimmet/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("hurda", "Hurda", "hurda/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("sayim", "Sayim", "sayim/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("devir", "Devir", "devir/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("raporlama", "Raporlama", "raporlama/{controller=Home}/{action=Index}/{id?}");
app.MapAreaControllerRoute("yonetim", "Yonetim", "yonetim/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
