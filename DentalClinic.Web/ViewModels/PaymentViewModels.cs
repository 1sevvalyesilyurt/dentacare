using DentalClinic.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Web.ViewModels
{
    /// <summary>
    /// ViewModel for the Secretary's "Record Payment" form.
    /// </summary>
    public class PaymentRecordViewModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        [Range(0.01, 100000)]
        [Display(Name = "Amount (₺)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public PaymentMethod PaymentMethod { get; set; }

        // ─── Read-only display info (populated by controller) ─────────────
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public decimal SuggestedFee { get; set; }
    }

    /// <summary>
    /// ViewModel for the Secretary's overall payment/revenue dashboard.
    /// </summary>
    public class PaymentDashboardViewModel
    {
        public decimal TotalRevenueToday { get; set; }
        public decimal TotalRevenueThisMonth { get; set; }
        public int PendingPaymentsCount { get; set; } // Completed appointments without a Payment record

        public List<PendingPaymentRow> PendingPayments { get; set; } = new();
        public List<RecentPaymentRow> RecentPayments { get; set; } = new();
        public List<DoctorEarningsRow> DoctorEarnings { get; set; } = new();
    }

    public class PendingPaymentRow
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public decimal Fee { get; set; }
    }

    public class RecentPaymentRow
    {
        public int PaymentId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class DoctorEarningsRow
    {
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public int CompletedAppointments { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal DoctorShare { get; set; } // TotalRevenue × CommissionRate
    }
}
