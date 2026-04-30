using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Represents a dental treatment or procedure offered by the clinic.
    /// Managed exclusively by the Secretary.
    /// </summary>
    public class Service
    {
        [Key]
        public int ServiceId { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Service Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Base Fee (₺)")]
        public decimal BaseFee { get; set; }

        [Required]
        [Range(5, 480)]
        [Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; } = 30;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
