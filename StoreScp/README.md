# StoreScp

A C-STORE and C-ECHO SCP that saves every received object as `<storageFolder>/<CallingAE>/<SOPInstanceUID>.dcm`,
hosted in the .NET Generic Host. Stop it with Ctrl+C; open associations are drained first.

```
dotnet run --project StoreScp -- <storageFolder> [port=11112] [aeTitle=STORESCP]
```

What it demonstrates:

- `AddPlgicDicom` registers the toolkit services and the log router. Toolkit logs are forwarded to the host
  `ILogger` (the console here) and also written as JSON lines per channel to `<storageFolder>/../plgic-logs`.
- `AddPlgicDicomServer<THandler>` runs the SCP as a hosted service. The sample handler wraps the built-in
  `FileSystemStoreHandler` and logs each object; replace it with your own `IDicomStoreHandler` for validation,
  routing or database work.
- `DicomServerOptions`: port, AE title, called-AE check, calling-AE allow list, spool directory for large data sets.
- `DicomServer.Statistics`, printed on shutdown.

For a server without custom logic, `services.AddPlgicDicomServer(storageFolder, o => o.Port = 11112)` is enough.
