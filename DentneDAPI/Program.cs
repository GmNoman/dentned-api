using DentneDAPI.Services;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseService>();

var app = builder.Build();

// Simple test endpoint
app.MapGet("/", () => "DentneD API is running!");

// Your existing endpoints
app.MapPost("/api/appointments/book", async (AppointmentRequest request, DatabaseService dbService) =>
{
    try
    {
        // Validate required fields
        if (string.IsNullOrEmpty(request.PatientFirstName) || string.IsNullOrEmpty(request.PatientLastName))
            return Results.BadRequest("First name and last name are required.");

        if (request.AppointmentDate == default)
            return Results.BadRequest("Appointment date is required.");

        if (string.IsNullOrEmpty(request.AppointmentTime))
            return Results.BadRequest("Appointment time is required.");

        // Create patient
        var newPatient = new Patient
        {
            FirstName = request.PatientFirstName,
            LastName = request.PatientLastName
        };

        var patientId = await dbService.CreatePatientAsync(newPatient);

        // Combine date and time
        var appointmentDateTime = request.AppointmentDate.Date;
        if (TimeSpan.TryParse(request.AppointmentTime.Replace(" AM", "").Replace(" PM", ""), out var time))
            appointmentDateTime = appointmentDateTime.Add(time);
        else
            appointmentDateTime = appointmentDateTime.AddHours(10);

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

app.MapGet("/api/patients", async (DatabaseService dbService) =>
{
    try
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
        await connection.OpenAsync();

        var query = "SELECT patients_id, patients_name, patients_surname, patients_birthdate FROM patients";
        using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
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

// Add these endpoints before app.Run()

// Root endpoint
// Root endpoint
app.MapGet("/", () => "Dental API is running! Use these endpoints:\n\n" +
    "/api/patients - Get all patients\n" +
    "/api/treatments - Get all treatments\n" +
    "/api/doctors - Get all doctors\n" +
    "/api/appointments - Get all appointments\n" +
    "/api/appointments/book - Book new appointment");

// Health check endpoint
app.MapGet("/health", () => new {
    status = "healthy",
    timestamp = DateTime.UtcNow,
    message = "Dental API is running successfully"
});

// Test endpoint
app.MapGet("/test", () => new {
    message = "API test successful",
    environment = "Development",
    port = 8080
});

app.Run();

// Model classes (add these at the bottom of the same file)
public class AppointmentRequest
{
    public string? PatientFirstName { get; set; }
    public string? PatientLastName { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string? AppointmentTime { get; set; }
    public string? Procedure { get; set; }
    public string? Notes { get; set; }
}

public class Patient
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class Appointment
{
    public int PatientId { get; set; }
    public DateTime AppointmentFrom { get; set; }
    public string? Procedure { get; set; }
    public string? Notes { get; set; }
}