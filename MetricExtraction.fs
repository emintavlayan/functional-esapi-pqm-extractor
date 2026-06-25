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
    let errorRow patientId courseId planId metricId requestedStructureId actualStructureId originalPrescriptionGy intendedFractions currentFractions fullCourseDoseThresholdGy mayoQuery rawValue value normalizationFactor unitName error =
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
            CurrentFractions = currentFractions
            MayoQuery = mayoQuery
            FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
            RawValue = rawValue
            Value = value
            NormalizationFactor = normalizationFactor
            Unit = unitName
            Status = "Error"
            Error = error
        }

    /// Creates one metric missing-structure row.
    let missingStructureRow patientId courseId planId metricId requestedStructureId originalPrescriptionGy intendedFractions currentFractions fullCourseDoseThresholdGy mayoQuery unitName =
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
            CurrentFractions = currentFractions
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
    let okRow patientId courseId planId metricId requestedStructureId actualStructureId originalPrescriptionGy intendedFractions currentFractions fullCourseDoseThresholdGy mayoQuery unitName status rawValue value normalizationFactor =
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
            CurrentFractions = currentFractions
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
            ""
            (fullCourseDoseThresholdGyText metric)
            ""
            ""
            ""
            ""
            metric.Unit
            error

    /// Gets the success status text from one structure match kind.
    let statusFromMatchKind (matchKind: string) =
        match matchKind with
        | "Exact" -> "OkExact"
        | "Normalized" -> "OkNormalized"
        | other -> "Ok" + other

    /// Gets the current plan fraction count when it is available and non-zero.
    let currentFractions (plan: PlanSetup) =
        try
            let value = plan.NumberOfFractions

            if value.HasValue && value.Value > 0 then
                Ok value.Value
            else
                Error(sprintf "Current plan %s has missing or zero NumberOfFractions." plan.Id)
        with ex ->
            Error(sprintf "Could not read NumberOfFractions for plan %s: %s" plan.Id ex.Message)

    /// Extracts one configured metric from one plan using the ESAPIX Mayo backend.
    let extractMetric patientId courseId (plan: PlanSetup) (prescription: PrescriptionContext) (metric: MetricDefinition) =
        let requestedStructureId = metricStructureId metric
        let originalPrescriptionGy = floatText prescription.OriginalPrescriptionGy
        let intendedFractions = string prescription.IntendedFractions
        let currentFractionsResult = currentFractions plan
        let currentFractionsText =
            match currentFractionsResult with
            | Ok value -> string value
            | Error _ -> ""
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
                    currentFractionsText
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
                        currentFractionsText
                        fullCourseDoseThresholdGy
                        mayoQuery
                        ""
                        ""
                        ""
                        metric.Unit
                        (sprintf "ESAPIX Mayo query returned invalid value for query %s" mayoQuery)
                else
                    match metric.Kind with
                    | RelativeVolumeAtFullCourseDose _ ->
                        okRow
                            patientId
                            courseId
                            plan.Id
                            metric.Id
                            requestedStructureId
                            structureMatch.ActualStructureId
                            originalPrescriptionGy
                            intendedFractions
                            currentFractionsText
                            fullCourseDoseThresholdGy
                            mayoQuery
                            metric.Unit
                            (statusFromMatchKind structureMatch.MatchKind)
                            rawValue
                            rawValue
                            1.0
                    | DoseAtVolumeNeedsFractionNormalization _ ->
                        match currentFractionsResult with
                        | Ok currentPlanFractions ->
                            let normalizationFactor = float prescription.IntendedFractions / float currentPlanFractions
                            let normalizedValue = rawValue * normalizationFactor

                            okRow
                                patientId
                                courseId
                                plan.Id
                                metric.Id
                                requestedStructureId
                                structureMatch.ActualStructureId
                                originalPrescriptionGy
                                intendedFractions
                                currentFractionsText
                                fullCourseDoseThresholdGy
                                mayoQuery
                                metric.Unit
                                (statusFromMatchKind structureMatch.MatchKind)
                                rawValue
                                normalizedValue
                                normalizationFactor
                        | Error error ->
                            errorRow
                                patientId
                                courseId
                                plan.Id
                                metric.Id
                                requestedStructureId
                                structureMatch.ActualStructureId
                                originalPrescriptionGy
                                intendedFractions
                                currentFractionsText
                                fullCourseDoseThresholdGy
                                mayoQuery
                                (floatText rawValue)
                                ""
                                ""
                                metric.Unit
                                error
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
                currentFractionsText
                fullCourseDoseThresholdGy
                mayoQuery
                ""
                ""
                ""
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
