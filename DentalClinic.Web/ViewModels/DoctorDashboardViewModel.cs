using DentalClinic.Web.Models;

namespace DentalClinic.Web.ViewModels
{
    /// <summary>
    /// ViewModel for the Doctor's dashboard — daily/weekly schedule + earnings.
    /// Business Rule BR-04: Doctor sees only their own appointments.
    /// Business Rule BR-06: Earnings = Fee × CommissionRate.
    /// </summary>
    public class DoctorDashboardViewModel
    {
        public string DoctorFullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public decimal CommissionRate { get; set; }

        public DateTime SelectedDate { get; set; } = DateTime.Today;

        public List<AppointmentSummary> TodayAppointments { get; set; } = new();
        public List<AppointmentSummary> WeekAppointments { get; set; } = new();

        // ─── Earnings summary ─────────────────────────────────────────────
        public decimal TodayEarnings { get; set; }
        public decimal WeekEarnings { get; set; }
    }

    public class AppointmentSummary
    {
        public int AppointmentId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string? PatientNote { get; set; }
        public AppointmentStatus Status { get; set; }
        public decimal Fee { get; set; }
        public decimal DoctorEarning { get; set; } // Fee × CommissionRate
    }
}
