using System.Runtime.CompilerServices;
using Plgic.Dicom.Licensing;

// Shared by every sample (linked in Directory.Build.props): a missing or invalid license key prints one clear line
// instead of a stack trace. Get a free Developer key at https://dicomkit.plogic.dev/DeveloperKey and set it in the
// PLGIC_LICENSE_KEY environment variable (see README.md).
internal static class LicenseHint
{
    [ModuleInitializer]
    internal static void Register() =>
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not PlgicLicenseException ex) return;
            Console.Error.WriteLine();
            Console.Error.WriteLine("License key needed: " + ex.Message);
            Console.Error.WriteLine("Then set it once:  setx PLGIC_LICENSE_KEY \"YOUR-KEY\"   (Linux/macOS: export PLGIC_LICENSE_KEY=...)");
            Console.Error.Flush();
            Environment.Exit(10);
        };
}
