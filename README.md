# Plgic.Dicom samples

Runnable console apps for [Plgic.Dicom](https://dicomkit.plogic.dev), a fast, fault-tolerant DICOM toolkit for
.NET 8, 9 and 10. Each sample is a small, self-contained project that uses the
[NuGet package](https://www.nuget.org/packages/Plgic.Dicom), so you can copy one straight into your own solution.

| Sample | What it shows |
| --- | --- |
| [DumpFile](DumpFile) | Parse a Part 10 file and print every element (tag, VR, keyword, value), sequences and parse issues. Can also write a small test file. |
| [EchoScu](EchoScu) | C-ECHO ("DICOM ping") to a remote SCP. |
| [StoreScp](StoreScp) | C-STORE / C-ECHO SCP that saves to a folder, hosted in the .NET Generic Host with `AddPlgicDicom` / `AddPlgicDicomServer`. |
| [StoreScu](StoreScu) | Send a file or a whole folder to an SCP over one association. |
| [Anonymize](Anonymize) | Parallel folder de-identification with `DicomBatchProcessor`, with a persisted salt for consistent pseudonyms and UIDs. |
| [ConvertToDicom](ConvertToDicom) | Wrap a PDF, JPEG, PNG or BMP in DICOM (Encapsulated PDF / Secondary Capture), attach it to an existing study, and extract it again. |
| [LargeFile](LargeFile) | Open a multi-gigabyte file metadata-only and stream its frames lazily with flat memory. |

## 1. Get a free Developer key

Plgic.Dicom needs a license key. For development and testing the
[Developer key is free](https://dicomkit.plogic.dev/DeveloperKey) (all features, one machine). Put it in an
environment variable once:

```powershell
setx PLGIC_LICENSE_KEY "YOUR-KEY"        # Windows (open a new terminal afterwards)
```

```bash
export PLGIC_LICENSE_KEY="YOUR-KEY"      # Linux / macOS
```

The library activates the key online on first use and then works offline.

## 2. Run a sample

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer.

```
dotnet run --project DumpFile -- --create-sample test.dcm
dotnet run --project DumpFile -- test.dcm
```

A quick end-to-end network test on one machine (in separate terminals):

```
dotnet run --project StoreScp -- ./received 11112 STORESCP
dotnet run --project EchoScu  -- 127.0.0.1 11112 STORESCP
dotnet run --project StoreScu -- 127.0.0.1 11112 test.dcm STORESCP
```

Run any sample without arguments to see its options.

## Links

- Documentation: https://dicomkit.plogic.dev/Docs
- Pricing (no runtime fees, ship unlimited copies): https://dicomkit.plogic.dev/Pricing
- Support: https://dicomkit.plogic.dev/Contact

The sample code in this repository is MIT licensed (see [LICENSE](LICENSE)); use it freely in your own projects.
Plgic.Dicom itself is commercial software under its own license agreement, shipped in the NuGet package.

Plgic.Dicom is a developer library, not a medical device.
