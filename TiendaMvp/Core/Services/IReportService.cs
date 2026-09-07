using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IReportService
{
    DashboardSummary GetDashboardSummary();
    ReportResult GetReport(string reportType, DateTime fromLocalDate, DateTime toLocalDate);
}
