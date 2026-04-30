using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// In-app notification records for patients.
    /// Generated automatically by ReminderBackgroundService (UC-SYS01).
    /// </summary>
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        public int? AppointmentId { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "Read")]
        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }
    }
}
