namespace Functional.Esapi.PqmExtractor

open System
open System.Globalization
open System.Text.RegularExpressions
open ESAPIX.Extensions
open FsToolkit.ErrorHandling
open VMS.TPS.Common.Model.API

module EsapiQuery =

    /// Returns the metric backend label used in CSV outputs.
    let backendName = "ESAPIX-Mayo"

    /// Represents how a requested structure id was matched.
    type StructureMatch =
        {
            RequestedStructureId: string
            ActualStructureId: string
            Structure: Structure
            MatchKind: string
        }

    /// Opens one patient by direct id or by matching visible patient summary fields.
    let openPatientById (app: Application) (patientId: string) : Result<Patient, string> =
        try
            let directPatient = app.OpenPatientById patientId

            if not (isNull directPatient) then
                Ok directPatient
            else
                let normalize (value: string) =
                    if isNull value then "" else value.Trim()

                let equalsIgnoreCase expected actual =
                    String.Equals(normalize expected, normalize actual, StringComparison.OrdinalIgnoreCase)

                let containsIgnoreCase expected actual =
                    let expectedValue = normalize expected
                    let actualValue = normalize actual

                    if String.IsNullOrWhiteSpace expectedValue || String.IsNullOrWhiteSpace actualValue then
                        false
                    else
                        actualValue.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0

                let matchesSummary (summary: PatientSummary) =
                    equalsIgnoreCase patientId summary.Id
                    || equalsIgnoreCase patientId summary.Id2
                    || equalsIgnoreCase patientId summary.LastName
                    || equalsIgnoreCase patientId summary.FirstName
                    || containsIgnoreCase patientId summary.Id
                    || containsIgnoreCase patientId summary.Id2
                    || containsIgnoreCase patientId summary.LastName
                    || containsIgnoreCase patientId summary.FirstName

                let matches =
                    app.PatientSummaries
                    |> Seq.filter matchesSummary
                    |> Seq.truncate 10
                    |> Seq.toList

                match matches with
                | [] ->
                    Error(sprintf "Patient not found by direct id or PatientSummaries search: %s" patientId)

                | [ summary ] ->
                    let patient = app.OpenPatient(summary)

                    if isNull patient then
                        Error(sprintf "PatientSummary matched input '%s' with Id='%s', Id2='%s', Name='%s, %s', but app.OpenPatient(summary) returned null." patientId summary.Id summary.Id2 summary.LastName summary.FirstName)
                    else
                        Ok patient

                | many ->
                    let descriptions =
                        many
                        |> List.map (fun summary ->
                            sprintf "Id='%s'; Id2='%s'; Name='%s, %s'" summary.Id summary.Id2 summary.LastName summary.FirstName)
                        |> String.concat " | "

                    Error(sprintf "Ambiguous patient input '%s'. Matches: %s" patientId descriptions)
        with ex ->
            Error(sprintf "Could not open patient %s: %s" patientId ex.Message)

    /// Closes the currently opened patient if possible.
    let closePatient (app: Application) =
        try
            app.ClosePatient()
        with _ ->
            ()

    /// Checks whether course id matches the hardcoded filter.
    let courseMatches (course: Course) = Text.containsIgnoreCase "session" course.Id

    /// Checks whether plan id matches the hardcoded filter.
    let planMatches (plan: PlanSetup) =
        Text.notContainsIgnoreCase "R" plan.Id
        && not (isNull plan.Id)
        && plan.Id.Contains("/")

    /// Gets all matching plans for a patient.
    let getMatchingPlans (patient: Patient) =
        patient.Courses
        |> Seq.filter courseMatches
        |> Seq.collect (fun course ->
            course.PlanSetups
            |> Seq.filter planMatches
            |> Seq.map (fun plan -> course, plan))
        |> Seq.toList

    /// Removes a trailing underscore-plus-two-digits dose suffix from one structure id segment.
    let removeTrailingDoseSuffix (value: string) =
        if String.IsNullOrWhiteSpace value then value
        else
            Regex.Replace(value.Trim(), "_\\d{2}$", "")

    /// Normalizes structure ids for lookup by removing dose suffixes from each hyphen-delimited segment.
    let normalizeStructureIdForLookup (value: string) =
        if isNull value then ""
        else
            value.Split([| '-' |], StringSplitOptions.None)
            |> Array.map removeTrailingDoseSuffix
            |> String.concat "-"

    /// Finds a structure by exact id only.
    let tryFindStructureExactOnly (plan: PlanSetup) (structureId: string) =
        if isNull plan.StructureSet then None
        else
            plan.StructureSet.Structures
            |> Seq.tryFind (fun structure ->
                String.Equals(structure.Id, structureId, StringComparison.Ordinal))

    /// Finds a structure by exact id first and normalized id second.
    let tryFindStructureForMetric (plan: PlanSetup) (requestedStructureId: string) =
        match tryFindStructureExactOnly plan requestedStructureId with
        | Some structure ->
            Some
                {
                    RequestedStructureId = requestedStructureId
                    ActualStructureId = structure.Id
                    Structure = structure
                    MatchKind = "Exact"
                }
        | None ->
            if isNull plan.StructureSet then None
            else
                let normalizedTargetId = normalizeStructureIdForLookup requestedStructureId

                plan.StructureSet.Structures
                |> Seq.filter (fun structure ->
                    String.Equals(normalizeStructureIdForLookup structure.Id, normalizedTargetId, StringComparison.Ordinal))
                |> Seq.toList
                |> function
                    | [ structure ] ->
                        Some
                            {
                                RequestedStructureId = requestedStructureId
                                ActualStructureId = structure.Id
                                Structure = structure
                                MatchKind = "Normalized"
                            }
                    | [] -> None
                    | many ->
                        let ids = many |> List.map (fun s -> s.Id) |> String.concat " | "
                        failwithf "Ambiguous normalized structure match for requested '%s'. Candidates: %s" requestedStructureId ids

    /// Gets the structure id from one metric definition.
    let metricStructureId (metric: MetricDefinition) =
        match metric.Kind with
        | MayoQuery(_, structureId) -> structureId

    /// Creates a missing-structure metric row.
    let missingStructureRow patientId courseId planId metricId requestedStructureId mayoQuery unitName =
        {
            PatientId = patientId
            CourseId = courseId
            PlanId = planId
            Backend = backendName
            RequestedStructureId = requestedStructureId
            ActualStructureId = ""
            MetricId = metricId
            MayoQuery = mayoQuery
            Value = ""
            Unit = unitName
            Status = "MissingStructure"
            Error = sprintf "Structure id not found by exact or normalized lookup: %s" requestedStructureId
        }

    /// Creates an error metric row.
    let errorRow patientId courseId planId metricId requestedStructureId actualStructureId mayoQuery unitName error =
        {
            PatientId = patientId
            CourseId = courseId
            PlanId = planId
            Backend = backendName
            RequestedStructureId = requestedStructureId
            ActualStructureId = actualStructureId
            MetricId = metricId
            MayoQuery = mayoQuery
            Value = ""
            Unit = unitName
            Status = "Error"
            Error = error
        }

    /// Creates a successful metric row.
    let okRow patientId courseId planId metricId requestedStructureId actualStructureId mayoQuery unitName status (value: float) =
        {
            PatientId = patientId
            CourseId = courseId
            PlanId = planId
            Backend = backendName
            RequestedStructureId = requestedStructureId
            ActualStructureId = actualStructureId
            MetricId = metricId
            MayoQuery = mayoQuery
            Value = value.ToString("0.###", CultureInfo.InvariantCulture)
            Unit = unitName
            Status = status
            Error = ""
        }

    /// Extracts one metric from one plan using ESAPIX Mayo query.
    let extractMetric patientId courseId (plan: PlanSetup) (metric: MetricDefinition) =
        let requestedStructureId = metricStructureId metric

        match metric.Kind with
        | MayoQuery(query, _) ->
            try
                match tryFindStructureForMetric plan requestedStructureId with
                | None ->
                    missingStructureRow patientId courseId plan.Id metric.Id requestedStructureId query metric.Unit
                | Some structureMatch ->
                    let value = plan.ExecuteQuery(query, structureMatch.Structure)

                    if Double.IsNaN(value) || Double.IsInfinity(value) then
                        errorRow
                            patientId
                            courseId
                            plan.Id
                            metric.Id
                            requestedStructureId
                            structureMatch.ActualStructureId
                            query
                            metric.Unit
                            (sprintf "ESAPIX Mayo query returned invalid value for query %s" query)
                    else
                        let status =
                            match structureMatch.MatchKind with
                            | "Exact" -> "OkExact"
                            | "Normalized" -> "OkNormalized"
                            | other -> "Ok" + other

                        okRow
                            patientId
                            courseId
                            plan.Id
                            metric.Id
                            requestedStructureId
                            structureMatch.ActualStructureId
                            query
                            metric.Unit
                            status
                            value
            with ex ->
                errorRow
                    patientId
                    courseId
                    plan.Id
                    metric.Id
                    requestedStructureId
                    ""
                    query
                    metric.Unit
                    (sprintf "ESAPIX Mayo query failed for query %s: %s" query ex.Message)

    /// Extracts all configured metrics from one plan.
    let extractPlanMetrics patientId (course: Course, plan: PlanSetup) =
        Config.metrics |> List.map (extractMetric patientId course.Id plan)

    /// Extracts all configured metrics from one patient.
    let extractPatient (app: Application) patientId =
        result {
            let! patient = openPatientById app patientId

            try
                let rows = getMatchingPlans patient |> List.collect (extractPlanMetrics patientId)
                return rows
            finally
                closePatient app
        }
