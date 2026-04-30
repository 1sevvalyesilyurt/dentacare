using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Central identity entity. Extends ASP.NET Core IdentityUser.
    /// All roles (Secretary, Doctor, Customer) are stored here, differentiated by Identity Role.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Soft-delete flag. Inactive users cannot log in.
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Doctor? DoctorProfile { get; set; }
        public Customer? CustomerProfile { get; set; }
    }
}
