// ConvertToDicom: wraps a PDF, JPEG, PNG or BMP (detected from the file content) in a DICOM Part 10 file.
//   PDF            -> Encapsulated PDF Storage
//   JPEG           -> Secondary Capture, the JPEG bit stream kept as-is (no re-compression)
//   PNG / BMP      -> Secondary Capture, uncompressed
//   several images -> one multi-frame Secondary Capture (all JPEG, or all PNG/BMP, same size)
//
//   dotnet run --project samples/ConvertToDicom -- scan.pdf scan.dcm --patient-name "DOE^JANE" --patient-id 12345
//   dotnet run --project samples/ConvertToDicom -- requisition.jpg req.dcm --from-dicom study-image.dcm
//   dotnet run --project samples/ConvertToDicom -- --extract scan.dcm scan-out.pdf

using System.Globalization;
using Plgic.Dicom;
using Plgic.Dicom.Conversion;
using Plgic.Dicom.IO;

const string Usage = """
    Usage:
      ConvertToDicom <input> [<input> ...] <out.dcm> [options]
      ConvertToDicom --extract <in.dcm> <out.pdf|out.jpg>

    Options:
      --patient-name <PN>      e.g. "DOE^JANE"
      --patient-id <id>
      --birth-date <yyyy-mm-dd>
      --sex <M|F|O>
      --accession <number>
      --study-uid <uid>        attach to this existing study
      --series-uid <uid>       put the instance in this series
      --study-desc <text>
      --series-desc <text>
      --title <text>           Document Title (PDF; defaults to the file name)
      --institution <name>
      --conversion-type <CS>   WSD (default), SD, SI, DI, DV, DF, SYN
      --from-dicom <file.dcm>  copy patient and study attributes from an existing DICOM file
    """;

if (args.Length == 3 && args[0] == "--extract") return await ExtractAsync(args[1], args[2]);

var inputs = new List<string>();
var options = new DicomConversionOptions();
string? fromDicom = null;
try
{
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (!a.StartsWith("--", StringComparison.Ordinal)) { inputs.Add(a); continue; }
        if (i + 1 >= args.Length) throw new ArgumentException($"{a} needs a value.");
        var v = args[++i];
        switch (a)
        {
            case "--patient-name": options.PatientName = v; break;
            case "--patient-id": options.PatientId = v; break;
            case "--birth-date": options.PatientBirthDate = DateOnly.Parse(v, CultureInfo.InvariantCulture); break;
            case "--sex": options.PatientSex = v; break;
            case "--accession": options.AccessionNumber = v; break;
            case "--study-uid": options.StudyInstanceUid = v; break;
            case "--series-uid": options.SeriesInstanceUid = v; break;
            case "--study-desc": options.StudyDescription = v; break;
            case "--series-desc": options.SeriesDescription = v; break;
            case "--title": options.DocumentTitle = v; break;
            case "--institution": options.InstitutionName = v; break;
            case "--conversion-type": options.ConversionType = v; break;
            case "--from-dicom": fromDicom = v; break;
            default: throw new ArgumentException($"Unknown option {a}.");
        }
    }
    if (inputs.Count < 2) throw new ArgumentException("Give at least one input file and the output file.");
}
catch (Exception ex) when (ex is ArgumentException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 2;
}

var output = inputs[^1];
inputs.RemoveAt(inputs.Count - 1);
foreach (var input in inputs.Append(fromDicom))
{
    if (input is null || File.Exists(input)) continue;
    Console.Error.WriteLine($"File not found: {input}");
    return 1;
}

DicomFile? source = null;
try
{
    if (fromDicom is not null)
    {
        // Typically an image of the study the document belongs to: the new instance joins that study as a new series.
        source = await DicomFile.OpenAsync(fromDicom);
        options.CopyFrom = source.Dataset;
    }

    using var file = await DicomConverter.FromFilesAsync(inputs, options);
    await file.SaveAsync(output);

    var ds = file.Dataset;
    Console.WriteLine($"Wrote {Path.GetFullPath(output)}");
    Console.WriteLine($"  SOP Class        {ds.GetString(DicomTag.SOPClassUID)}");
    Console.WriteLine($"  Transfer syntax  {file.TransferSyntax.Uid}");
    Console.WriteLine($"  Patient          {ds.GetString(DicomTag.PatientName)} / {ds.GetString(DicomTag.PatientID)}");
    Console.WriteLine($"  Study UID        {ds.GetString(DicomTag.StudyInstanceUID)}");
    Console.WriteLine($"  Series UID       {ds.GetString(DicomTag.SeriesInstanceUID)}");
    Console.WriteLine($"  SOP Instance UID {ds.GetString(DicomTag.SOPInstanceUID)}");
    if (ds.Contains(DicomTag.Rows))
        Console.WriteLine($"  Image            {ds.GetString(DicomTag.Columns)}x{ds.GetString(DicomTag.Rows)} {ds.GetString(DicomTag.PhotometricInterpretation)}, {ds.GetString(DicomTag.NumberOfFrames, "1")} frame(s)");
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
{
    // Not a PDF/JPEG/PNG/BMP, a progressive JPEG, frames that do not match, an invalid UID ...
    Console.Error.WriteLine($"Cannot convert: {ex.Message}");
    return 3;
}
finally
{
    source?.Dispose();
}

static async Task<int> ExtractAsync(string input, string output)
{
    if (!File.Exists(input))
    {
        Console.Error.WriteLine($"File not found: {input}");
        return 1;
    }
    using var file = await DicomFile.OpenAsync(input);
    try
    {
        var bytes = file.Dataset.Contains(DicomTag.PixelData)
            ? await DicomConverter.ExtractJpegAsync(file)
            : await DicomConverter.ExtractPdfAsync(file);
        await File.WriteAllBytesAsync(output, bytes);
        Console.WriteLine($"Wrote {bytes.Length} bytes to {Path.GetFullPath(output)}");
        return 0;
    }
    catch (InvalidDataException ex)
    {
        Console.Error.WriteLine($"Cannot extract: {ex.Message}");
        return 3;
    }
}
