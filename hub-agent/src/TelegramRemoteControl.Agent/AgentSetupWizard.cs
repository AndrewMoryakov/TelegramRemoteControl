using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TelegramRemoteControl.Agent;

public static class AgentSetupWizard
{
    public static bool TryRun(string settingsPath, AgentSettings settings, string[] args)
    {
        var forceSetup = HasArg(args, "--setup");
        var hubArg = GetArgValue(args, "--hub");
        var pairArg = GetArgValue(args, "--pair");
        var nameArg = GetArgValue(args, "--name");

        if (!string.IsNullOrWhiteSpace(hubArg))
            settings.HubUrl = hubArg.Trim();
        if (!string.IsNullOrWhiteSpace(nameArg))
            settings.FriendlyName = nameArg.Trim();

        if (!string.IsNullOrWhiteSpace(pairArg))
        {
            settings.PairingCode = pairArg.Trim();
            settings.AgentToken = string.Empty;
        }

        var hasCredential = !string.IsNullOrWhiteSpace(settings.AgentToken) ||
                            !string.IsNullOrWhiteSpace(settings.PairingCode);

        if (forceSetup || (!hasCredential && !Console.IsInputRedirected))
        {
            RunInteractive(settings);
        }

        var changed = forceSetup ||
                      !string.IsNullOrWhiteSpace(hubArg) ||
                      !string.IsNullOrWhiteSpace(pairArg) ||
                      !string.IsNullOrWhiteSpace(nameArg) ||
                      (!hasCredential && !Console.IsInputRedirected);

        if (changed)
            Persist(settingsPath, settings);

        return changed;
    }

    private static void RunInteractive(AgentSettings settings)
    {
        Console.WriteLine("=== Agent setup ===");
        Console.Write($"Hub URL [{settings.HubUrl}]: ");
        var hub = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(hub))
            settings.HubUrl = hub.Trim();

        Console.Write("Pairing code (from /addpc): ");
        var code = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(code))
        {
            settings.PairingCode = code.Trim();
            settings.AgentToken = string.Empty;
        }

        Console.Write($"Friendly name [{settings.FriendlyName}]: ");
        var name = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(name))
            settings.FriendlyName = name.Trim();
    }

    private static void Persist(string path, AgentSettings settings)
    {
        JsonObject root;
        if (File.Exists(path))
        {
            root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var agent = root["Agent"] as JsonObject ?? new JsonObject();
        agent["HubUrl"] = settings.HubUrl;
        agent["AgentToken"] = settings.AgentToken;
        agent["PairingCode"] = settings.PairingCode;
        agent["FriendlyName"] = settings.FriendlyName;
        root["Agent"] = agent;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, root.ToJsonString(options), Encoding.UTF8);
    }

    private static bool HasArg(string[] args, string name)
    {
        return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    return args[i + 1];
                return null;
            }

            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return arg[(name.Length + 1)..];
        }

        return null;
    }
}
