using Manifest.Data;

namespace Manifest.Tools;

/// <summary>
/// Accounts from the command line, for the machine the server runs on. This is the
/// way in when registration is closed, and the way back in when someone forgets a
/// password - there is no email on this server to send a reset link to, and adding
/// one would mean an SMTP account and a deliverability problem for a tool whose
/// whole point is that it is yours.
/// </summary>
public static class UserAdmin
{
    const string Usage = """
        manifest user <command>

          add <name>        create an account (prompts for the password)
          passwd <name>     change an account's password, ending its sessions
          list              show accounts, when made and when last seen
          delete <name>     remove an account and everything it owns

        Passwords are typed at the prompt rather than passed as arguments, which
        would put them in your shell history and in `ps` for anyone on the box.
        """;

    public static int Run(string[] args, Database database)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }

        if (database.Initialise(false) is { } error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var users = new UserRepository(database);
        var name = args.Length > 1 ? args[1] : null;

        switch (args[0])
        {
            case "add":
                return Add(users, name);
            case "passwd":
                return Passwd(users, name);
            case "list":
                return List(users);
            case "delete":
                return Delete(users, name);
            default:
                Console.Error.WriteLine($"unknown command: {args[0]}\n");
                Console.Error.WriteLine(Usage);
                return 2;
        }
    }

    static int Add(UserRepository users, string? name)
    {
        if (name is null) { Console.Error.WriteLine("which name? manifest user add <name>"); return 2; }

        var password = AskTwice();
        if (password is null) return 1;

        try
        {
            var user = users.Create(name, password);
            Console.WriteLine($"created {user.Username}"
                              + (user.IsOwner
                                  ? " — the first account, so it now owns anything that "
                                    + "was in the database already"
                                  : ""));
            return 0;
        }
        catch (AccountError e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    static int Passwd(UserRepository users, string? name)
    {
        if (name is null) { Console.Error.WriteLine("which name? manifest user passwd <name>"); return 2; }
        var user = users.ByName(name);
        if (user is null) { Console.Error.WriteLine($"no account called {name}"); return 1; }

        var password = AskTwice();
        if (password is null) return 1;

        try
        {
            users.SetPassword(user.Id, password);
            Console.WriteLine($"password changed for {user.Username}; "
                              + "any signed-in devices will have to sign in again");
            return 0;
        }
        catch (AccountError e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    static int List(UserRepository users)
    {
        var all = users.List();
        if (all.Count == 0)
        {
            Console.WriteLine("no accounts yet");
            return 0;
        }
        Console.WriteLine($"{"name",-22} {"created",-20} {"last seen",-20}");
        foreach (var u in all)
            Console.WriteLine($"{u.Username + (u.IsOwner ? " (owner)" : ""),-22} "
                              + $"{u.CreatedAt,-20} {u.LastSeen ?? "never",-20}");
        return 0;
    }

    static int Delete(UserRepository users, string? name)
    {
        if (name is null) { Console.Error.WriteLine("which name? manifest user delete <name>"); return 2; }
        var user = users.ByName(name);
        if (user is null) { Console.Error.WriteLine($"no account called {name}"); return 1; }

        Console.Write($"Delete {user.Username} and every card and deck it owns? "
                      + "This cannot be undone. Type the username to confirm: ");
        var typed = Console.ReadLine();
        if (!string.Equals(typed?.Trim(), user.Username, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("left alone");
            return 1;
        }

        users.Delete(user.Username);
        Console.WriteLine($"deleted {user.Username}");
        return 0;
    }

    /// <summary>Reads a password twice without echoing it, and checks they match.</summary>
    static string? AskTwice()
    {
        var first = Ask("Password: ");
        if (first is null) return null;
        var again = Ask("Again: ");
        if (again is null) return null;
        if (first != again)
        {
            Console.Error.WriteLine("those did not match");
            return null;
        }
        return first;
    }

    static string? Ask(string prompt)
    {
        Console.Write(prompt);

        // No console to turn echo off on when this runs from a script or a pipe,
        // in which case reading the line plainly is the only option left.
        if (Console.IsInputRedirected)
        {
            var piped = Console.ReadLine();
            return string.IsNullOrEmpty(piped) ? null : piped;
        }

        var typed = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0) typed.Length--;
                continue;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return null;
            }
            if (!char.IsControl(key.KeyChar)) typed.Append(key.KeyChar);
        }
        Console.WriteLine();
        return typed.Length == 0 ? null : typed.ToString();
    }
}
