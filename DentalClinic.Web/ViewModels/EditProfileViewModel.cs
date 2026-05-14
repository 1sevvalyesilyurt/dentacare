using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.ViewModels
{
    /// <summary>
    /// ViewModel for the Customer's "Edit Profile" page (UC-C09).
    /// Covers personal info update AND optional password change.
    /// Rule: if ANY password field is filled, ALL three are required.
    /// </summary>
    public class EditProfileViewModel : IValidatableObject
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

        // ─── Password Change ──────────────────────────────────────────────────
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmNewPassword { get; set; }

        /// <summary>
        /// If ANY password field is filled, all three are required and
        /// NewPassword must match ConfirmNewPassword.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            bool anyFilled = !string.IsNullOrWhiteSpace(CurrentPassword)
                          || !string.IsNullOrWhiteSpace(NewPassword)
                          || !string.IsNullOrWhiteSpace(ConfirmNewPassword);

            if (!anyFilled) yield break; // no password change requested — OK

            if (string.IsNullOrWhiteSpace(CurrentPassword))
                yield return new ValidationResult(
                    "Current password is required when changing your password.",
                    new[] { nameof(CurrentPassword) });

            if (string.IsNullOrWhiteSpace(NewPassword))
                yield return new ValidationResult(
                    "New password is required when changing your password.",
                    new[] { nameof(NewPassword) });

            if (string.IsNullOrWhiteSpace(ConfirmNewPassword))
                yield return new ValidationResult(
                    "Please confirm your new password.",
                    new[] { nameof(ConfirmNewPassword) });

            if (!string.IsNullOrWhiteSpace(NewPassword) &&
                !string.IsNullOrWhiteSpace(ConfirmNewPassword) &&
                NewPassword != ConfirmNewPassword)
                yield return new ValidationResult(
                    "New passwords do not match.",
                    new[] { nameof(ConfirmNewPassword) });
        }
    }
}
