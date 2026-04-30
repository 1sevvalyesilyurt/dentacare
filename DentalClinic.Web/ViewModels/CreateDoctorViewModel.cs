using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.ViewModels
{
    public class CreateDoctorViewModel
    {
        // ─── Account details ──────────────────────────────────────────────
        [Required]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email (login)")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        // ─── Doctor profile details ───────────────────────────────────────
        [Required]
        [StringLength(100)]
        [Display(Name = "Specialty")]
        public string Specialty { get; set; } = string.Empty;

        [Required]
        [Range(0.0, 1.0)]
        [Display(Name = "Commission Rate (e.g. 0.70 = 70%)")]
        public decimal CommissionRate { get; set; } = 0.70m;

        [Required]
        [Display(Name = "Working Hours Start")]
        public TimeSpan WorkingHoursStart { get; set; } = new TimeSpan(9, 0, 0);

        [Required]
        [Display(Name = "Working Hours End")]
        public TimeSpan WorkingHoursEnd { get; set; } = new TimeSpan(17, 0, 0);

        [Required]
        [Range(15, 120)]
        [Display(Name = "Appointment Slot Duration (minutes)")]
        public int SlotDurationMinutes { get; set; } = 30;
    }
}
