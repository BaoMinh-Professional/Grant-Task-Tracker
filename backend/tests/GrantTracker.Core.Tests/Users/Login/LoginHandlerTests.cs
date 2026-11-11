using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Users;
using GrantTracker.Core.Users.Login;
using Moq;

namespace GrantTracker.Core.Tests.Users.Login;

public sealed class LoginHandlerTests
{
    [Fact]
    public async Task NormalisesEmailAndReturnsUser()
    {
        var user = new User(Guid.NewGuid(), "Alex", "alex@example.org");
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.FindByEmailAsync("alex@example.org", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var result = await new LoginHandler(repository.Object).Handle(new LoginQuery(" Alex@Example.org "), TestContext.Current.CancellationToken);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task UnknownAndBlankEmailsFailWithTypedErrors()
    {
        var repository = new Mock<IUserRepository>();
        var handler = new LoginHandler(repository.Object);
        var unknown = await Assert.ThrowsAsync<UserNotIdentifiedException>(() => handler.Handle(new LoginQuery("unknown@example.org"), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.UnknownUser, unknown.Code);
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new LoginQuery(" "), TestContext.Current.CancellationToken));
    }
}
