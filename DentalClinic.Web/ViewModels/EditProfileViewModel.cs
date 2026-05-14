using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.ViewModels
{
    /// <summary>
    /// ViewModel for the Customer's "Edit Profile" page (UC-C09).
    /// Covers personal info update AND optional password change.
    /// </summary>
    public class EditProfileViewModel
    {
        // ─── Personal Info ────────────────────────────────────────────────────
        [Required]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        // ─── Password Change (all three required together if any is set) ──────
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "New passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmNewPassword { get; set; }
    }
}
