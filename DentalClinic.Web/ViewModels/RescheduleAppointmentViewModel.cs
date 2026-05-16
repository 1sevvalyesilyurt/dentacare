using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.ViewModels
{
    public class RescheduleAppointmentViewModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        public DateTime CurrentAppointmentDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "New Date")]
        public DateTime NewDate { get; set; }

        [Required]
        [Display(Name = "New Time Slot")]
        public string NewTime { get; set; } = string.Empty;

        public List<string> AvailableSlots { get; set; } = new();
    }
}
