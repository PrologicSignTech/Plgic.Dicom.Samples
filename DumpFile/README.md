# DumpFile

Prints the File Meta Information and the data set of a DICOM Part 10 file: tag, VR, keyword and value for every
element, indented into sequence items, followed by any parse issues. Large values (pixel data, big blobs) are not
loaded; they are shown with their length only.

```
dotnet run --project DumpFile -- <file.dcm>
dotnet run --project DumpFile -- --create-sample <out.dcm>
```

`--create-sample` writes a small synthetic CT (with a sequence and a private tag) to try this and the other
samples on.

The parser never throws on malformed input. Problems such as truncation or bad lengths are reported as
`DicomParseIssue`s (severity, code, byte offset, tag) and whatever could be read is still returned. The exit code
is 3 when an issue of severity Error was found.

API used: `DicomFile.OpenAsync`, `DicomDataset` enumeration, `DicomElement` / `DicomSequence` / `DicomBulkElement` /
`DicomFragmentSequence`, `DicomDictionary.KeywordOf`, `DicomFile.Issues`, `DicomFile.SaveAsync`.
