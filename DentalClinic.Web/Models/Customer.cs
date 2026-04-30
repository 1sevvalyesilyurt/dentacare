using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Patient profile entity. Extends ApplicationUser (1-to-1) for the Customer role.
    /// </summary>
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Medical Notes")]
        public string? MedicalNotes { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
