using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Web.Models
{
    /// <summary>
    /// Records financial transactions for completed appointments.
    /// Business Rule BR-03: Only one payment per Appointment.
    /// </summary>
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        /// <summary>
        /// 1-to-1 relationship with Appointment. Enforced via unique index in AppDbContext.
        /// </summary>
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount Paid (₺)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public PaymentMethod PaymentMethod { get; set; }

        [Display(Name = "Paid At")]
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The Secretary (ApplicationUser) who recorded this payment.
        /// </summary>
        [Required]
        public string RecordedByUserId { get; set; } = string.Empty;

        /// <summary>
        /// Auto-generated invoice number. Format: INV-YYYYMMDD-{AppointmentId}
        /// </summary>
        [StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }
    }
}
