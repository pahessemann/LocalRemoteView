namespace LocalRemoteView.Host;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var config = HostConfig.Load();
        if (!config.IsValid)
        {
            using var setup = new SetupForm(config);
            if (setup.ShowDialog() != DialogResult.OK) return;
            config = setup.Config;
            config.Save();
        }
        Application.Run(new TrayApplicationContext(config));
    }
}
