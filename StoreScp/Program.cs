// StoreScp: a C-STORE / C-ECHO SCP that saves every received object to a folder, hosted in the .NET Generic Host
// with the library's dependency-injection extensions. Stop it with Ctrl+C (the server drains open associations).
//
//   dotnet run --project samples/StoreScp -- <storageFolder> [port=11112] [aeTitle=STORESCP]
//
// Files land in <storageFolder>/<CallingAE>/<SOPInstanceUID>.dcm. Toolkit logs go to the console (via ILogger)
// and, as JSON lines per channel, to <storageFolder>/../plgic-logs.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plgic.Dicom;
using Plgic.Dicom.Logging;
using Plgic.Dicom.Network;

if (args.Length is < 1 or > 3 || args[0].StartsWith('-'))
{
    Console.Error.WriteLine("""
        Usage: StoreScp <storageFolder> [port=11112] [aeTitle=STORESCP]

        Accepts C-ECHO and C-STORE from any calling AE and writes each object to
        <storageFolder>/<CallingAE>/<SOPInstanceUID>.dcm. Ctrl+C to stop.
        """);
    return 2;
}

var storage = Path.GetFullPath(args[0]);
var port = 11112;
if (args.Length > 1 && (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
{
    Console.Error.WriteLine($"Invalid port: {args[1]}");
    return 2;
}
var aeTitle = args.Length > 2 ? args[2] : "STORESCP";
Directory.CreateDirectory(storage);

var builder = Host.CreateApplicationBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Toolkit-wide services: log router (file sink + forwarding into ILogger), parser defaults, anonymizer, batch processor.
builder.Services.AddPlgicDicom(o =>
{
    o.Logging.Directory = Path.Combine(Path.GetDirectoryName(storage) ?? storage, "plgic-logs");
    o.Logging.EnabledChannels = PlgicLogChannel.NetworkLogs | PlgicLogChannel.SafeLogs | PlgicLogChannel.ErrorLogs;
});

// The SCP as a hosted service. The built-in FileSystemStoreHandler does the writing; LoggingStoreHandler wraps it
// to show how to plug in your own logic (validation, routing, a database insert, ...).
builder.Services.AddSingleton(new FileSystemStoreHandler(storage));
builder.Services.AddPlgicDicomServer<LoggingStoreHandler>(o =>
{
    o.Port = port;
    o.AeTitle = aeTitle;
    o.RequireCalledAeMatch = false;          // set true to reject associations addressed to another AE title
    // o.AllowedCallingAeTitles = new HashSet<string> { "MODALITY1" };   // optional allow list
    o.SpoolDirectory = Path.Combine(Path.GetTempPath(), "plgic-scp-spool"); // big data sets spool here, not to memory
});

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILogger<Program>>();
log.LogInformation("StoreScp {AeTitle} listening on port {Port}, storing to {Storage}", aeTitle, port, storage);

await host.RunAsync();

var stats = host.Services.GetRequiredService<DicomServer>().Statistics;
Console.WriteLine($"Stopped. Associations: {stats.TotalAssociations}, stores: {stats.StoresCompleted}, failures: {stats.StoreFailures}, echoes: {stats.Echoes}, bytes: {stats.BytesReceived:N0}");
return 0;

/// <summary>Logs each received object, then delegates the write to <see cref="FileSystemStoreHandler"/>.</summary>
internal sealed class LoggingStoreHandler(FileSystemStoreHandler inner, ILogger<LoggingStoreHandler> log) : IDicomStoreHandler
{
    public async ValueTask<DicomStoreResult> OnStoreAsync(DicomStoreRequest request, CancellationToken ct)
    {
        var result = await inner.OnStoreAsync(request, ct);
        log.LogInformation("C-STORE from {CallingAE}: {SopClass} {SopInstance} ({Bytes:N0} bytes, {Syntax}) -> status 0x{Status:X4}",
            request.Association.CallingAE, request.SopClassUid, request.SopInstanceUid, request.DatasetLength,
            request.TransferSyntax, result.Status);
        return result;
    }
}
