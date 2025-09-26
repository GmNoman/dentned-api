namespace DentneDAPI.Models
{
    public class AppointmentRequest
    {
        public string? PatientFirstName { get; set; }
        public string? PatientLastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime? PatientBirthDate { get; set; } // ADD THIS
        public DateTime AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public string? Procedure { get; set; }
        public string? Notes { get; set; }
    }
}