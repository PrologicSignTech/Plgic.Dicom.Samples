// Anonymize: de-identifies every DICOM file in a folder in parallel with DicomBatchProcessor, and persists the salt
// so a later run produces the SAME pseudonyms and remapped UIDs (a study sent in two batches stays one study).
//
//   dotnet run --project samples/Anonymize -- <inputFolder> <outputFolder> [saltFile=anonymize.salt]
//
// The salt is a secret: anyone holding it can test whether a known UID or patient ID maps to a given pseudonym.
// Keep the salt file out of the output folder you hand to others.

using Plgic.Dicom.Anonymization;
using Plgic.Dicom.Batch;

if (args.Length is < 2 or > 3 || args[0].StartsWith('-'))
{
    Console.Error.WriteLine("""
        Usage: Anonymize <inputFolder> <outputFolder> [saltFile=anonymize.salt]

        De-identifies every file under <inputFolder> (recursively) into <outputFolder>, named by the new
        SOP Instance UID. The salt is read from <saltFile>, or generated and saved there on the first run;
        reuse the same salt file to keep pseudonyms and UIDs consistent across runs.
        """);
    return 2;
}

var (input, output) = (args[0], args[1]);
var saltFile = args.Length > 2 ? args[2] : "anonymize.salt";
if (!Directory.Exists(input))
{
    Console.Error.WriteLine($"Input folder not found: {input}");
    return 1;
}
Directory.CreateDirectory(output);

// The default profile is a pragmatic subset of the PS3.15 Basic Profile; tweak the rules for your site policy.
var profile = new AnonymizationProfile
{
    RemovePrivateTags = true,
    // DateShiftDays = -30,                                     // shift dates instead of keeping them
};
// profile.Rules[DicomTag.StudyDescription] = AnonymizeAction.Remove;

// Salt persistence: load it if we have one, otherwise let the anonymizer generate one and save it afterwards.
var hadSalt = File.Exists(saltFile);
if (hadSalt) profile.Salt = (await File.ReadAllTextAsync(saltFile)).Trim();

var anonymizer = new DicomAnonymizer(profile);
if (anonymizer.IsSaltGenerated)
{
    await File.WriteAllTextAsync(saltFile, anonymizer.Salt);
    Console.WriteLine($"Generated a new salt and saved it to {Path.GetFullPath(saltFile)}");
}
else
{
    Console.WriteLine($"Using the salt from {Path.GetFullPath(saltFile)}");
}

// Same salt => same mapping. Run the sample twice and this line prints the same UID both times.
Console.WriteLine($"Example remap: 1.2.3.4.5 -> {anonymizer.RemapUid("1.2.3.4.5")}");

// Non-DICOM files (a readme.txt, a JPEG) are not touched: the batch processor reports them as skipped.
var files = Directory.GetFiles(input, "*", SearchOption.AllDirectories);
var options = new DicomBatchOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
var report = await new DicomBatchProcessor().AnonymizeAsync(files, output, anonymizer, options);

foreach (var item in report.Items)
{
    if (item.Success)
        Console.WriteLine($"ok    {Path.GetFileName(item.Path)} -> new SOP Instance UID {item.Value!.NewSopInstanceUid} " +
                          $"(removed {item.Value.Removed}, emptied {item.Value.Emptied}, replaced {item.Value.Replaced}, " +
                          $"uids {item.Value.UidsRemapped}, private {item.Value.PrivateRemoved})");
    else if (item.Skipped)
        Console.WriteLine($"skip  {Path.GetFileName(item.Path)}: {item.Error}");
    else
        Console.WriteLine($"fail  {Path.GetFileName(item.Path)}: {item.Error}");
}

Console.WriteLine($"{report.Succeeded} anonymized, {report.Failed} failed, {report.Skipped} skipped, {report.WithIssues} with parse issues, " +
                  $"{report.FilesPerSecond:F0} files/s.");
return report.Failed == 0 ? 0 : 1;
