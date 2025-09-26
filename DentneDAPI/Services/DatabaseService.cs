using System.Data;
using Microsoft.Data.SqlClient;
using DentneDAPI.Models;

namespace DentneDAPI.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DentneDConnection");
        }

        public async Task<int> CreatePatientAsync(Patient patient)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Only include essential fields
            var query = @"INSERT INTO patients (patients_name, patients_surname) 
                         OUTPUT INSERTED.patients_id
                         VALUES (@FirstName, @LastName)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@FirstName", patient.FirstName ?? "Unknown");
            command.Parameters.AddWithValue("@LastName", patient.LastName ?? "Patient");

            return (int)await command.ExecuteScalarAsync();
        }

        public async Task<int> CreateAppointmentAsync(Appointment appointment)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Try to get a doctor ID, but allow NULL if none exists
            int? doctorId = await TryGetFirstDoctorIdAsync();

            // Calculate end time
            var appointmentEndTime = appointment.AppointmentFrom.AddHours(1);

            var query = @"INSERT INTO appointments (
                    patients_id, 
                    doctors_id, 
                    appointments_from, 
                    appointments_to, 
                    appointments_title
                 ) 
                 OUTPUT INSERTED.appointments_id
                 VALUES (@PatientId, @DoctorId, @AppointmentFrom, @AppointmentTo, @Title)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@PatientId", appointment.PatientId);
            command.Parameters.AddWithValue("@DoctorId", doctorId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@AppointmentFrom", appointment.AppointmentFrom);
            command.Parameters.AddWithValue("@AppointmentTo", appointmentEndTime);
            command.Parameters.AddWithValue("@Title", appointment.Procedure ?? "Dental Appointment");

            return (int)await command.ExecuteScalarAsync();
        }

        private async Task<int?> TryGetFirstDoctorIdAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT TOP 1 doctors_id FROM doctors ORDER BY doctors_id";
            using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();

            return result != null ? (int)result : null;
        }

        public async Task<List<dynamic>> GetAvailableTreatmentsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT treatments_id, treatments_name, treatments_price FROM treatments";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var treatments = new List<dynamic>();
            while (await reader.ReadAsync())
            {
                treatments.Add(new
                {
                    TreatmentId = reader.GetInt32("treatments_id"),
                    Name = reader.IsDBNull("treatments_name") ? null : reader.GetString("treatments_name"),
                    Price = reader.IsDBNull("treatments_price") ? 0 : reader.GetDecimal("treatments_price")
                });
            }

            return treatments;
        }

        public async Task<List<dynamic>> GetDoctorsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT doctors_id, doctors_name FROM doctors";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var doctors = new List<dynamic>();
            while (await reader.ReadAsync())
            {
                doctors.Add(new
                {
                    DoctorId = reader.GetInt32("doctors_id"),
                    Name = reader.IsDBNull("doctors_name") ? null : reader.GetString("doctors_name")
                });
            }

            return doctors;
        }
    }
}