using Calendare.Server.Api;

namespace Server.Tests;

public class UsernameTest
{
    [Theory]
    [InlineData("Firstname Lastname")]
    [InlineData("username")]
    [InlineData("username2")]
    [InlineData("username-3")]
    [InlineData("user@example.com")]
    public void UsernameValid(string username)
    {
        var isValid = UserExtensions.IsValidUsername(username);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("username ")]
    [InlineData("username-")]
    [InlineData("2easy")]
    [InlineData("user@-example.com")]
    [InlineData("user@part@me")]
    [InlineData("user%20me")]
    [InlineData("user/me")]
    [InlineData("user\\me")]
    public void UsernameInvalid(string username)
    {
        var isValid = UserExtensions.IsValidUsername(username);
        Assert.False(isValid);
    }
}
