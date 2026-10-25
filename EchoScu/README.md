# EchoScu

Sends one C-ECHO to an SCP and prints the status and round-trip time. Exit code 0 on status Success.

```
dotnet run --project EchoScu -- <host> <port> [calledAE=ANY_SCP] [callingAE=PLGIC_SCU]
dotnet run --project EchoScu -- 127.0.0.1 11112 STORESCP
```

An association rejection (wrong called AE title, calling AE not allowed) is reported with the result, source and
reason codes of the A-ASSOCIATE-RJ.

API used: `DicomClient.ConnectAsync`, `DicomClientOptions`, `DicomClient.EchoAsync`, `DicomClient.ReleaseAsync`,
`DicomAssociationRejectedException`.
