// EchoScu: sends a C-ECHO ("DICOM ping") to a remote SCP and prints the status and round-trip time.
//
//   dotnet run --project samples/EchoScu -- <host> <port> [calledAE] [callingAE]
//   dotnet run --project samples/EchoScu -- 127.0.0.1 11112 STORESCP

using System.Diagnostics;
using System.Globalization;
using Plgic.Dicom.Network;

if (args.Length is < 2 or > 4 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
{
    Console.Error.WriteLine("""
        Usage: EchoScu <host> <port> [calledAE=ANY_SCP] [callingAE=PLGIC_SCU]

        Opens an association, sends one C-ECHO, releases. Exit code 0 when the SCP answers Success.
        """);
    return 2;
}

var options = new DicomClientOptions
{
    CalledAE = args.Length > 2 ? args[2] : "ANY_SCP",
    CallingAE = args.Length > 3 ? args[3] : "PLGIC_SCU",
    ConnectTimeout = TimeSpan.FromSeconds(10),
    // Verification is always proposed, so no StorageSopClasses are needed for an echo.
};

var sw = Stopwatch.StartNew();
try
{
    await using var client = await DicomClient.ConnectAsync(args[0], port, options);
    var status = await client.EchoAsync();
    await client.ReleaseAsync();

    Console.WriteLine($"C-ECHO {args[0]}:{port} ({options.CallingAE} -> {options.CalledAE}): status 0x{status:X4} in {sw.ElapsedMilliseconds} ms");
    return status == DimseStatus.Success ? 0 : 1;
}
catch (DicomAssociationRejectedException ex)
{
    // Typical causes: wrong called AE title, or our calling AE is not on the SCP's allow list.
    Console.Error.WriteLine($"Association rejected: result {ex.Result}, source {ex.RejectSource}, reason {ex.Reason}");
    return 1;
}
catch (Exception ex) // connection refused, timeout, protocol error
{
    Console.Error.WriteLine($"Could not reach {args[0]}:{port}: {ex.Message}");
    return 1;
}
