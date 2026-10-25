// DumpFile: prints every element of a DICOM file (tag, VR, keyword, value), walks into sequences,
// and lists any parse issues. The parser never throws on bad data; problems are reported as issues.
//
//   dotnet run --project samples/DumpFile -- <file.dcm>
//   dotnet run --project samples/DumpFile -- --create-sample <out.dcm>   (writes a small CT test file)

using Plgic.Dicom;
using Plgic.Dicom.IO;

if (args.Length == 2 && args[0] == "--create-sample")
{
    await CreateSampleAsync(args[1]);
    Console.WriteLine($"Wrote {Path.GetFullPath(args[1])}");
    return 0;
}

if (args.Length != 1 || args[0].StartsWith('-'))
{
    Console.Error.WriteLine("""
        Usage:
          DumpFile <file.dcm>                  Print the file meta group, the data set and any parse issues.
          DumpFile --create-sample <out.dcm>   Write a small synthetic CT file to try it on.
        """);
    return 2;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 1;
}

// OpenAsync reads small values into memory; values above DicomParserOptions.LazyLoadThreshold (pixel data, big blobs)
// stay on disk as lazy references, so dumping a huge file is cheap. Dispose releases the file handle.
using var file = await DicomFile.OpenAsync(path);

Console.WriteLine($"# {Path.GetFileName(path)}  ({file.TransferSyntax}, preamble: {file.HadPreamble}, complete: {file.IsComplete})");
Console.WriteLine();
Console.WriteLine("## File meta information");
Dump(file.FileMetaInfo, 0);
Console.WriteLine();
Console.WriteLine("## Data set");
Dump(file.Dataset, 0);

if (file.HasIssues)
{
    Console.WriteLine();
    Console.WriteLine($"## Parse issues ({file.Issues.Count})");
    foreach (var issue in file.Issues) Console.WriteLine($"  {issue}");
}

return file.Issues.Any(i => i.Severity == DicomParseSeverity.Error) ? 3 : 0;

static void Dump(DicomDataset dataset, int depth)
{
    var indent = new string(' ', depth * 2);
    foreach (var item in dataset)
    {
        var head = $"{indent}{item.Tag} {item.VR.Code()} {DicomDictionary.KeywordOf(item.Tag),-32}";
        switch (item)
        {
            case DicomSequence sq:
                Console.WriteLine($"{head} ({sq.Items.Count} item{(sq.Items.Count == 1 ? "" : "s")})");
                for (var i = 0; i < sq.Items.Count; i++)
                {
                    Console.WriteLine($"{indent}  > Item {i + 1}");
                    Dump(sq.Items[i], depth + 2);
                }
                break;

            case DicomElement e:
                Console.WriteLine($"{head} {Format(e, dataset)}");
                break;

            case DicomBulkElement bulk:
                // Not loaded: only offset and length are known until someone reads it.
                Console.WriteLine($"{head} <{bulk.Length:N0} bytes, deferred>");
                break;

            case DicomFragmentSequence frags:
                Console.WriteLine($"{head} <encapsulated, {frags.Fragments.Count} fragment(s), deferred>");
                break;

            default:
                Console.WriteLine(head);
                break;
        }

        if (item.IsRecovered) Console.WriteLine($"{indent}    (recovered: {item.Recovery})");
    }
}

static string Format(DicomElement e, DicomDataset owner)
{
    if (e.Length == 0) return "<empty>";
    if (e.VR == DicomVR.UI && DicomUids.GetName(e.GetString(owner.Encoding)) is { } uidName)
        return $"[{e.GetString(owner.Encoding)}] {uidName}"; // well-known SOP class / transfer syntax UID
    if (e.VR.IsString()) return Truncate("[" + e.GetString(owner.Encoding) + "]");

    // Binary numbers: print the first few values. Anything else (OB, OW, UN, ...) as a short hex preview.
    if (e.VR is DicomVR.US or DicomVR.SS or DicomVR.UL or DicomVR.SL or DicomVR.FL or DicomVR.FD or DicomVR.UV or DicomVR.SV)
    {
        var values = e.Get<string[]>();
        return string.Join('\\', values.Take(8)) + (values.Length > 8 ? $" ... ({values.Length} values)" : "");
    }
    if (e.VR == DicomVR.AT) return string.Join('\\', Enumerable.Range(0, e.Length / 4).Select(i => e.Get<DicomTag>(i).ToString()));
    return $"<{e.Length:N0} bytes: {Convert.ToHexString(e.RawValue.Span[..Math.Min(16, e.Length)])}{(e.Length > 16 ? "..." : "")}>";
}

static string Truncate(string s) => s.Length <= 96 ? s : s[..93] + "...";

// A small CT image with one sequence and a private tag, enough to exercise the dump.
static async Task CreateSampleAsync(string output)
{
    var ds = new DicomDataset();
    ds.AddOrUpdate(DicomTag.SpecificCharacterSet, DicomVR.CS, "ISO_IR 192");
    ds.AddOrUpdate(DicomTag.SOPClassUID, DicomVR.UI, DicomUids.CTImageStorage);
    ds.AddOrUpdate(DicomTag.SOPInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(DicomTag.StudyDate, DicomVR.DA, "20260115");
    ds.AddOrUpdate(DicomTag.Modality, DicomVR.CS, "CT");
    ds.AddOrUpdate(DicomTag.PatientName, DicomVR.PN, "DOE^JANE");
    ds.AddOrUpdate(DicomTag.PatientID, DicomVR.LO, "PID-0042");
    ds.AddOrUpdate(DicomTag.PatientBirthDate, DicomVR.DA, "19700101");
    ds.AddOrUpdate(DicomTag.StudyInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(DicomTag.PixelSpacing, DicomVR.DS, "0.5", "0.5");
    ds.AddOrUpdate(DicomTag.SamplesPerPixel, DicomVR.US, (ushort)1);
    ds.AddOrUpdate(DicomTag.PhotometricInterpretation, DicomVR.CS, "MONOCHROME2");
    ds.AddOrUpdate(DicomTag.Rows, DicomVR.US, (ushort)64);
    ds.AddOrUpdate(DicomTag.Columns, DicomVR.US, (ushort)64);
    ds.AddOrUpdate(DicomTag.BitsAllocated, DicomVR.US, (ushort)16);
    ds.AddOrUpdate(DicomTag.BitsStored, DicomVR.US, (ushort)12);
    ds.AddOrUpdate(DicomTag.HighBit, DicomVR.US, (ushort)11);
    ds.AddOrUpdate(DicomTag.PixelRepresentation, DicomVR.US, (ushort)0);

    var reference = new DicomDataset();
    reference.AddOrUpdate(DicomTag.ReferencedSOPClassUID, DicomVR.UI, DicomUids.CTImageStorage);
    reference.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, DicomVR.UI, DicomUids.Generate());
    ds.AddOrUpdate(new DicomSequence(DicomTag.ReferencedImageSequence, [reference]));

    ds.AddOrUpdate(new DicomTag(0x0029, 0x0010), DicomVR.LO, "ACME PRIVATE");
    ds.AddOrUpdate(new DicomTag(0x0029, 0x1010), DicomVR.OB, new byte[] { 1, 2, 3, 4 }.AsMemory());

    var pixels = new byte[64 * 64 * 2];
    for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 7);
    ds.AddOrUpdate(DicomTag.PixelData, DicomVR.OW, pixels.AsMemory());

    // The file meta group (media storage UIDs, transfer syntax, implementation UID) is generated on save.
    using var file = new DicomFile(ds, DicomTransferSyntax.ExplicitVRLittleEndian);
    await file.SaveAsync(output);
}
