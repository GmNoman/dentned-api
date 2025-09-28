using DentneDAPI.Models;
using DentneDAPI.Services;
using Microsoft.Data.SqlClient;

namespace DentneDAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add CORS policy
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // Register services
        builder.Services.AddScoped<DatabaseService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseHttpsRedirection();
        }

        app.UseCors("AllowAll");

        // API Endpoints
        app.MapPost("/api/appointments/book", async (AppointmentRequest request, DatabaseService dbService) =>
        {
            // Your existing booking logic
        });

        // Other endpoints...

        app.Run();
    }
}