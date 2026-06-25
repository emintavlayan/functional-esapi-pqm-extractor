namespace Functional.Esapi.PqmExtractor

open System
open System.Globalization
open FsToolkit.ErrorHandling
open VMS.TPS.Common.Model.API
open ESAPIX.Extensions

module MetricExtraction =

    /// Returns the metric backend label used in CSV outputs.
    let backendName = "ESAPIX-Mayo"

    /// Formats one floating-point value for CSV output.
    let floatText (value: float) = value.ToString("0.######", CultureInfo.InvariantCulture)

    /// Gets the requested structure id from one metric definition.
    let metricStructureId (metric: MetricDefinition) =
        match metric.Kind with
        | RelativeVolumeAtFullCourseDose(_, structureId) -> structureId
        | DoseAtVolumeNeedsFractionNormalization(_, structureId) -> structureId

    /// Gets the full-course dose threshold text from one metric definition.
    let fullCourseDoseThresholdGyText (metric: MetricDefinition) =
        match metric.Kind with
        | RelativeVolumeAtFullCourseDose(fullCourseDoseGy, _) -> floatText fullCourseDoseGy
        | DoseAtVolumeNeedsFractionNormalization _ -> ""

    /// Builds the final Mayo query string for one metric definition.
    let buildMayoQuery (prescription: PrescriptionContext) (metric: MetricDefinition) =
        match metric.Kind with
        | RelativeVolumeAtFullCourseDose(fullCourseDoseGy, _) ->
            Prescription.buildRelativeVolumeMayoQuery fullCourseDoseGy prescription.OriginalPrescriptionGy
        | DoseAtVolumeNeedsFractionNormalization(volumeCc, _) ->
            Prescription.buildDoseAtVolumeMayoQuery volumeCc

    /// Creates one metric error row.
    let errorRow patientId courseId planId metricId requestedStructureId actualStructureId originalPrescriptionGy intendedFractions fullCourseDoseThresholdGy mayoQuery unitName error =
        {
            PatientId = patientId
            CourseId = courseId
            PlanId = planId
            Backend = backendName
            RequestedStructureId = requestedStructureId
            ActualStructureId = actualStructureId
            MetricId = metricId
            OriginalPrescriptionGy = originalPrescriptionGy
            IntendedFractions = intendedFractions
            MayoQuery = mayoQuery
            FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
            RawValue = ""
            Value = ""
            NormalizationFactor = ""
            Unit = unitName
            Status = "Error"
            Error = error
        }

    /// Creates one metric missing-structure row.
    let missingStructureRow patientId courseId planId metricId requestedStructureId originalPrescriptionGy intendedFractions fullCourseDoseThresholdGy mayoQuery unitName =
        {
            PatientId = patientId
            CourseId = courseId
            PlanId = planId
            Backend = backendName
            RequestedStructureId = requestedStructureId
            ActualStructureId = ""
            MetricId = metricId
            OriginalPrescriptionGy = originalPrescriptionGy
            IntendedFractions = intendedFractions
            MayoQuery = mayoQuery
            FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
            RawValue = ""
            Value = ""
            NormalizationFactor = ""
            Unit = unitName
            Status = "MissingStructure"
            Error = sprintf "Structure id not found by exact or normalized lookup: %s" requestedStructureId
        }

    /// Creates one successful metric row.
    let okRow patientId courseId planId metricId requestedStructureId actualStructureId originalPrescriptionGy intendedFractions fullCourseDoseThresholdGy mayoQuery unitName status rawValue value normalizationFactor =
        {
            PatientId = patientId
            CourseId = courseId
            PlanId = planId
            Backend = backendName
            RequestedStructureId = requestedStructureId
            ActualStructureId = actualStructureId
            MetricId = metricId
            OriginalPrescriptionGy = originalPrescriptionGy
            IntendedFractions = intendedFractions
            MayoQuery = mayoQuery
            FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
            RawValue = floatText rawValue
            Value = floatText value
            NormalizationFactor = floatText normalizationFactor
            Unit = unitName
            Status = status
            Error = ""
        }

    /// Creates one metric row for a plan-level prescription failure.
    let prescriptionErrorRow patientId courseId planId (metric: MetricDefinition) error =
        let requestedStructureId = metricStructureId metric

        errorRow
            patientId
            courseId
            planId
            metric.Id
            requestedStructureId
            ""
            ""
            ""
            (fullCourseDoseThresholdGyText metric)
            ""
            metric.Unit
            error

    /// Gets the success status text from one structure match kind.
    let statusFromMatchKind (matchKind: string) =
        match matchKind with
        | "Exact" -> "OkExact"
        | "Normalized" -> "OkNormalized"
        | other -> "Ok" + other

    /// Extracts one configured metric from one plan using the ESAPIX Mayo backend.
    let extractMetric patientId courseId (plan: PlanSetup) (prescription: PrescriptionContext) (metric: MetricDefinition) =
        let requestedStructureId = metricStructureId metric
        let originalPrescriptionGy = floatText prescription.OriginalPrescriptionGy
        let intendedFractions = string prescription.IntendedFractions
        let fullCourseDoseThresholdGy = fullCourseDoseThresholdGyText metric
        let mayoQuery = buildMayoQuery prescription metric

        try
            match EsapiQuery.tryFindStructureForMetric plan requestedStructureId with
            | None ->
                missingStructureRow
                    patientId
                    courseId
                    plan.Id
                    metric.Id
                    requestedStructureId
                    originalPrescriptionGy
                    intendedFractions
                    fullCourseDoseThresholdGy
                    mayoQuery
                    metric.Unit
            | Some structureMatch ->
                let rawValue = plan.ExecuteQuery(mayoQuery, structureMatch.Structure)

                if Double.IsNaN(rawValue) || Double.IsInfinity(rawValue) then
                    errorRow
                        patientId
                        courseId
                        plan.Id
                        metric.Id
                        requestedStructureId
                        structureMatch.ActualStructureId
                        originalPrescriptionGy
                        intendedFractions
                        fullCourseDoseThresholdGy
                        mayoQuery
                        metric.Unit
                        (sprintf "ESAPIX Mayo query returned invalid value for query %s" mayoQuery)
                else
                    okRow
                        patientId
                        courseId
                        plan.Id
                        metric.Id
                        requestedStructureId
                        structureMatch.ActualStructureId
                        originalPrescriptionGy
                        intendedFractions
                        fullCourseDoseThresholdGy
                        mayoQuery
                        metric.Unit
                        (statusFromMatchKind structureMatch.MatchKind)
                        rawValue
                        rawValue
                        1.0
        with ex ->
            errorRow
                patientId
                courseId
                plan.Id
                metric.Id
                requestedStructureId
                ""
                originalPrescriptionGy
                intendedFractions
                fullCourseDoseThresholdGy
                mayoQuery
                metric.Unit
                (sprintf "ESAPIX Mayo query failed for query %s: %s" mayoQuery ex.Message)

    /// Extracts all configured metrics from one accepted plan.
    let extractPlanMetrics patientId (course: Course, plan: PlanSetup) =
        match Prescription.findOriginalPrescriptionFromPtv1 plan with
        | Error error ->
            Config.metrics |> List.map (fun metric -> prescriptionErrorRow patientId course.Id plan.Id metric error)
        | Ok prescription ->
            Config.metrics |> List.map (extractMetric patientId course.Id plan prescription)

    /// Extracts all configured metrics from one patient.
    let extractPatient (app: Application) patientId =
        result {
            let! patient = EsapiQuery.openPatientById app patientId

            try
                let rows = EsapiQuery.getMatchingPlans patient |> List.collect (extractPlanMetrics patientId)
                return rows
            finally
                EsapiQuery.closePatient app
        }
