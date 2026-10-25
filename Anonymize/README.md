# Anonymize

De-identifies every DICOM file under a folder in parallel with `DicomBatchProcessor.AnonymizeAsync`, writing the
results to an output folder, each named by its new SOP Instance UID. Non-DICOM files are reported as skipped and
left alone.

```
dotnet run --project Anonymize -- <inputFolder> <outputFolder> [saltFile=anonymize.salt]
```

## Salt persistence

UID remapping and ID pseudonyms are deterministic for a given salt. Without one, `DicomAnonymizer` generates a
random salt per instance, so two runs would give the same study two different sets of new UIDs. The sample:

- reads the salt from `saltFile` when it exists and sets `AnonymizationProfile.Salt`;
- otherwise lets the anonymizer generate one (`IsSaltGenerated`) and saves `DicomAnonymizer.Salt` to the file.

Run it twice on the same input: the "Example remap" line and the output file names are identical, so a study
de-identified in two batches stays one study. Treat the salt as a secret (whoever holds it can check whether a
known UID or ID maps to a given pseudonym) and keep it out of the folder you share.

The profile defaults to a pragmatic subset of the PS3.15 Basic Application Confidentiality Profile; edit
`AnonymizationProfile.Rules`, `RemovePrivateTags` and `DateShiftDays` for your site policy.
