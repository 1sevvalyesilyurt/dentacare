using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Records periods when a Doctor is unavailable.
    /// The booking service checks this table before confirming a slot.
    /// </summary>
    public class DoctorLeave
    {
        [Key]
        public int LeaveId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Leave Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Leave End Date")]
        public DateTime EndDate { get; set; }

        [StringLength(300)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        // Navigation properties
        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }
    }
}
