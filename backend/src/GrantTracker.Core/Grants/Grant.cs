using GrantTracker.Core.Assignments;
using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Grants;

public sealed class Grant
{
    private Grant() { }
    public Grant(Guid id, string referenceNumber, string organisationName, DateOnly applicationDate, GrantStatus status, DateTime? closedAtUtc)
    {
        if ((status == GrantStatus.Closed) != closedAtUtc.HasValue) throw new ArgumentException("Status and closed timestamp must be consistent.");
        Id = id; ReferenceNumber = referenceNumber; OrganisationName = organisationName;
        ApplicationDate = applicationDate; Status = status; ClosedAtUtc = closedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ReferenceNumber { get; private set; } = "";
    public string OrganisationName { get; private set; } = "";
    public DateOnly ApplicationDate { get; private set; }
    public GrantStatus Status { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public ICollection<GrantTask> Tasks { get; private set; } = new List<GrantTask>();
    public ICollection<GrantAssignment> Assignments { get; private set; } = new List<GrantAssignment>();
}
