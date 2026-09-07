using System.Security.Claims;
using InventorySystem.Helpers;
using Xunit;

namespace InventorySystem.Tests
{
    public class CurrentUserHelperTests
    {
        [Fact]
        public void TryGetUserId_ReturnsParsedId_WhenNameIdentifierIsValid()
        {
            var user = CreateUser("42");

            var found = CurrentUserHelper.TryGetUserId(user, out var userId);

            Assert.True(found);
            Assert.Equal(42, userId);
        }

        [Fact]
        public void GetRequiredUserId_ReturnsParsedId_WhenNameIdentifierIsValid()
        {
            var user = CreateUser("7");

            Assert.Equal(7, CurrentUserHelper.GetRequiredUserId(user));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-1")]
        public void TryGetUserId_Fails_WhenClaimIsMissingOrInvalid(string? claimValue)
        {
            var user = claimValue == null
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : CreateUser(claimValue);

            var found = CurrentUserHelper.TryGetUserId(user, out var userId);

            Assert.False(found);
            Assert.Equal(0, userId);
        }

        [Fact]
        public void TryGetUserId_Fails_WhenPrincipalIsNull()
        {
            var found = CurrentUserHelper.TryGetUserId(null, out var userId);

            Assert.False(found);
            Assert.Equal(0, userId);
        }

        [Fact]
        public void GetRequiredUserId_Throws_WhenClaimIsMissing()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            var ex = Assert.Throws<MissingUserIdentityException>(
                () => CurrentUserHelper.GetRequiredUserId(user));

            Assert.Equal(MissingUserIdentityException.DefaultMessage, ex.Message);
        }

        [Fact]
        public void GetRequiredUserId_NeverFallsBackToAdminUserId()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            Assert.Throws<MissingUserIdentityException>(() => CurrentUserHelper.GetRequiredUserId(user));
        }

        private static ClaimsPrincipal CreateUser(string userId)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                authenticationType: "Test");
            return new ClaimsPrincipal(identity);
        }
    }
}
