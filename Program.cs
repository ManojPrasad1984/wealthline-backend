using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wealthline.Functions.Functions.Data;
using Wealthline.Functions.Functions.Services;
using QuestPDF.Infrastructure;
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // ✅ Configuration (local.settings.json / Azure)
        var configuration = context.Configuration;

        // ✅ DB Context (SQL Server)
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration["SqlConnection"]
            ));

        // ✅ Services
        services.AddScoped<AgentService>();
        // ✅ Swagger / OpenAPI
        services.AddEndpointsApiExplorer();

        // ✅ CORS (for React / Static Web App)
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

    })
    .Build();
QuestPDF.Settings.License = LicenseType.Community;
host.Run();