using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Doctor profile entity. Extends ApplicationUser (1-to-1 relationship via UserId).
    /// </summary>
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Specialty")]
        public string Specialty { get; set; } = string.Empty;

        /// <summary>
        /// Commission rate as a decimal (e.g. 0.70 = 70%).
        /// Used to calculate per-appointment earnings (BR-06).
        /// </summary>
        [Required]
        [Range(0.0, 1.0)]
        [Column(TypeName = "decimal(5,4)")]
        [Display(Name = "Commission Rate")]
        public decimal CommissionRate { get; set; } = 0.70m;

        [Required]
        [Display(Name = "Working Hours Start")]
        public TimeSpan WorkingHoursStart { get; set; } = new TimeSpan(9, 0, 0);

        [Required]
        [Display(Name = "Working Hours End")]
        public TimeSpan WorkingHoursEnd { get; set; } = new TimeSpan(17, 0, 0);

        /// <summary>
        /// Default appointment slot duration in minutes (e.g. 30).
        /// Used to generate available booking slots.
        /// </summary>
        [Required]
        [Range(15, 120)]
        [Display(Name = "Slot Duration (minutes)")]
        public int SlotDurationMinutes { get; set; } = 30;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<DoctorLeave> Leaves { get; set; } = new List<DoctorLeave>();
    }
}
