using System.Text;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Identity;

namespace BrandWorkspace.Api.Access;

/// <summary>Creates an operator through an explicit local command; never grants organization membership.</summary>
public static class OperatorProvisioning
{
    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 5 || args[0] != "--provision-operator" || args[1] != "--email" || args[3] != "--name")
            throw new ArgumentException("Use --provision-operator --email <email> --name <display-name>; provide the password on stdin.");
        var email = args[2].Trim();
        var name = args[4].Trim();
        if (email.Length > 254 || string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Any(char.IsControl))
            throw new ArgumentException("A valid email and display name of 1–120 characters are required.");
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>();
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("Operator already exists; this command never resets an account.");
        var password = await ReadPasswordAsync();
        if (password.Length > 1024) throw new ArgumentException("Password exceeds the maximum length.");
        var user = new AccessUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name, LockoutEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Operator was not created: " + string.Join(", ", result.Errors.Select(error => error.Code)));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { created = true, userId = user.Id, email = user.Email }));
    }

    private static async Task<string> ReadPasswordAsync()
    {
        if (Console.IsInputRedirected) return await Console.In.ReadLineAsync() ?? throw new ArgumentException("Password is required on stdin.");
        Console.Error.Write("Password (hidden): ");
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
            if (value.Length > 1024) throw new ArgumentException("Password exceeds the maximum length.");
        }
    }
}
