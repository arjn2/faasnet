using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BMS.External.Seeding;

public static class UserSeeder
{
    public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await CreateUserWithRole(userManager, "admin@dwvops1.com", "Admin123!", Roles.Admin);
    }

    private static async Task CreateUserWithRole(
        UserManager<IdentityUser> userManager,
        string email,
        string password,
        string role)
    {
        if (await userManager.FindByEmailAsync(email) == null)
        {
            var user = new IdentityUser
            {
                Email = email,
                EmailConfirmed = true,
                UserName = email
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
            else
            {
                throw new Exception($"Failed creating user {email}: {string.Join(",", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}