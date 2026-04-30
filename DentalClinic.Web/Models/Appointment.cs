using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Core transactional entity of the system.
    /// Business Rule BR-01: Unique composite index on (DoctorId, AppointmentDate) prevents double-booking.
    /// </summary>
    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [Required]
        public int ServiceId { get; set; }

        /// <summary>
        /// Full date AND time of the appointment.
        /// The unique constraint (DoctorId, AppointmentDate) lives in AppDbContext.OnModelCreating.
        /// </summary>
        [Required]
        [Display(Name = "Appointment Date & Time")]
        public DateTime AppointmentDate { get; set; }

        [Required]
        [Display(Name = "Status")]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

        [StringLength(500)]
        [Display(Name = "Patient Note")]
        public string? PatientNote { get; set; }

        [StringLength(500)]
        [Display(Name = "Secretary Note")]
        public string? SecretaryNote { get; set; }

        /// <summary>
        /// Actual fee charged. May differ from Service.BaseFee if overridden by the secretary.
        /// </summary>
        [Required]
        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Fee (₺)")]
        public decimal Fee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Null if self-booked by the customer; populated with the Secretary's UserId if created manually.
        /// </summary>
        public string? CreatedByUserId { get; set; }

        /// <summary>
        /// Used by the ReminderBackgroundService to prevent duplicate notifications (UC-SYS01).
        /// </summary>
        public bool ReminderSent { get; set; } = false;

        // Navigation properties
        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        [ForeignKey("ServiceId")]
        public Service? Service { get; set; }

        public Payment? Payment { get; set; }
    }
}
