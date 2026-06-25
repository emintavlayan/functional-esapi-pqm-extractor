namespace Functional.Esapi.PqmExtractor

/// Represents the supported metric calculation kinds.
type MetricKind =
    | MayoQuery of query: string * structureId: string

/// Represents one configured metric definition.
type MetricDefinition =
    {
        Id: string
        Kind: MetricKind
        Unit: string
    }

/// Represents one extracted metric row in the CSV output.
type MetricRow =
    {
        PatientId: string
        CourseId: string
        PlanId: string
        Backend: string
        RequestedStructureId: string
        ActualStructureId: string
        MetricId: string
        MayoQuery: string
        Value: string
        Unit: string
        Status: string
        Error: string
    }
