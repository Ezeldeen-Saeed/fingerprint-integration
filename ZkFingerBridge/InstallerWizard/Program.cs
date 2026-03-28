using System.Windows.Forms;

namespace InstallerWizard;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        
        // Parse command line arguments
        var silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase) ||
                     args.Contains("/silent", StringComparer.OrdinalIgnoreCase);
        
        var keyArg = args.FirstOrDefault(a => a.StartsWith("--key=", StringComparison.OrdinalIgnoreCase) ||
                                               a.StartsWith("/key=", StringComparison.OrdinalIgnoreCase));
        var installationKey = keyArg?.Split('=', 2).LastOrDefault();
        
        if (silent && !string.IsNullOrEmpty(installationKey))
        {
            // Silent installation
            var installer = new Services.InstallerService();
            var result = installer.InstallSilent(installationKey).GetAwaiter().GetResult();
            Environment.Exit(result ? 0 : 1);
        }
        else
        {
            // GUI wizard
            Application.Run(new Forms.MainWizardForm());
        }
    }
}
