
using LicenseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;


QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Register Controllers
builder.Services.AddControllers();

// Register Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Database Context
builder.Services.AddDbContext<LicenseManagementContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("LicenseManagementDB")
    )
);



var app = builder.Build();

// Development Configuration
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS
app.UseHttpsRedirection();

// Serve default HTML files
app.UseDefaultFiles();

// Serve static files from wwwroot
app.UseStaticFiles();

// Authorization
app.UseAuthorization();

// Map API Controllers
app.MapControllers();

app.Run();
