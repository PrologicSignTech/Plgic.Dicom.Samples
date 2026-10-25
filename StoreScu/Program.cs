// StoreScu: sends one DICOM file, or every file in a folder (recursively), to a C-STORE SCP over one association.
//
//   dotnet run --project samples/StoreScu -- <host> <port> <file-or-folder> [calledAE] [callingAE]
//   dotnet run --project samples/StoreScu -- 127.0.0.1 11112 ./images STORESCP

using System.Diagnostics;
using System.Globalization;
using Plgic.Dicom;
using Plgic.Dicom.IO;
using Plgic.Dicom.Network;

if (args.Length is < 3 or > 5 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
{
    Console.Error.WriteLine("""
        Usage: StoreScu <host> <port> <file-or-folder> [calledAE=ANY_SCP] [callingAE=PLGIC_SCU]

        Sends a file, or every file under a folder, with C-STORE on a single association.
        Non-DICOM files are skipped. Exit code 0 when every file was stored with status Success.
        """);
    return 2;
}

var (host, input) = (args[0], args[2]);
string[] paths = Directory.Exists(input) ? Directory.GetFiles(input, "*", SearchOption.AllDirectories)
    : File.Exists(input) ? [input]
    : [];
if (paths.Length == 0)
{
    Console.Error.WriteLine($"Nothing to send: {input}");
    return 1;
}

// Pass 1: a metadata-only parse of each file (stops at Pixel Data, never reads it) to learn which SOP classes and
// transfer syntaxes to propose. An association must announce them up front.
var sopClasses = new HashSet<string>(StringComparer.Ordinal);
var syntaxes = new List<string> { DicomUids.ExplicitVRLittleEndian, DicomUids.ImplicitVRLittleEndian };
var sendable = new List<string>();
foreach (var path in paths)
{
    using var meta = await DicomStreamParser.ParseMetadataAsync(path);
    var sopClass = meta.Dataset.GetString(DicomTag.SOPClassUID);
    if (string.IsNullOrEmpty(sopClass) || meta.Dataset.GetString(DicomTag.SOPInstanceUID) is null)
    {
        Console.WriteLine($"skip  {path} (not a DICOM instance)");
        continue;
    }
    sopClasses.Add(sopClass);
    if (!syntaxes.Contains(meta.TransferSyntax.Uid)) syntaxes.Add(meta.TransferSyntax.Uid); // e.g. JPEG files stay compressed
    sendable.Add(path);
}

if (sopClasses.Count > DicomClient.MaxStorageSopClasses)
{
    Console.Error.WriteLine($"{sopClasses.Count} SOP classes exceed one association's limit ({DicomClient.MaxStorageSopClasses}); split the folder.");
    return 1;
}

var options = new DicomClientOptions
{
    CalledAE = args.Length > 3 ? args[3] : "ANY_SCP",
    CallingAE = args.Length > 4 ? args[4] : "PLGIC_SCU",
    StorageSopClasses = [.. sopClasses],
    TransferSyntaxes = syntaxes,
};

// Pass 2: send. OpenAsync keeps pixel data on disk; StoreAsync streams it into PDUs, so file size does not matter.
var (ok, failed) = (0, 0);
var sw = Stopwatch.StartNew();
try
{
    await using var client = await DicomClient.ConnectAsync(host, port, options);
    foreach (var path in sendable)
    {
        try
        {
            using var file = await DicomFile.OpenAsync(path);
            var status = await client.StoreAsync(file);
            Console.WriteLine($"0x{status:X4}  {path}");
            if (status == DimseStatus.Success) ok++; else failed++;
        }
        catch (InvalidOperationException ex) // e.g. the SCP accepted no presentation context for this SOP class / syntax
        {
            Console.WriteLine($"fail  {path}: {ex.Message}");
            failed++;
        }
        if (client.IsFaulted) { Console.Error.WriteLine("Association lost."); break; }
    }
    if (!client.IsFaulted) await client.ReleaseAsync();
}
catch (DicomAssociationRejectedException ex)
{
    Console.Error.WriteLine($"Association rejected: result {ex.Result}, source {ex.RejectSource}, reason {ex.Reason}");
    return 1;
}
catch (Exception ex) // connection refused, timeout, protocol error
{
    Console.Error.WriteLine($"Error talking to {host}:{port}: {ex.Message}");
    return 1;
}

Console.WriteLine($"Sent {ok} of {sendable.Count} in {sw.Elapsed.TotalSeconds:F1} s ({failed} failed, {paths.Length - sendable.Count} skipped).");
return failed == 0 && ok == sendable.Count ? 0 : 1;
