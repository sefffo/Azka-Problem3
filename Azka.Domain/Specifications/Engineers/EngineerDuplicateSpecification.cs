using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Checks whether an engineer with the given employee number already exists.
/// Used by EngineerService.CreateAsync() to enforce unique employee numbers.
/// </summary>
public class EngineerDuplicateSpecification : BaseSpecification<Engineer>
{
    public EngineerDuplicateSpecification(string employeeNumber)
    {
        AddCriteria(e => e.EmployeeNumber == employeeNumber);
    }
}
