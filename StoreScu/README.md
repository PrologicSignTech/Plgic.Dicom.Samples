# StoreScu

Sends a file, or every file under a folder (recursively), to a C-STORE SCP over a single association.
Non-DICOM files are skipped.

```
dotnet run --project StoreScu -- <host> <port> <file-or-folder> [calledAE=ANY_SCP] [callingAE=PLGIC_SCU]
dotnet run --project StoreScu -- 127.0.0.1 11112 ./images STORESCP
```

How it works:

1. A metadata-only parse of every file (`DicomStreamParser.ParseMetadataAsync`, which stops at Pixel Data) collects
   the SOP classes and transfer syntaxes to propose. An association must announce them up front, at most
   `DicomClient.MaxStorageSopClasses` of them.
2. Each file is opened with `DicomFile.OpenAsync` and sent with `DicomClient.StoreAsync`. Pixel data stays on disk
   and is streamed into PDUs, so file size does not matter. Compressed files are sent unchanged when the SCP
   accepts their transfer syntax.

Exit code 0 when every file was stored with status Success.
