using System.Diagnostics;
using System.Reflection;

namespace LocalRemoteView.Installer;

internal static class Program
{
    private const string TaskName = "LocalRemoteView Host";
    private const string FirewallName = "LocalRemoteView (LAN)";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var answer = MessageBox.Show(
            "Installer LocalRemoteView sur ce PC ?\n\nL’agent démarrera automatiquement à l’ouverture de votre session. La configuration initiale et la clé d’appairage seront affichées après l’installation.",
            "Installation de LocalRemoteView", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (answer != DialogResult.OK) return;

        try
        {
            var installDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LocalRemoteView");
            var hostPath = Path.Combine(installDirectory, "LocalRemoteView.Host.exe");
            Directory.CreateDirectory(installDirectory);
            StopExistingHost();
            ExtractHost(hostPath);
            CreateStartupTask(hostPath);
            ConfigureFirewall(hostPath);
            Process.Start(new ProcessStartInfo(hostPath) { UseShellExecute = true });
            MessageBox.Show("Installation terminée.\n\nLa fenêtre de configuration initiale va s’ouvrir. Copiez sa clé pour la saisir dans LocalRemoteView sur l’autre PC.", "LocalRemoteView", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"L’installation a échoué :\n\n{ex.Message}", "LocalRemoteView", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExtractHost(string destination)
    {
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("LocalRemoteView.HostPayload.exe")
            ?? throw new InvalidOperationException("Le composant hôte intégré est introuvable.");
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
    }

    private static void StopExistingHost()
    {
        foreach (var process in Process.GetProcessesByName("LocalRemoteView.Host"))
            try { process.Kill(true); process.WaitForExit(3000); } catch { }
    }

    private static void CreateStartupTask(string hostPath)
    {
        Run("schtasks.exe", "/Create", "/TN", TaskName, "/TR", $"\"{hostPath}\"", "/SC", "ONLOGON", "/RL", "HIGHEST", "/F");
    }

    private static void ConfigureFirewall(string hostPath)
    {
        RunAllowFailure("netsh.exe", "advfirewall", "firewall", "delete", "rule", $"name={FirewallName}");
        Run("netsh.exe", "advfirewall", "firewall", "add", "rule", $"name={FirewallName}", "dir=in", "action=allow", "protocol=TCP", $"program={hostPath}", "profile=private", "remoteip=localsubnet");
    }

    private static void Run(string file, params string[] args)
    {
        var result = Start(file, args);
        if (result.ExitCode != 0) throw new InvalidOperationException($"La commande {file} a échoué : {result.Error}");
    }
    private static void RunAllowFailure(string file, params string[] args) => Start(file, args);
    private static (int ExitCode, string Error) Start(string file, string[] args)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Impossible de démarrer {file}.");
        var error = process.StandardError.ReadToEnd(); process.WaitForExit(); return (process.ExitCode, error);
    }
}
