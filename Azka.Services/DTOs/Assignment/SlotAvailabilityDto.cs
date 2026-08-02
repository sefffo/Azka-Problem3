namespace Azka.Services.DTOs.Assignment;

/// <summary>
/// Result of checking whether any active engineer can take a requested time
/// slot (within working hours, no conflicting assignment, within daily capacity).
/// Returned by GET /api/Assignments/availability.
/// </summary>
public class SlotAvailabilityDto
{
    public bool Available { get; set; }
    public List<EngineerAvailabilityDto> Engineers { get; set; } = new();
}
