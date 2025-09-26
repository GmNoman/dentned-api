using DentneDAPI.Models;
using DentneDAPI.Services;
using Microsoft.Data.SqlClient;
using System.Data;

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
}

app.UseCors("AllowAll");

// API Endpoints
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

// Simple treatments endpoint (remove the async dbService call)
app.MapGet("/api/treatments", async () =>
{
    try
    {
        using var connection = new SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
        await connection.OpenAsync();

        var query = "SELECT treatments_id, treatments_name, treatments_price FROM treatments";
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        var treatments = new List<object>();
        while (await reader.ReadAsync())
        {
            treatments.Add(new
            {
                TreatmentId = reader.GetInt32("treatments_id"),
                Name = reader.IsDBNull("treatments_name") ? null : reader.GetString("treatments_name"),
                Price = reader.IsDBNull("treatments_price") ? 0 : reader.GetDecimal("treatments_price")
            });
        }

        return Results.Ok(treatments);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving treatments: {ex.Message}");
    }
});

// Simple doctors endpoint (remove the async dbService call)
app.MapGet("/api/doctors", async () =>
{
    try
    {
        using var connection = new SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
        await connection.OpenAsync();

        var query = "SELECT doctors_id, doctors_name FROM doctors";
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        var doctors = new List<object>();
        while (await reader.ReadAsync())
        {
            doctors.Add(new
            {
                DoctorId = reader.GetInt32("doctors_id"),
                Name = reader.IsDBNull("doctors_name") ? null : reader.GetString("doctors_name")
            });
        }

        return Results.Ok(doctors);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving doctors: {ex.Message}");
    }
});

app.MapGet("/api/appointments/available", async (DateTime date, DatabaseService dbService) =>
{
    try
    {
        using var connection = new SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
        await connection.OpenAsync();

        // Check existing appointments for the date
        var query = @"SELECT appointments_from 
                      FROM appointments 
                      WHERE CAST(appointments_from AS DATE) = @Date 
                      AND appointments_from > GETDATE()";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Date", date.Date);

        using var reader = await command.ExecuteReaderAsync();

        var bookedTimes = new List<string>();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull("appointments_from"))
            {
                var appointmentTime = reader.GetDateTime("appointments_from");
                bookedTimes.Add(appointmentTime.ToString("HH:mm"));
            }
        }

        // Define available time slots
        var allSlots = new[] { "09:00", "10:00", "11:00", "12:00", "14:00", "15:00", "16:00", "17:00" };
        var availableSlots = allSlots.Except(bookedTimes).ToList();

        return Results.Ok(new { date = date.ToString("yyyy-MM-dd"), availableSlots });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving available slots: {ex.Message}");
    }
});

// View all appointments
app.MapGet("/api/appointments", async (DatabaseService dbService) =>
{
    try
    {
        using var connection = new SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
        await connection.OpenAsync();

        var query = @"
            SELECT 
                a.appointments_id,
                a.appointments_from,
                a.appointments_to,
                a.appointments_title,
                a.appointments_notes,
                p.patients_id,
                p.patients_name,
                p.patients_surname
            FROM appointments a
            INNER JOIN patients p ON a.patients_id = p.patients_id
            ORDER BY a.appointments_from DESC";

        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        var appointments = new List<object>();
        while (await reader.ReadAsync())
        {
            appointments.Add(new
            {
                AppointmentId = reader.GetInt32("appointments_id"),
                PatientId = reader.GetInt32("patients_id"),
                PatientName = $"{reader.GetString("patients_name")} {reader.GetString("patients_surname")}",
                From = reader.GetDateTime("appointments_from"),
                To = !reader.IsDBNull("appointments_to") ? reader.GetDateTime("appointments_to") : (DateTime?)null,
                Title = reader.IsDBNull("appointments_title") ? null : reader.GetString("appointments_title"),
                Notes = reader.IsDBNull("appointments_notes") ? null : reader.GetString("appointments_notes")
            });
        }

        return Results.Ok(appointments);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving appointments: {ex.Message}");
    }
});

// View specific appointment by ID
app.MapGet("/api/appointments/{id}", async (int id, DatabaseService dbService) =>
{
    try
    {
        using var connection = new SqlConnection(builder.Configuration.GetConnectionString("DentneDConnection"));
        await connection.OpenAsync();

        var query = @"
            SELECT 
                a.appointments_id,
                a.appointments_from,
                a.appointments_to,
                a.appointments_title,
                a.appointments_notes,
                p.patients_id,
                p.patients_name,
                p.patients_surname
            FROM appointments a
            INNER JOIN patients p ON a.patients_id = p.patients_id
            WHERE a.appointments_id = @AppointmentId";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@AppointmentId", id);

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var appointment = new
            {
                AppointmentId = reader.GetInt32("appointments_id"),
                PatientId = reader.GetInt32("patients_id"),
                PatientName = $"{reader.GetString("patients_name")} {reader.GetString("patients_surname")}",
                From = reader.GetDateTime("appointments_from"),
                To = !reader.IsDBNull("appointments_to") ? reader.GetDateTime("appointments_to") : (DateTime?)null,
                Title = reader.IsDBNull("appointments_title") ? null : reader.GetString("appointments_title"),
                Notes = reader.IsDBNull("appointments_notes") ? null : reader.GetString("appointments_notes")
            };

            return Results.Ok(appointment);
        }

        return Results.NotFound($"Appointment with ID {id} not found");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving appointment: {ex.Message}");
    }
});

app.Run();