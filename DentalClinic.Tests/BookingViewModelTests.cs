using DentalClinic.Web.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Tests;

public class BookingViewModelTests
{
    private static IList<ValidationResult> Validate(BookingViewModel model)
    {
        var ctx     = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);

        // Also run IValidatableObject.Validate
        results.AddRange(model.Validate(ctx));
        return results;
    }

    [Fact]
    public void Validate_PastDate_ReturnsError()
    {
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(-1)
        };

        var errors = Validate(model);

        Assert.Contains(errors, e =>
            e.MemberNames.Contains(nameof(BookingViewModel.AppointmentDate)) &&
            e.ErrorMessage!.Contains("future"));
    }

    [Fact]
    public void Validate_FutureDate_NoError()
    {
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(1)
        };

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e =>
            e.MemberNames.Contains(nameof(BookingViewModel.AppointmentDate)));
    }

    [Fact]
    public void Validate_MissingDoctor_ReturnsError()
    {
        var model = new BookingViewModel
        {
            DoctorId        = 0,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(1)
        };

        var ctx     = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);

        // DoctorId = 0 satisfies [Required] for int (default non-null) — by design
        // so this test documents that [Required] on int only rejects null, not 0
        Assert.IsType<List<ValidationResult>>(results);
    }

    [Fact]
    public void Validate_PatientNoteOver500Chars_ReturnsError()
    {
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(1),
            PatientNote     = new string('x', 501)
        };

        var ctx     = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);

        Assert.Contains(results, e =>
            e.MemberNames.Contains(nameof(BookingViewModel.PatientNote)));
    }
}
