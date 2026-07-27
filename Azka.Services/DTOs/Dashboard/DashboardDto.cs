namespace Azka.Services.DTOs.Dashboard;

public class DashboardDto
{
    public EngineerSummaryDto EngineerSummary { get; set; } = new();
    public WorkOrderSummaryDto WorkOrderSummary { get; set; } = new();
}

public class EngineerSummaryDto
{
    public int TotalEngineers { get; set; }
    public int AvailableEngineers { get; set; }
    public int BusyEngineers { get; set; }
    public int InactiveEngineers { get; set; }
    public int OverloadedEngineers { get; set; }
}

public class WorkOrderSummaryDto
{
    public int TotalWorkOrders { get; set; }
    public int OpenWorkOrders { get; set; }
    public int AssignedWorkOrders { get; set; }
    public int InProgressWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public int EmergencyRequests { get; set; }
    public int CancelledWorkOrders { get; set; }
}
