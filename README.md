# Functional ESAPI PQM Extractor

This repository contains a small F# console application for extracting plan-quality metrics from Eclipse via ESAPI and ESAPIX Mayo queries.

The current workflow is:

1. Open a configured patient list.
2. Filter to matching session courses and plans.
3. Infer the original full-course prescription from `PTV1`.
4. Build prescription-aware Mayo queries.
5. Execute the queries through ESAPIX.
6. Normalize dose-at-volume results back to full-course fraction context.
7. Write the main extract and debug CSV files.

The code is intentionally organized as small modules with explicit data flow:

- [Config.fs](./Config.fs) defines the patient list and metric intent.
- [EsapiQuery.fs](./EsapiQuery.fs) handles patient access, plan filtering, and structure matching.
- [Prescription.fs](./Prescription.fs) infers original prescription context from structure naming.
- [MetricExtraction.fs](./MetricExtraction.fs) builds Mayo queries, executes them, and normalizes results.
- [Program.fs](./Program.fs) coordinates the application and writes CSV outputs.

Build locally with:

```powershell
dotnet build
```

Further notes live in:

- [docs/fsharp-and-architecture.md](./docs/fsharp-and-architecture.md)
- [docs/problem-solving-ethos.md](./docs/problem-solving-ethos.md)
