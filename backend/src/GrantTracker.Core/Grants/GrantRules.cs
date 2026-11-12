namespace GrantTracker.Core.Grants;

public static class GrantRules
{
    public static bool CanClose(GrantStatus status, int pendingHighTasks) =>
        status == GrantStatus.Open && pendingHighTasks == 0;
}
