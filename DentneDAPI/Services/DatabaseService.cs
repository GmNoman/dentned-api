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
            _connectionString = configuration.GetConnectionString("DentneDConnection") ??
                throw new ArgumentNullException("Connection string is null");
        }

        public async Task<int> CreatePatientAsync(Patient patient)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"INSERT INTO patients (patients_name, patients_surname) 
                         OUTPUT INSERTED.patients_id
                         VALUES (@FirstName, @LastName)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@FirstName", patient.FirstName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@LastName", patient.LastName ?? (object)DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return result != null ? (int)result : 0;
        }

        public async Task<int> CreateAppointmentAsync(Appointment appointment)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            int doctorId = await GetFirstDoctorIdAsync();
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
            command.Parameters.AddWithValue("@DoctorId", doctorId);
            command.Parameters.AddWithValue("@AppointmentFrom", appointment.AppointmentFrom);
            command.Parameters.AddWithValue("@AppointmentTo", appointmentEndTime);
            command.Parameters.AddWithValue("@Title", appointment.Procedure ?? "Dental Appointment");

            var result = await command.ExecuteScalarAsync();
            return result != null ? (int)result : 0;
        }

        private async Task<int> GetFirstDoctorIdAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT TOP 1 doctors_id FROM doctors ORDER BY doctors_id";
            using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();

            return result != null ? (int)result : 1;
        }
    }
}