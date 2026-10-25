// LargeFile: opens a (multi-gigabyte) DICOM file metadata-only and streams its frames lazily. Only the header is
// parsed; pixel data stays on disk and each frame is read on demand into a pooled buffer, so memory stays flat no
// matter how big the file is.
//
//   dotnet run -c Release --project samples/LargeFile -- <file.dcm> [maxFrames]
//   dotnet run -c Release --project samples/LargeFile -- --generate <out.dcm> <sizeMB>   (synthetic multi-frame file)

using System.Diagnostics;
using System.Globalization;
using Plgic.Dicom;
using Plgic.Dicom.IO;

if (args.Length == 3 && args[0] == "--generate" && int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var sizeMb) && sizeMb > 0)
{
    await GenerateAsync(args[1], sizeMb);
    return 0;
}

var maxFrames = int.MaxValue;
if (args.Length is < 1 or > 2 || args[0].StartsWith('-') ||
    (args.Length == 2 && (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out maxFrames) || maxFrames < 1)))
{
    Console.Error.WriteLine("""
        Usage:
          LargeFile <file.dcm> [maxFrames]            Open metadata-only, then stream frames lazily (all, or the first N).
          LargeFile --generate <out.dcm> <sizeMB>     Write a synthetic multi-frame CT of about sizeMB megabytes.
        """);
    return 2;
}

var path = args[0];
var sw = Stopwatch.StartNew();

// Metadata-only: parsing stops right after Pixel Data, whose bytes are never read or copied. The returned file keeps
// the file handle open so frames can be read later; dispose it when done.
using var file = await DicomStreamParser.ParseMetadataAsync(path);
Console.WriteLine($"Opened {Path.GetFileName(path)} ({new FileInfo(path).Length:N0} bytes) metadata-only in {sw.ElapsedMilliseconds} ms");
Console.WriteLine($"  Patient: {file.Dataset.GetString(DicomTag.PatientName)}  Modality: {file.Dataset.GetString(DicomTag.Modality)}  Syntax: {file.TransferSyntax}");
foreach (var issue in file.Issues) Console.WriteLine($"  issue: {issue}");

if (!LazyDicomPixelData.TryCreate(file.Dataset, out var pixels) || pixels is null)
{
    Console.Error.WriteLine("The file has no usable pixel data.");
    return 1;
}

// Locates frames (offset tables / fragment scan for compressed data) without reading pixel bytes.
await pixels.PrepareAsync();
Console.WriteLine($"  {pixels.FrameCount} frame(s) of {pixels.Columns}x{pixels.Rows}, {pixels.BitsAllocated} bits, " +
                  $"{(pixels.IsEncapsulated ? "encapsulated" : $"{pixels.FrameLength:N0} bytes each")}, pixel data {pixels.Length:N0} bytes");

// Random access: read just the last frame into a buffer we own.
var last = pixels.FrameCount - 1;
var buffer = new byte[await pixels.GetFrameLengthAsync(last)];
var read = await pixels.ReadFrameAsync(last, buffer);
Console.WriteLine($"  Random access: frame {last} = {read:N0} bytes, checksum {Checksum(buffer.AsSpan(0, read)):X8}");

// Sequential streaming: each frame arrives in one reused pooled buffer, valid only until the loop moves on.
sw.Restart();
long bytes = 0;
var frames = 0;
var peakWorkingSet = Environment.WorkingSet;
await foreach (var frame in pixels.GetFramesAsync())
{
    bytes += frame.Data.Length;
    if (++frames % 64 == 0) peakWorkingSet = Math.Max(peakWorkingSet, Environment.WorkingSet);
    if (frames >= maxFrames) break;
}
var seconds = Math.Max(sw.Elapsed.TotalSeconds, 1e-6);
Console.WriteLine($"  Streamed {frames} frame(s), {bytes:N0} bytes in {sw.Elapsed.TotalSeconds:F2} s " +
                  $"({bytes / seconds / (1 << 20):F0} MB/s); peak working set {peakWorkingSet / (1 << 20)} MB");
return 0;

static uint Checksum(ReadOnlySpan<byte> data)
{
    uint a = 1, b = 0; // Adler-32: enough to show two reads of a frame agree
    foreach (var x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
    return (b << 16) | a;
}

// Writes a 512x512 16-bit multi-frame CT. Pixel bytes come from a synthetic source, so even a multi-GB file is
// written without ever being held in memory: the writer streams any DicomBulkSource.
static async Task GenerateAsync(string output, int sizeMb)
{
    const int rows = 512, columns = 512;
    const long frameLength = rows * columns * 2;
    var frames = (int)Math.Max(1, (long)sizeMb * (1 << 20) / frameLength);
    var length = frames * frameLength;

    var ds = new DicomDataset();
    ds.AddOrUpdate(DicomTag.SOPClassUID, DicomVR.UI, DicomUids.EnhancedCTImageStorage);
    ds.AddOrUpdate(DicomTag.SOPInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(DicomTag.Modality, DicomVR.CS, "CT");
    ds.AddOrUpdate(DicomTag.PatientName, DicomVR.PN, "LARGE^FILE");
    ds.AddOrUpdate(DicomTag.PatientID, DicomVR.LO, "BIG-1");
    ds.AddOrUpdate(DicomTag.StudyInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(DicomTag.SamplesPerPixel, DicomVR.US, (ushort)1);
    ds.AddOrUpdate(DicomTag.PhotometricInterpretation, DicomVR.CS, "MONOCHROME2");
    ds.AddOrUpdate(DicomTag.NumberOfFrames, frames);
    ds.AddOrUpdate(DicomTag.Rows, DicomVR.US, (ushort)rows);
    ds.AddOrUpdate(DicomTag.Columns, DicomVR.US, (ushort)columns);
    ds.AddOrUpdate(DicomTag.BitsAllocated, DicomVR.US, (ushort)16);
    ds.AddOrUpdate(DicomTag.BitsStored, DicomVR.US, (ushort)12);
    ds.AddOrUpdate(DicomTag.HighBit, DicomVR.US, (ushort)11);
    ds.AddOrUpdate(DicomTag.PixelRepresentation, DicomVR.US, (ushort)0);
    ds.AddOrUpdate(new DicomBulkElement(DicomTag.PixelData, DicomVR.OW, new DicomLazyValue(new PatternSource(length), 0, length)));

    var sw = Stopwatch.StartNew();
    using var file = new DicomFile(ds, DicomTransferSyntax.ExplicitVRLittleEndian);
    await file.SaveAsync(output);
    Console.WriteLine($"Wrote {Path.GetFullPath(output)}: {frames} frames, {new FileInfo(output).Length:N0} bytes in {sw.Elapsed.TotalSeconds:F1} s");
}

/// <summary>Deterministic bytes computed from the position; lets the generator write gigabytes from no memory.</summary>
internal sealed class PatternSource(long length) : DicomBulkSource
{
    public override long Length => length;

    public override ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken ct = default) =>
        ValueTask.FromResult(Read(offset, destination.Span));

    public override int Read(long offset, Span<byte> destination)
    {
        var n = (int)Math.Clamp(length - offset, 0, destination.Length);
        for (var i = 0; i < n; i++) destination[i] = (byte)((offset + i) * 31 >> 3);
        return n;
    }
}
