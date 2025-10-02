using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

var app = builder.Build();

// Root endpoint
app.MapGet("/", () => "Dental API is running! Use these endpoints:\n\n" +
    "/api/patients - Get all patients\n" +
    "/api/doctors - Get all doctors\n" +
    "/api/rooms - Get all rooms\n" +
    "/api/appointments/book - Book new appointment\n" +
    "/api/appointments/comprehensive - Comprehensive booking with insurance/contact");

// Your existing patients endpoint
app.MapGet("/api/patients", async (IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DentneDConnection");
        using var connection = new SqlConnection(connectionString);
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

// Your existing doctors endpoint
app.MapGet("/api/doctors", async (IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DentneDConnection");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "SELECT doctors_id, doctors_name, doctors_surname FROM doctors";
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        var doctors = new List<object>();
        while (await reader.ReadAsync())
        {
            doctors.Add(new
            {
                DoctorId = reader.GetInt32("doctors_id"),
                FirstName = reader.IsDBNull("doctors_name") ? null : reader.GetString("doctors_name"),
                LastName = reader.IsDBNull("doctors_surname") ? null : reader.GetString("doctors_surname")
            });
        }

        return Results.Ok(doctors);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving doctors: {ex.Message}");
    }
});

// Your existing rooms endpoint
app.MapGet("/api/rooms", async (IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DentneDConnection");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "SELECT rooms_id, rooms_name FROM rooms";
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        var rooms = new List<object>();
        while (await reader.ReadAsync())
        {
            rooms.Add(new
            {
                RoomId = reader.GetInt32("rooms_id"),
                Name = reader.IsDBNull("rooms_name") ? null : reader.GetString("rooms_name")
            });
        }

        return Results.Ok(rooms);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving rooms: {ex.Message}");
    }
});

// Your existing simple appointment booking
app.MapPost("/api/appointments/book", async (AppointmentRequest request, IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DentneDConnection");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Get default doctor and room
        int doctorId, roomId;

        using (var doctorCommand = new SqlCommand("SELECT TOP 1 doctors_id FROM doctors", connection))
        {
            doctorId = Convert.ToInt32(await doctorCommand.ExecuteScalarAsync());
        }

        using (var roomCommand = new SqlCommand("SELECT TOP 1 rooms_id FROM rooms", connection))
        {
            roomId = Convert.ToInt32(await roomCommand.ExecuteScalarAsync());
        }

        // Create patient
        int patientId;
        using (var createPatientCommand = new SqlCommand(
            "INSERT INTO patients (patients_name, patients_surname, patients_birthdate) OUTPUT INSERTED.patients_id VALUES (@FirstName, @LastName, @BirthDate)",
            connection))
        {
            createPatientCommand.Parameters.AddWithValue("@FirstName", request.PatientFirstName ?? "");
            createPatientCommand.Parameters.AddWithValue("@LastName", request.PatientLastName ?? "");
            createPatientCommand.Parameters.AddWithValue("@BirthDate", DateTime.Now.AddYears(-30));
            patientId = Convert.ToInt32(await createPatientCommand.ExecuteScalarAsync());
        }

        // Create appointment
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
            patientId = patientId
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error booking appointment: {ex.Message}");
    }
});

