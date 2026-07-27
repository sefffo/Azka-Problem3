namespace Azka.Domain.Enums;

public enum WorkOrderStatus
{
    Open = 1,
    PendingAssignment = 2,
    Assigned = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
    Overdue = 7
}
