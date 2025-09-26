namespace DentneDAPI.Models
{
    public class Appointment
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public DateTime AppointmentFrom { get; set; }
        public string? Procedure { get; set; }
        public string? Notes { get; set; }
    }
}