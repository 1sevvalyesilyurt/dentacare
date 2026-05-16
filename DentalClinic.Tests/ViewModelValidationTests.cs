using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.ViewModels;

namespace DentalClinic.Tests;

/// <summary>
/// Unit tests for ViewModel data-annotation and IValidatableObject validation rules.
/// Covers RegisterViewModel and EditProfileViewModel.
/// BookingViewModel is tested separately in BookingViewModelTests.cs.
/// </summary>
public class ViewModelValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var ctx     = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);

        if (model is IValidatableObject validatable)
            results.AddRange(validatable.Validate(ctx));

        return results;
    }

    private static bool HasError(IList<ValidationResult> results, string? memberName = null) =>
        memberName == null
            ? results.Count > 0
            : results.Any(r => r.MemberNames.Contains(memberName));

    // ═══════════════════════════════════════════════════════════════════════════
    // RegisterViewModel
    // ═══════════════════════════════════════════════════════════════════════════

    private static RegisterViewModel ValidRegister() => new()
    {
        FullName        = "Jane Smith",
        Email           = "jane@example.com",
        PhoneNumber     = "+905001234567",
        Password        = "Test@Pass1!",
        ConfirmPassword = "Test@Pass1!",
    };

    [Fact]
    public void Register_AllValid_NoErrors()
    {
        var errors = Validate(ValidRegister());
        Assert.Empty(errors);
    }

    [Fact]
    public void Register_EmptyFullName_ReturnsError()
    {
        var model = ValidRegister();
        model.FullName = "";

        Assert.True(HasError(Validate(model), nameof(model.FullName)));
    }

    [Fact]
    public void Register_FullNameTooShort_ReturnsError()
    {
        var model = ValidRegister();
        model.FullName = "A"; // min 2 chars

        Assert.True(HasError(Validate(model), nameof(model.FullName)));
    }

    [Fact]
    public void Register_InvalidEmail_ReturnsError()
    {
        var model = ValidRegister();
        model.Email = "not-an-email";

        Assert.True(HasError(Validate(model), nameof(model.Email)));
    }

    [Fact]
    public void Register_EmptyEmail_ReturnsError()
    {
        var model = ValidRegister();
        model.Email = "";

        Assert.True(HasError(Validate(model), nameof(model.Email)));
    }

    [Fact]
    public void Register_EmptyPassword_ReturnsError()
    {
        var model = ValidRegister();
        model.Password = "";
        model.ConfirmPassword = "";

        Assert.True(HasError(Validate(model), nameof(model.Password)));
    }

    [Fact]
    public void Register_PasswordTooShort_ReturnsError()
    {
        var model = ValidRegister();
        model.Password        = "ab1!";  // min 6 chars in ViewModel (Identity policy is 8)
        model.ConfirmPassword = "ab1!";

        Assert.True(HasError(Validate(model), nameof(model.Password)));
    }

    [Fact]
    public void Register_MismatchedPasswords_ReturnsError()
    {
        var model = ValidRegister();
        model.ConfirmPassword = "Different@1!";

        Assert.True(HasError(Validate(model), nameof(model.ConfirmPassword)));
    }

    [Fact]
    public void Register_EmptyConfirmPassword_ReturnsError()
    {
        var model = ValidRegister();
        model.ConfirmPassword = "";

        Assert.True(HasError(Validate(model)));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EditProfileViewModel — password change all-or-nothing rule
    // ═══════════════════════════════════════════════════════════════════════════

    private static EditProfileViewModel ValidProfile() => new()
    {
        FullName = "John Doe",
    };

    [Fact]
    public void EditProfile_NoPasswordFields_NoErrors()
    {
        var errors = Validate(ValidProfile());
        Assert.Empty(errors);
    }

    [Fact]
    public void EditProfile_AllPasswordFieldsFilled_NoErrors()
    {
        var model = ValidProfile();
        model.CurrentPassword  = "Old@Pass1!";
        model.NewPassword      = "New@Pass1!";
        model.ConfirmNewPassword = "New@Pass1!";

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void EditProfile_OnlyCurrentPassword_ReturnsNewPasswordError()
    {
        var model = ValidProfile();
        model.CurrentPassword = "Old@Pass1!";
        // NewPassword and ConfirmNewPassword are blank

        var errors = Validate(model);
        Assert.True(HasError(errors, nameof(model.NewPassword)));
    }

    [Fact]
    public void EditProfile_OnlyNewPassword_ReturnsCurrentPasswordError()
    {
        var model = ValidProfile();
        model.NewPassword = "New@Pass1!";
        // CurrentPassword and ConfirmNewPassword are blank

        var errors = Validate(model);
        Assert.True(HasError(errors, nameof(model.CurrentPassword)));
    }

    [Fact]
    public void EditProfile_NewAndConfirmMismatch_ReturnsConfirmError()
    {
        var model = ValidProfile();
        model.CurrentPassword    = "Old@Pass1!";
        model.NewPassword        = "New@Pass1!";
        model.ConfirmNewPassword = "Different@1!";

        var errors = Validate(model);
        Assert.True(HasError(errors, nameof(model.ConfirmNewPassword)));
    }

    [Fact]
    public void EditProfile_NewPasswordTooShort_ReturnsLengthError()
    {
        var model = ValidProfile();
        model.CurrentPassword    = "Old@Pass1!";
        model.NewPassword        = "short";     // min 8 chars
        model.ConfirmNewPassword = "short";

        var errors = Validate(model);
        Assert.True(HasError(errors, nameof(model.NewPassword)));
    }

    [Fact]
    public void EditProfile_EmptyFullName_ReturnsError()
    {
        var model = ValidProfile();
        model.FullName = "";

        Assert.True(HasError(Validate(model), nameof(model.FullName)));
    }
}
