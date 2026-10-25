# LargeFile

Opens a DICOM file of any size metadata-only and streams its frames lazily. Only the header is parsed; pixel data
stays on disk and each frame is read on demand, so memory use stays flat for multi-gigabyte files.

```
dotnet run -c Release --project samples/LargeFile -- <file.dcm> [maxFrames]
dotnet run -c Release --project samples/LargeFile -- --generate <out.dcm> <sizeMB>
```

`--generate` writes a synthetic 512x512 16-bit multi-frame CT of about `sizeMB` megabytes. The pixel bytes come
from a custom `DicomBulkSource`, so even a multi-GB file is written without being held in memory.

What it demonstrates:

- `DicomStreamParser.ParseMetadataAsync`: stops after Pixel Data without reading it.
- `LazyDicomPixelData.TryCreate` and `PrepareAsync`: locate frames (native, or encapsulated via offset tables or a
  fragment scan) without reading pixels.
- Random access with `GetFrameLengthAsync` and `ReadFrameAsync` into your own buffer.
- Sequential streaming with `GetFramesAsync`: one reused pooled buffer, so copy a frame if you keep it.

Keep the `DicomFile` open while frames are read, and dispose it when done.