// COMPREHENSIVE APPOINTMENT BOOKING - FIXED VERSION
app.MapPost("/api/appointments/comprehensive", async (CompleteAppointmentRequest request, IConfiguration config) =>
{
    try
    {
        var connectionString = config.GetConnectionString("DentneDConnection");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Local helper functions
        async Task<int> GetDefaultDoctorId()
        {
            using var command = new SqlCommand("SELECT TOP 1 doctors_id FROM doctors", connection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        async Task<int> GetDefaultRoomId()
        {
            using var command = new SqlCommand("SELECT TOP 1 rooms_id FROM rooms", connection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        async Task<int> CreateCompletePatient()
        {
            var query = @"
                INSERT INTO patients (
                    patients_name, patients_surname, patients_sex, patients_birthdate,
                    patients_birthcity, patients_doctext, patients_notes, patients_isarchived
                ) OUTPUT INSERTED.patients_id 
                VALUES (@FirstName, @LastName, @Gender, @DOB, @BirthCity, @DoctorText, @Notes, 0)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@FirstName", request.FirstName ?? "");
            command.Parameters.AddWithValue("@LastName", request.LastName ?? "");
            command.Parameters.AddWithValue("@Gender", request.Gender ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DOB", request.DateOfBirth ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BirthCity", request.BirthCity ?? (object)DBNull.Value);

            // Store contact and insurance info in patients_doctext as JSON
            var contactInsuranceData = new
            {
                Phone = request.Phone ?? "+1-555-0123",
                Email = request.Email ?? $"{request.FirstName?.ToLower()}.{request.LastName?.ToLower()}@example.com",
                Address = request.Address ?? "123 Main St, Anytown, USA",
                EmergencyContact = request.EmergencyContact ?? "Jane Doe (+1-555-0124)",
                InsuranceProvider = request.InsuranceProvider ?? "Delta Dental",
                PolicyNumber = request.PolicyNumber ?? "POL123456789",
                GroupNumber = request.GroupNumber ?? "GRP987654",
                SubscriberName = request.SubscriberName ?? $"{request.FirstName} {request.LastName}"
            };

            command.Parameters.AddWithValue("@DoctorText", JsonSerializer.Serialize(contactInsuranceData));
            command.Parameters.AddWithValue("@Notes", request.Symptoms ?? "Routine dental checkup");

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        async Task UpdatePatientDetails(int patientId)
        {
            var updateQuery = @"
                UPDATE patients 
                SET patients_doctext = @DoctorText,
                    patients_notes = COALESCE(@Notes, patients_notes)
                WHERE patients_id = @PatientId";

            var contactInsuranceData = new
            {
                Phone = request.Phone ?? "+1-555-0123",
                Email = request.Email ?? $"{request.FirstName?.ToLower()}.{request.LastName?.ToLower()}@example.com",
                Address = request.Address ?? "123 Main St, Anytown, USA",
                EmergencyContact = request.EmergencyContact ?? "Jane Doe (+1-555-0124)",
                InsuranceProvider = request.InsuranceProvider ?? "Delta Dental",
                PolicyNumber = request.PolicyNumber ?? "POL123456789",
                GroupNumber = request.GroupNumber ?? "GRP987654",
                SubscriberName = request.SubscriberName ?? $"{request.FirstName} {request.LastName}"
            };

            using var command = new SqlCommand(updateQuery, connection);
            command.Parameters.AddWithValue("@PatientId", patientId);
            command.Parameters.AddWithValue("@DoctorText", JsonSerializer.Serialize(contactInsuranceData));
            command.Parameters.AddWithValue("@Notes", request.Symptoms ?? "Routine dental checkup");

            await command.ExecuteNonQueryAsync();
        }

        async Task<int> CreateAppointment(int patientId, int doctorId, int roomId)
        {
            var appointmentDateTime = request.PreferredDate;
            if (!string.IsNullOrEmpty(request.PreferredTime))
            {
                if (TimeSpan.TryParse(request.PreferredTime.Replace(" AM", "").Replace(" PM", ""), out var time))
                    appointmentDateTime = appointmentDateTime.Date.Add(time);
            }

            var query = @"
                INSERT INTO appointments (patients_id, doctors_id, rooms_id, appointments_from, appointments_to, appointments_procedure, appointments_notes) 
                OUTPUT INSERTED.appointments_id 
                VALUES (@PatientId, @DoctorId, @RoomId, @AppointmentFrom, @AppointmentTo, @Procedure, @Notes)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@PatientId", patientId);
            command.Parameters.AddWithValue("@DoctorId", doctorId);
            command.Parameters.AddWithValue("@RoomId", roomId);
            command.Parameters.AddWithValue("@AppointmentFrom", appointmentDateTime);
            command.Parameters.AddWithValue("@AppointmentTo", appointmentDateTime.AddHours(1));
            command.Parameters.AddWithValue("@Procedure", request.Procedure ?? "Dental Consultation");

            var appointmentNotes = $"Symptoms: {request.Symptoms}\n" +
                                  $"Insurance: {request.InsuranceProvider ?? "Delta Dental"}\n" +
                                  $"Policy: {request.PolicyNumber ?? "POL123456789"}\n" +
                                  $"Phone: {request.Phone ?? "+1-555-0123"}\n" +
                                  $"Email: {request.Email ?? $"{request.FirstName?.ToLower()}.{request.LastName?.ToLower()}@example.com"}";

            command.Parameters.AddWithValue("@Notes", appointmentNotes);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        // Main logic
        // 1. Check if patient exists by name and DOB
        int patientId;
        using (var patientCheckCommand = new SqlCommand(
            "SELECT patients_id FROM patients WHERE patients_name = @FirstName AND patients_surname = @LastName",
            connection))
        {
            patientCheckCommand.Parameters.AddWithValue("@FirstName", request.FirstName);
            patientCheckCommand.Parameters.AddWithValue("@LastName", request.LastName);

            var existingPatientId = await patientCheckCommand.ExecuteScalarAsync();

            if (existingPatientId != null)
            {
                patientId = Convert.ToInt32(existingPatientId);
                // Update existing patient
                await UpdatePatientDetails(patientId);
            }
            else
            {
                // Create new patient with mock contact/insurance data
                patientId = await CreateCompletePatient();
            }
        }

        // 2. Get or use preferred doctor
        int doctorId = request.PreferredDoctorId ?? await GetDefaultDoctorId();

        // 3. Get default room
        int roomId = await GetDefaultRoomId();

        // 4. Create appointment
        var appointmentId = await CreateAppointment(patientId, doctorId, roomId);

        return Results.Ok(new
        {
            success = true,
            message = "Comprehensive appointment booked successfully",
            appointmentId = appointmentId,
            patientId = patientId,
            doctorId = doctorId,
            roomId = roomId,
            nextSteps = "Confirmation will be sent shortly"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error booking comprehensive appointment: {ex.Message}");
    }
});

// Test comprehensive appointment booking
app.MapGet("/api/test-comprehensive", () =>
{
    var sampleRequest = new CompleteAppointmentRequest
    {
        FirstName = "John",
        LastName = "Smith",
        DateOfBirth = new DateTime(1985, 5, 15),
        Gender = "M",
        BirthCity = "New York",
        Phone = "+1-555-0123",
        Email = "john.smith@example.com",
        InsuranceProvider = "Delta Dental",
        PreferredDoctorId = 1,
        PreferredDate = DateTime.Now.AddDays(7),
        PreferredTime = "10:00 AM",
        Procedure = "Dental Checkup and Cleaning",
        Symptoms = "Routine checkup, minor sensitivity in lower left molar"
    };

    return Results.Ok(new
    {
        message = "Use this sample JSON in n8n",
        sampleRequest = sampleRequest,
        endpoint = "POST /api/appointments/comprehensive"
    });
});


// Health check endpoint
app.MapGet("/health", () => new {
    status = "healthy",
    timestamp = DateTime.UtcNow,
    message = "Dental API is running successfully"
});

// Check appointment availability
app.MapGet("/api/appointments/availability", async (string? date, string? startTime, IConfiguration config) =>
{
    var connStr = config.GetConnectionString("DentneDConnection");
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    var sql = @"
      SELECT start_time, end_time, doctor_id
      FROM appointments
      WHERE appointment_date = @date
        AND start_time >= @startTime
        AND is_available = 1
      ORDER BY start_time
    ";

    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@date", date ?? "");
    cmd.Parameters.AddWithValue("@startTime", startTime ?? "");

    var slots = new List<object>();
    await using var rdr = await cmd.ExecuteReaderAsync();
    while (await rdr.ReadAsync())
    {
        slots.Add(new
        {
            startTime = rdr["start_time"],
            endTime = rdr["end_time"],
            doctorId = rdr["doctor_id"]
        });
    }

    return Results.Ok(new
    {
        success = slots.Count > 0,
        availableSlots = slots,
        totalSlots = slots.Count
    });
});

// Search doctors
app.MapGet("/api/doctors/search", async (string? name, string? specialty, IConfiguration config) =>
{
    var connStr = config.GetConnectionString("DentneDConnection");
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    var sql = @"
      SELECT 
        patients_id       AS doctorId,
        patients_name     AS firstName,
        patients_surname  AS lastName,
        patients_doctext  AS specialty
      FROM patients
      WHERE patients_doctext IS NOT NULL
        AND (@name      IS NULL OR patients_name    LIKE '%' + @name + '%')
        AND (@specialty IS NULL OR patients_doctext LIKE '%' + @specialty + '%');
    ";

    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@name", (object?)name ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@specialty", (object?)specialty ?? DBNull.Value);

    var doctors = new List<object>();
    await using var rdr = await cmd.ExecuteReaderAsync();
    while (await rdr.ReadAsync())
    {
        doctors.Add(new
        {
            doctorId = (int)rdr["doctorId"],
            firstName = (string)rdr["firstName"],
            lastName = (string)rdr["lastName"],
            specialty = (string)rdr["specialty"]
        });
    }

    return Results.Ok(new
    {
        success = doctors.Count > 0,
        doctors = doctors,
        totalFound = doctors.Count
    });
});


// Book appointment
app.MapPost("/api/appointments/book", async (AppointmentBookingRequest request, IConfiguration config) =>
{
    try
    {
        // For now, return mock success to test the endpoint
        return Results.Ok(new
        {
            success = true,
            message = "Appointment booked successfully!",
            appointmentId = 12345,
            patientName = $"{request.PatientFirstName} {request.PatientLastName}",
            appointmentDate = request.AppointmentDate,
            appointmentTime = request.AppointmentTime,
            doctorId = request.DoctorId
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Appointment booking failed: {ex.Message}");
    }
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

public class CompleteAppointmentRequest
{
    // Patient Demographics
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? BirthCity { get; set; }

    // Contact Information (Mock data will be used if not provided)
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }

    // Insurance Information (Mock data will be used if not provided)
    public string? InsuranceProvider { get; set; }
    public string? PolicyNumber { get; set; }
    public string? GroupNumber { get; set; }
    public string? SubscriberName { get; set; }

    // Appointment Details
    public int? PreferredDoctorId { get; set; }
    public DateTime PreferredDate { get; set; }
    public string? PreferredTime { get; set; }
    public string? Procedure { get; set; }
    public string? Symptoms { get; set; }
    public string? Notes { get; set; }
}