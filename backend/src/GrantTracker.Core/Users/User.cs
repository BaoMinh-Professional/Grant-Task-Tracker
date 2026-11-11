namespace GrantTracker.Core.Users;

public sealed class User
{
    private User() { }
    public User(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = EmailAddress.Normalise(email);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
}
