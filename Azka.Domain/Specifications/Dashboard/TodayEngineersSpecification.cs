using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Dashboard;

/// <summary>
/// Loads all engineers and their today-only assignments in one query.
/// The filtered Include keeps the collection scoped to today's window;
/// this is intentional — assignments outside today are irrelevant for
/// the dashboard's availability/utilization KPIs.
/// </summary>
public class TodayEngineersSpecification : BaseSpecification<Engineer>
{
    public TodayEngineersSpecification()
    {
        // String-based include to support filtered collection loading
        // (filtered Includes require EF 5+ and are done inline in the service)
        AddInclude("Assignments.WorkOrder");
    }
}
