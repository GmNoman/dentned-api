using DentneDAPI.Models;
using DentneDAPI.Services;
using Microsoft.Data.SqlClient;

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
    // Only use HTTPS in development
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

// Your existing endpoints (keep all your MapPost, MapGet calls)
app.MapPost("/api/appointments/book", async (AppointmentRequest request, DatabaseService dbService) =>
{
    try
    {
        // Validate required fields
        if (string.IsNullOrEmpty(request.PatientFirstName) || string.IsNullOrEmpty(request.PatientLastName))
        {
            return Results.BadRequest("First name and last name are required.");
        }

        if (request.AppointmentDate == default)
        {
            return Results.BadRequest("Appointment date is required.");
        }

        if (string.IsNullOrEmpty(request.AppointmentTime))
        {
            return Results.BadRequest("Appointment time is required.");
        }

        // Create patient with only essential information
        var newPatient = new Patient
        {
            FirstName = request.PatientFirstName,
            LastName = request.PatientLastName
        };

        var patientId = await dbService.CreatePatientAsync(newPatient);

        // Combine date and time
        var appointmentDateTime = request.AppointmentDate.Date;
        if (TimeSpan.TryParse(request.AppointmentTime.Replace(" AM", "").Replace(" PM", ""), out var time))
        {
            appointmentDateTime = appointmentDateTime.Add(time);
        }
        else
        {
            // Default to 10:00 if time parsing fails
            appointmentDateTime = appointmentDateTime.AddHours(10);
        }

        // Create appointment
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

// Keep all your other endpoints exactly as they are...
app.MapGet("/api/patients", async (DatabaseService dbService) =>
{
    // ... your existing code
});

app.MapGet("/api/treatments", async () =>
{
    // ... your existing code
});

app.MapGet("/api/doctors", async () =>
{
    // ... your existing code
});

app.MapGet("/api/appointments/available", async (DateTime date, DatabaseService dbService) =>
{
    // ... your existing code
});

app.MapGet("/api/appointments", async (DatabaseService dbService) =>
{
    // ... your existing code
});

app.MapGet("/api/appointments/{id}", async (int id, DatabaseService dbService) =>
{
    // ... your existing code
});

// Remove the app.Run("http://0.0.0.0:10000"); line
// Just use:
app.Run();