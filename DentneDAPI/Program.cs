using DentneDAPI.Services;
using Microsoft.Data.SqlClient;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseService>();

var app = builder.Build();

// Simple test endpoint
//app.MapGet("/", () => "DentneD API is running!");

// Your existing endpoints
app.MapPost("/api/appointments/book", async (AppointmentRequest request, IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DentneDConnection");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Get or create default doctor (with all required fields)
        int doctorId;
        using var doctorCommand = new SqlCommand(
            "SELECT ISNULL((SELECT TOP 1 doctors_id FROM doctors), 0)", connection);
        var existingDoctorId = await doctorCommand.ExecuteScalarAsync();

        if (Convert.ToInt32(existingDoctorId) == 0)
        {
            // Create default doctor with all required fields
            using var createDoctorCommand = new SqlCommand(
                "INSERT INTO doctors (doctors_name, doctors_surname, doctors_doctext, doctors_username, doctors_password) OUTPUT INSERTED.doctors_id VALUES ('Default', 'Dentist', 'General Dentist', 'default', 'password123')",
                connection);
            doctorId = Convert.ToInt32(await createDoctorCommand.ExecuteScalarAsync());
        }
        else
        {
            doctorId = Convert.ToInt32(existingDoctorId);
        }

        // Get or create default room
        int roomId;
        using var roomCommand = new SqlCommand(
            "SELECT ISNULL((SELECT TOP 1 rooms_id FROM rooms), 0)", connection);
        var existingRoomId = await roomCommand.ExecuteScalarAsync();

        if (Convert.ToInt32(existingRoomId) == 0)
        {
            // Create default room
            using var createRoomCommand = new SqlCommand(
                "INSERT INTO rooms (rooms_name, rooms_color) OUTPUT INSERTED.rooms_id VALUES ('Exam Room 1', 'B')",
                connection);
            roomId = Convert.ToInt32(await createRoomCommand.ExecuteScalarAsync());
        }
        else
        {
            roomId = Convert.ToInt32(existingRoomId);
        }

        // Create patient if they don't exist, or get existing patient
        int patientId;
        using var patientCheckCommand = new SqlCommand(
            "SELECT patients_id FROM patients WHERE patients_name = @FirstName AND patients_surname = @LastName",
            connection);
        patientCheckCommand.Parameters.AddWithValue("@FirstName", request.PatientFirstName);
        patientCheckCommand.Parameters.AddWithValue("@LastName", request.PatientLastName);

        var existingPatientId = await patientCheckCommand.ExecuteScalarAsync();

        if (existingPatientId != null)
        {
            patientId = Convert.ToInt32(existingPatientId);
        }
        else
        {
            // Create new patient
            using var createPatientCommand = new SqlCommand(
                "INSERT INTO patients (patients_name, patients_surname, patients_birthdate) OUTPUT INSERTED.patients_id VALUES (@FirstName, @LastName, @BirthDate)",
                connection);
            createPatientCommand.Parameters.AddWithValue("@FirstName", request.PatientFirstName);
            createPatientCommand.Parameters.AddWithValue("@LastName", request.PatientLastName);
            createPatientCommand.Parameters.AddWithValue("@BirthDate", DBNull.Value);
            patientId = Convert.ToInt32(await createPatientCommand.ExecuteScalarAsync());
        }

        // Create appointment with all required fields
        var appointmentDateTime = request.AppointmentDate;
        if (!string.IsNullOrEmpty(request.AppointmentTime))
        {
            if (TimeSpan.TryParse(request.AppointmentTime.Replace(" AM", "").Replace(" PM", ""), out var time))
                appointmentDateTime = appointmentDateTime.Date.Add(time);
        }

        using var appointmentCommand = new SqlCommand(
            @"INSERT INTO appointments (patients_id, doctors_id, rooms_id, appointments_from, appointments_to, appointments_procedure, appointments_notes) 
              OUTPUT INSERTED.appointments_id 
              VALUES (@PatientId, @DoctorId, @RoomId, @AppointmentFrom, @AppointmentTo, @Procedure, @Notes)",
            connection);

        appointmentCommand.Parameters.AddWithValue("@PatientId", patientId);
        appointmentCommand.Parameters.AddWithValue("@DoctorId", doctorId);
        appointmentCommand.Parameters.AddWithValue("@RoomId", roomId);
        appointmentCommand.Parameters.AddWithValue("@AppointmentFrom", appointmentDateTime);
        appointmentCommand.Parameters.AddWithValue("@AppointmentTo", appointmentDateTime.AddHours(1));
        appointmentCommand.Parameters.AddWithValue("@Procedure", request.Procedure ?? "Dental Checkup");
        appointmentCommand.Parameters.AddWithValue("@Notes", request.Notes ?? "");

        var appointmentId = await appointmentCommand.ExecuteScalarAsync();

        return Results.Ok(new
        {
            success = true,
            message = "Appointment booked successfully",
            appointmentId = appointmentId,
            patientId = patientId,
            doctorId = doctorId,
            roomId = roomId
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