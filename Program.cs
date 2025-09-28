using DentneDAPI.Models;
using DentneDAPI.Services;
using Microsoft.Data.SqlClient;
using System.Data;

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
        else
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AllowAll");

        // API Endpoints
        app.MapPost("/api/appointments/book", async (AppointmentRequest request, DatabaseService dbService) =>
        {
            try
            {
                if (string.IsNullOrEmpty(request.PatientFirstName) || string.IsNullOrEmpty(request.PatientLastName))
                    return Results.BadRequest("First name and last name are required.");

                if (request.AppointmentDate == default)
                    return Results.BadRequest("Appointment date is required.");

                if (string.IsNullOrEmpty(request.AppointmentTime))
                    return Results.BadRequest("Appointment time is required.");

                var newPatient = new Patient
                {
                    FirstName = request.PatientFirstName,
                    LastName = request.PatientLastName
                };

                var patientId = await dbService.CreatePatientAsync(newPatient);

                var appointmentDateTime = request.AppointmentDate.Date;
                if (TimeSpan.TryParse(request.AppointmentTime.Replace(" AM", "").Replace(" PM", ""), out var time))
                    appointmentDateTime = appointmentDateTime.Add(time);
                else
                    appointmentDateTime = appointmentDateTime.AddHours(10);

                var appointment = new Appointment
                {
                    PatientId = patientId,
                    AppointmentFrom = appointmentDateTime,
                    Procedure = request.Procedure ?? "Dental Appointment",
                    Notes = request.Notes
                };

                var appointmentId = await dbService.CreateAppointmentAsync(appointment);

                return Results.Ok(new
                {
                    success = true,
                    message = "Appointment booked successfully",
                    appointmentId = appointmentId,
                    patientId = patientId
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error booking appointment: {ex.Message}");
            }
        });

        app.MapGet("/api/patients", async (DatabaseService dbService) =>
        {
            try
            {
                using var connection = new SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
                await connection.OpenAsync();

                var query = "SELECT patients_id, patients_name, patients_surname, patients_birthdate FROM patients";
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var patients = new List<object>();
                while (await reader.ReadAsync())
                {
                    patients.Add(new
                    {
                        PatientId = reader.GetInt32("patients_id"),
                        FirstName = reader.IsDBNull("patients_name") ? null : reader.GetString("patients_name"),
                        LastName = reader.IsDBNull("patients_surname") ? null : reader.GetString("patients_surname"),
                        BirthDate = reader.IsDBNull("patients_birthdate") ? (DateTime?)null : reader.GetDateTime("patients_birthdate")
                    });
                }

                return Results.Ok(patients);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving patients: {ex.Message}");
            }
        });

        // Add other endpoints here (treatments, doctors, appointments, etc.)
        // ... [Keep all your other endpoints]

        app.Run();
    }
}