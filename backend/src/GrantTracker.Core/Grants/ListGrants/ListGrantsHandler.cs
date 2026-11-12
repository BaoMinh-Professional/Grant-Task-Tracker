using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Grants.ListGrants;

public sealed class ListGrantsHandler(IGrantRepository grants) : IRequestHandler<ListGrantsQuery, IReadOnlyList<GrantListItemDto>>
{
    public async Task<IReadOnlyList<GrantListItemDto>> Handle(ListGrantsQuery request, CancellationToken cancellationToken)
    {
        GrantStatus? status = null;
        var text = request.Status?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            if (!text.All(char.IsLetter) || !Enum.TryParse<GrantStatus>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ValidationException(ErrorCodes.ValidationFailed, "The status filter must be Open or Closed.",
                    new Dictionary<string, string[]> { ["status"] = ["Use Open or Closed."] });
            status = parsed;
        }
        var summaries = await grants.ListAsync(status, cancellationToken);
        return summaries.Select(s => GrantListItemDto.From(s)).ToList();
    }
}
