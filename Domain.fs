namespace Functional.Esapi.PqmExtractor

/// Represents the supported metric calculation kinds.
type MetricKind =
    | RelativeVolumeAtFullCourseDose of fullCourseDoseGy: float * structureId: string
    | DoseAtVolumeNeedsFractionNormalization of volumeCc: float * structureId: string

/// Represents one configured metric definition.
type MetricDefinition =
    {
        Id: string
        Kind: MetricKind
        Unit: string
    }

/// Represents one original prescription context inferred from PTV1.
type PrescriptionContext =
    {
        OriginalPrescriptionGy: float
        IntendedFractions: int
        ActualPtv1StructureId: string
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
        OriginalPrescriptionGy: string
        IntendedFractions: string
        CurrentFractions: string
        MayoQuery: string
        FullCourseDoseThresholdGy: string
        RawValue: string
        Value: string
        NormalizationFactor: string
        Unit: string
        Status: string
        Error: string
    }
