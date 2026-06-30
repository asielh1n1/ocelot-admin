using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Util
{
    public static class CliUserManager
    {
        public static async Task RunAsync(string[] args, IServiceProvider services, IConfiguration configuration)
        {
            var cmd = args[0].ToLowerInvariant();
            using var scope = services.CreateScope();
            var svc = scope.ServiceProvider;
            var userManager = svc.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = svc.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure DB exists
            var db = svc.GetRequiredService<ApplicationDbContext>();
            try
            {
                db.Database.EnsureCreated();
            }
            catch { }

            switch (cmd)
            {
                case "add":
                    await CliAdd(userManager, roleManager);
                    break;
                case "remove":
                    await CliRemove(userManager);
                    break;
                case "set-password":
                    await CliSetPassword(userManager);
                    break;
                case "list":
                    await CliList(userManager);
                    break;
                default:
                    Console.WriteLine("Unknown command. Commands: add, remove, set-password, list");
                    break;
            }

            // close process
            Environment.Exit(0);
        }

        private static async Task CliAdd(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            var username = Prompt("Username: ");
            var password = PromptPassword("Password: ");
            var email = ""; //Prompt($"Email (optional): ");
            var role = "Administrator"; // Prompt($"Role (optional, e.g. Administrator): ");

            if (await userManager.FindByNameAsync(username) != null)
            {
                Console.WriteLine("User already exists");
                return;
            }

            var user = new ApplicationUser { UserName = username, Email = string.IsNullOrWhiteSpace(email) ? null : email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                Console.WriteLine("Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
                await userManager.AddToRoleAsync(user, role);
            }
            Console.WriteLine("User created");
        }

        private static async Task CliRemove(UserManager<ApplicationUser> userManager)
        {
            var username = Prompt("Username: ");
            var user = await userManager.FindByNameAsync(username);
            if (user == null)
            {
                Console.WriteLine("User not found");
                return;
            }
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                Console.WriteLine("Failed to remove user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
            Console.WriteLine("User removed");
        }

        private static async Task CliSetPassword(UserManager<ApplicationUser> userManager)
        {
            var username = Prompt("Username: ");
            var user = await userManager.FindByNameAsync(username);
            if (user == null)
            {
                Console.WriteLine("User not found");
                return;
            }
            var newPwd = PromptPassword("New password: ");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, newPwd);
            if (!result.Succeeded)
            {
                Console.WriteLine("Failed to set password: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
            Console.WriteLine("Password updated");
        }

        private static async Task CliList(UserManager<ApplicationUser> userManager)
        {
            var users = userManager.Users;
            var all = await users.ToListAsync();
            foreach (var u in all)
            {
                var roles = await userManager.GetRolesAsync(u);
                Console.WriteLine($"- {u.UserName} [{string.Join(',', roles)}]");
            }
        }

        private static string Prompt(string message)
        {
            Console.Write(message);
            return Console.ReadLine() ?? string.Empty;
        }

        private static string PromptPassword(string message)
        {
            Console.Write(message);
            var pwd = string.Empty;
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;
                if (key == ConsoleKey.Enter) break;
                if (key == ConsoleKey.Backspace && pwd.Length > 0)
                {
                    pwd = pwd[..^1];
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    pwd += keyInfo.KeyChar;
                    Console.Write("*");
                }
            } while (true);
            Console.WriteLine();
            return pwd;
        }
    }
}
