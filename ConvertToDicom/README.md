# ConvertToDicom

Wraps a PDF, JPEG, PNG or BMP in a DICOM Part 10 file with `DicomConverter`. The format is detected from the file
content, not the extension.

| Input | Result |
| --- | --- |
| PDF | Encapsulated PDF Storage, Explicit VR Little Endian |
| JPEG (baseline, extended, lossless) | Secondary Capture Image Storage, the JPEG kept byte for byte as one encapsulated fragment |
| PNG, BMP | Secondary Capture Image Storage, uncompressed RGB or MONOCHROME2 |
| several images | one multi-frame Secondary Capture instance (all JPEG, or all PNG/BMP, same size) |

```
dotnet run --project ConvertToDicom -- scan.pdf scan.dcm --patient-name "DOE^JANE" --patient-id 12345
dotnet run --project ConvertToDicom -- page1.jpg page2.jpg pages.dcm --study-uid 1.2.840.99.1
dotnet run --project ConvertToDicom -- requisition.jpg req.dcm --from-dicom study-image.dcm
dotnet run --project ConvertToDicom -- --extract scan.dcm scan-out.pdf
```

`--from-dicom` copies the patient and study attributes (and the Study Instance UID) from an existing file of the
study, so the converted document joins that study as a new series. Options given on the command line win over
copied values. Run without arguments for the full option list.

Progressive JPEGs are rejected: DICOM has no transfer syntax for them and the converter never re-compresses. Save
the image as baseline JPEG or PNG first. Exit codes: 1 file not found, 2 bad arguments, 3 input cannot be converted.

Check the result with the DumpFile sample:

```
dotnet run --project DumpFile -- scan.dcm
```

API used: `DicomConverter.FromFilesAsync`, `DicomConversionOptions` (including `CopyFrom`),
`DicomConverter.ExtractPdfAsync` / `ExtractJpegAsync`, `DicomFile.SaveAsync`.

See the [conversion guide](https://dicomkit.plogic.dev/Docs#conversion) for the attributes written and the limitations.
