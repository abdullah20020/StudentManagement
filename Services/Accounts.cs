using static StudentManagement.Localization.L;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace StudentManagement.Services
{
    /// <summary>Creates login accounts for students and instructors.</summary>
    public static class Accounts
    {
        /// <summary>
        /// Creates a user with a random temporary password (shown once to the admin) in the given role.
        /// Returns the new user id, or null with the reason.
        /// </summary>
        public static async Task<(string? UserId, string Message)> CreateLoginAsync(UserManager<IdentityUser> users, string email, string role)
        {
            if (await users.FindByEmailAsync(email) != null)
                return (null, T("A login for {0} already exists.", email));

            var password = TemporaryPassword();
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                return (null, T("Couldn't create the login: {0}", string.Join(" ", result.Errors.Select(e => e.Description))));

            await users.AddToRoleAsync(user, role);
            return (user.Id, T("Login created for {0}. Temporary password: {1} — share it with them now; it won't be shown again.", email, password));
        }

        private static string TemporaryPassword()
        {
            const string letters = "abcdefghjkmnpqrstuvwxyz";
            string Pick(string chars, int n) => new(Enumerable.Range(0, n).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
            return Pick(letters.ToUpperInvariant(), 2) + Pick(letters, 4) + "@" + Pick("23456789", 4);
        }
    }
}
