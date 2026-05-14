using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.ViewModels
{
    /// <summary>
    /// ViewModel for the customer's appointment booking form.
    /// </summary>
    public class BookingViewModel : IValidatableObject
    {
        [Required]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required]
        [Display(Name = "Service")]
        public int ServiceId { get; set; }

        [Required]
        [Display(Name = "Appointment Date & Time")]
        public DateTime AppointmentDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var now    = DateTime.UtcNow;
            var maxDate = now.AddMonths(6);

            if (AppointmentDate <= now)
                yield return new ValidationResult(
                    "Appointment date must be in the future.",
                    new[] { nameof(AppointmentDate) });

            else if (AppointmentDate > maxDate)
                yield return new ValidationResult(
                    "Appointments can only be booked up to 6 months in advance.",
                    new[] { nameof(AppointmentDate) });
        }

        [StringLength(500)]
        [Display(Name = "Your Complaint / Note")]
        public string? PatientNote { get; set; }

        // ─── Dropdown data (populated by the controller) ─────────────────
        public List<DoctorSelectItem> AvailableDoctors { get; set; } = new();
        public List<ServiceSelectItem> AvailableServices { get; set; } = new();
        public List<DateTime> AvailableSlots { get; set; } = new();
    }

    public class DoctorSelectItem
    {
        public int DoctorId { get; set; }
        public string DisplayName { get; set; } = string.Empty; // "Dr. Jane Smith — Orthodontics"
    }

    public class ServiceSelectItem
    {
        public int ServiceId { get; set; }
        public string DisplayName { get; set; } = string.Empty; // "Root Canal — ₺1500 (60 min)"
    }
}
