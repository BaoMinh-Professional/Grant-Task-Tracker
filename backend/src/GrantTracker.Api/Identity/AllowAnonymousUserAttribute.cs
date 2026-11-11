namespace GrantTracker.Api.Identity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowAnonymousUserAttribute : Attribute;
