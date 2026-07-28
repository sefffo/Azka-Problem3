using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Assignments;

public class AssignmentByIdSpecification : BaseSpecification<Assignment>
{
    public AssignmentByIdSpecification(int id, bool includeHistory = false)
    {
        AddCriteria(a => a.Id == id);
        AddInclude(a => a.Engineer);
        AddInclude(a => a.WorkOrder);
        if (includeHistory)
            AddInclude("History");
    }
}