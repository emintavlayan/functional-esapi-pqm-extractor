namespace Functional.Esapi.PqmExtractor

open System
open System.Globalization
open System.IO
open System.Text
open VMS.TPS.Common.Model.API

module Program =

    /// Represents one accepted plan and structure debug row.
    type PlanDebugRow =
        {
            PatientId: string
            CourseId: string
            PlanId: string
            RequestedStructureId: string
            ActualStructureId: string
            MatchKind: string
            StructureVolumeCc: string
            HasDose: string
            MetricId: string
            OriginalPrescriptionGy: string
            IntendedFractions: string
            CurrentFractions: string
            FullCourseDoseThresholdGy: string
            MayoQuery: string
        }

    /// Represents one accepted plan dose-context debug row.
    type PlanDoseDebugRow =
        {
            PatientId: string
            CourseId: string
            PlanId: string
            HasDose: string
            DoseMax3DGy: string
            TotalDoseGy: string
            DosePerFractionGy: string
            NumberOfFractions: string
            TreatmentPercentage: string
            PlanNormalizationValue: string
            StructureSetId: string
            ApprovalStatus: string
            CreationDateTime: string
        }

    /// Gets the metric CSV path on the current user's desktop.
    let getDesktopCsvPath () =
        let desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        Path.Combine(desktop, "pqm_extract.csv")

    /// Gets the debug CSV path on the current user's desktop.
    let getDesktopDebugCsvPath () =
        let desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        Path.Combine(desktop, "pqm_plan_debug.csv")

    /// Gets the plan dose debug CSV path on the current user's desktop.
    let getDesktopPlanDoseDebugCsvPath () =
        let desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        Path.Combine(desktop, "pqm_plan_dose_debug.csv")

    /// Keeps the console window open so terminal output stays visible.
    let pauseBeforeExit () =
        printfn ""
        printf "Press Enter to exit..."
        Console.ReadLine() |> ignore

    /// Gets the structure id from one configured metric definition.
    let configMetricStructureId (metric: MetricDefinition) =
        match metric.Kind with
        | RelativeVolumeAtFullCourseDose(_, structureId) -> structureId
        | DoseAtVolumeNeedsFractionNormalization(_, structureId) -> structureId

    /// Gets the final Mayo query text for one plan-debug row when prescription inference succeeds.
    let debugMayoQuery (plan: PlanSetup) (metric: MetricDefinition) =
        match Prescription.findOriginalPrescriptionFromPtv1 plan with
        | Ok prescription -> MetricExtraction.buildMayoQuery prescription metric
        | Error _ -> ""

    /// Gets the inferred original prescription Gy text for one accepted plan.
    let debugOriginalPrescriptionGy (plan: PlanSetup) =
        match Prescription.findOriginalPrescriptionFromPtv1 plan with
        | Ok prescription -> MetricExtraction.floatText prescription.OriginalPrescriptionGy
        | Error _ -> ""

    /// Gets the inferred intended fraction count text for one accepted plan.
    let debugIntendedFractions (plan: PlanSetup) =
        match Prescription.findOriginalPrescriptionFromPtv1 plan with
        | Ok prescription -> string prescription.IntendedFractions
        | Error _ -> ""

    /// Gets the current fraction count text for one accepted plan when it is available.
    let debugCurrentFractions (plan: PlanSetup) =
        match MetricExtraction.currentFractions plan with
        | Ok value -> string value
        | Error _ -> ""

    /// Gets whether the plan has dose available.
    let hasDose (plan: PlanSetup) =
        if isNull plan.Dose then "false" else "true"

    /// Safely gets text from a function.
    let safeText getter =
        try
            getter ()
        with ex ->
            "ERROR: " + ex.Message

    /// Converts dose value to Gy text.
    let doseValueGyText (dose: VMS.TPS.Common.Model.Types.DoseValue) =
        try
            match dose.Unit with
            | VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.Gy ->
                dose.Dose.ToString("0.###", CultureInfo.InvariantCulture)
            | VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.cGy ->
                (dose.Dose / 100.0).ToString("0.###", CultureInfo.InvariantCulture)
            | _ ->
                dose.ToString()
        with ex ->
            "ERROR: " + ex.Message

    /// Converts dose value to cGy text.
    let doseValueCGyText (dose: VMS.TPS.Common.Model.Types.DoseValue) =
        try
            match dose.Unit with
            | VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.Gy ->
                (dose.Dose * 100.0).ToString("0.###", CultureInfo.InvariantCulture)
            | VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.cGy ->
                dose.Dose.ToString("0.###", CultureInfo.InvariantCulture)
            | _ ->
                dose.ToString()
        with ex ->
            "ERROR: " + ex.Message

    /// Gets the display volume text for one structure.
    let structureVolumeText (structure: Structure) =
        structure.Volume.ToString("0.###", CultureInfo.InvariantCulture)

    /// Creates one plan-debug row for one accepted plan metric.
    let createPlanDebugRow patientId (course: Course) (plan: PlanSetup) (metric: MetricDefinition) =
        let requestedStructureId = configMetricStructureId metric
        let originalPrescriptionGy = debugOriginalPrescriptionGy plan
        let intendedFractions = debugIntendedFractions plan
        let currentFractions = debugCurrentFractions plan
        let fullCourseDoseThresholdGy = MetricExtraction.fullCourseDoseThresholdGyText metric
        let mayoQuery = debugMayoQuery plan metric

        try
            match EsapiQuery.tryFindStructureForMetric plan requestedStructureId with
            | None ->
                {
                    PatientId = patientId
                    CourseId = course.Id
                    PlanId = plan.Id
                    RequestedStructureId = requestedStructureId
                    ActualStructureId = ""
                    MatchKind = "Missing"
                    StructureVolumeCc = "MissingStructure"
                    HasDose = hasDose plan
                    MetricId = metric.Id
                    OriginalPrescriptionGy = originalPrescriptionGy
                    IntendedFractions = intendedFractions
                    CurrentFractions = currentFractions
                    FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
                    MayoQuery = mayoQuery
                }
            | Some structureMatch ->
                {
                    PatientId = patientId
                    CourseId = course.Id
                    PlanId = plan.Id
                    RequestedStructureId = requestedStructureId
                    ActualStructureId = structureMatch.ActualStructureId
                    MatchKind = structureMatch.MatchKind
                    StructureVolumeCc = structureVolumeText structureMatch.Structure
                    HasDose = hasDose plan
                    MetricId = metric.Id
                    OriginalPrescriptionGy = originalPrescriptionGy
                    IntendedFractions = intendedFractions
                    CurrentFractions = currentFractions
                    FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
                    MayoQuery = mayoQuery
                }
        with ex ->
            {
                PatientId = patientId
                CourseId = course.Id
                PlanId = plan.Id
                RequestedStructureId = requestedStructureId
                ActualStructureId = ""
                MatchKind = "Error"
                StructureVolumeCc = "ERROR: " + ex.Message
                HasDose = hasDose plan
                MetricId = metric.Id
                OriginalPrescriptionGy = originalPrescriptionGy
                IntendedFractions = intendedFractions
                CurrentFractions = currentFractions
                FullCourseDoseThresholdGy = fullCourseDoseThresholdGy
                MayoQuery = mayoQuery
            }

    /// Creates debug rows for accepted plan structures.
    let createPlanDebugRows patientId (course: Course, plan: PlanSetup) =
        Config.metrics |> List.map (createPlanDebugRow patientId course plan)

    /// Creates one plan dose-context debug row for one accepted plan.
    let createPlanDoseDebugRow patientId (course: Course, plan: PlanSetup) =
        let doseMax3DGy =
            if isNull plan.Dose then
                ""
            else
                safeText (fun () -> doseValueGyText plan.Dose.DoseMax3D)

        {
            PatientId = patientId
            CourseId = course.Id
            PlanId = safeText (fun () -> plan.Id)
            HasDose = hasDose plan
            DoseMax3DGy = doseMax3DGy
            TotalDoseGy = safeText (fun () -> doseValueGyText plan.TotalDose)
            DosePerFractionGy = safeText (fun () -> doseValueGyText plan.DosePerFraction)
            NumberOfFractions = safeText (fun () -> string plan.NumberOfFractions)
            TreatmentPercentage = safeText (fun () -> string plan.TreatmentPercentage)
            PlanNormalizationValue = safeText (fun () -> string plan.PlanNormalizationValue)
            StructureSetId = safeText (fun () -> if isNull plan.StructureSet then "" else plan.StructureSet.Id)
            ApprovalStatus = safeText (fun () -> plan.ApprovalStatus.ToString())
            CreationDateTime = safeText (fun () -> string plan.CreationDateTime)
        }

    /// Converts one plan debug row to a CSV line.
    let debugRowToCsv (row: PlanDebugRow) =
        [
            row.PatientId
            row.CourseId
            row.PlanId
            row.RequestedStructureId
            row.ActualStructureId
            row.MatchKind
            row.StructureVolumeCc
            row.HasDose
            row.MetricId
            row.OriginalPrescriptionGy
            row.IntendedFractions
            row.CurrentFractions
            row.FullCourseDoseThresholdGy
            row.MayoQuery
        ]
        |> List.map Csv.escape
        |> String.concat ","

    /// Writes plan debug rows to the target CSV path.
    let writeDebugRows (path: string) (rows: PlanDebugRow list) =
        let header = "PatientId,CourseId,PlanId,RequestedStructureId,ActualStructureId,MatchKind,StructureVolumeCc,HasDose,MetricId,OriginalPrescriptionGy,IntendedFractions,CurrentFractions,FullCourseDoseThresholdGy,MayoQuery"
        let lines = rows |> List.map debugRowToCsv
        File.WriteAllLines(path, header :: lines, Encoding.UTF8)

    /// Converts one plan dose debug row to a CSV line.
    let planDoseDebugRowToCsv (row: PlanDoseDebugRow) =
        [
            row.PatientId
            row.CourseId
            row.PlanId
            row.HasDose
            row.DoseMax3DGy
            row.TotalDoseGy
            row.DosePerFractionGy
            row.NumberOfFractions
            row.TreatmentPercentage
            row.PlanNormalizationValue
            row.StructureSetId
            row.ApprovalStatus
            row.CreationDateTime
        ]
        |> List.map Csv.escape
        |> String.concat ","

    /// Writes plan dose debug rows to the target CSV path.
    let writePlanDoseDebugRows (path: string) (rows: PlanDoseDebugRow list) =
        let header = "PatientId,CourseId,PlanId,HasDose,DoseMax3DGy,TotalDoseGy,DosePerFractionGy,NumberOfFractions,TreatmentPercentage,PlanNormalizationValue,StructureSetId,ApprovalStatus,CreationDateTime"
        let lines = rows |> List.map planDoseDebugRowToCsv
        File.WriteAllLines(path, header :: lines, Encoding.UTF8)

    /// Creates error rows for a patient-level failure.
    let patientErrorRows patientId error =
        Config.metrics
        |> List.map (fun metric ->
            {
                PatientId = patientId
                CourseId = ""
                PlanId = ""
                Backend = EsapiQuery.backendName
                RequestedStructureId = configMetricStructureId metric
                ActualStructureId = ""
                MetricId = metric.Id
                OriginalPrescriptionGy = ""
                IntendedFractions = ""
                CurrentFractions = ""
                MayoQuery = ""
                FullCourseDoseThresholdGy = ""
                RawValue = ""
                Value = ""
                NormalizationFactor = ""
                Unit = metric.Unit
                Status = "PatientError"
                Error = error
            })

    /// Extracts accepted-plan debug rows and plan dose rows from one patient.
    let extractPatientDebugData (app: Application) patientId =
        match EsapiQuery.openPatientById app patientId with
        | Error error -> Error error
        | Ok patient ->
            try
                let plans = EsapiQuery.getMatchingPlans patient
                let debugRows = plans |> List.collect (createPlanDebugRows patientId)
                let planDoseDebugRows = plans |> List.map (createPlanDoseDebugRow patientId)
                Ok(debugRows, planDoseDebugRows)
            finally
                EsapiQuery.closePatient app

    /// Writes status and match-kind summaries to the console.
    let printSummary label keyName values =
        printfn "%s" label

        values
        |> Seq.countBy id
        |> Seq.sortBy fst
        |> Seq.iter (fun (key, count) -> printfn "  %s=%s Count=%d" keyName key count)

    /// Writes summary counts for one sequence of paired values.
    let printPairSummary label leftName rightName values =
        printfn "%s" label

        values
        |> Seq.countBy id
        |> Seq.sortBy fst
        |> Seq.iter (fun ((left, right), count) -> printfn "  %s=%s %s=%s Count=%d" leftName left rightName right count)

    /// Creates the ESAPI application session.
    let createApplication () =
        try
            Ok(Application.CreateApplication())
        with ex ->
            Error(sprintf "Application.CreateApplication failed: %s" (ex.ToString()))

    /// Runs the full extraction workflow.
    let run () =
        let outputPath = getDesktopCsvPath ()
        let debugPath = getDesktopDebugCsvPath ()
        let planDoseDebugPath = getDesktopPlanDoseDebugCsvPath ()
        printfn "Creating ESAPI application session..."

        match createApplication () with
        | Error error -> failwith error
        | Ok app ->
            use app = app

            let patientResults =
                Config.patientIds
                |> List.map (fun patientId ->
                    printfn "Extracting %s" patientId

                    let metricRows =
                        match MetricExtraction.extractPatient app patientId with
                        | Ok rows -> rows
                        | Error error ->
                            EsapiQuery.closePatient app
                            patientErrorRows patientId error

                    let planDebugRows, planDoseDebugRows =
                        match extractPatientDebugData app patientId with
                        | Ok(rows, planDoseRows) -> rows, planDoseRows
                        | Error error ->
                            printfn "Debug extraction skipped for %s: %s" patientId error
                            EsapiQuery.closePatient app
                            [], []

                    metricRows, planDebugRows, planDoseDebugRows)

            let rows = patientResults |> List.collect (fun (metricRows, _, _) -> metricRows)
            let debugRows = patientResults |> List.collect (fun (_, planDebugRows, _) -> planDebugRows)
            let planDoseDebugRows = patientResults |> List.collect (fun (_, _, doseRows) -> doseRows)
            let uniquePatientCount =
                rows
                |> Seq.map (fun row -> row.PatientId)
                |> Seq.filter (String.IsNullOrWhiteSpace >> not)
                |> Seq.distinct
                |> Seq.length
            let uniqueSessionCount =
                rows
                |> Seq.map (fun row -> row.PatientId, row.CourseId)
                |> Seq.filter (fun (_, courseId) -> not (String.IsNullOrWhiteSpace courseId))
                |> Seq.distinct
                |> Seq.length
            let uniquePlanCount =
                rows
                |> Seq.map (fun row -> row.PatientId, row.CourseId, row.PlanId)
                |> Seq.filter (fun (_, _, planId) -> not (String.IsNullOrWhiteSpace planId))
                |> Seq.distinct
                |> Seq.length

            Csv.writeRows outputPath rows
            writeDebugRows debugPath debugRows
            writePlanDoseDebugRows planDoseDebugPath planDoseDebugRows
            printfn "Wrote %d metric rows to %s" rows.Length outputPath
            printfn "Unique patients=%d" uniquePatientCount
            printfn "Unique sessions=%d" uniqueSessionCount
            printfn "Unique plans=%d" uniquePlanCount
            printSummary "Rows by status:" "Status" (rows |> Seq.map (fun row -> row.Status))
            printPairSummary
                "Rows by requested-to-actual structure id:"
                "RequestedStructureId"
                "ActualStructureId"
                (rows |> Seq.map (fun row -> row.RequestedStructureId, row.ActualStructureId))
            printfn "Wrote %d debug rows to %s" debugRows.Length debugPath
            printSummary "Debug match summary:" "MatchKind" (debugRows |> Seq.map (fun row -> row.MatchKind))
            printfn "Wrote %d plan dose debug rows to %s" planDoseDebugRows.Length planDoseDebugPath
            0

    /// Runs the console entry point.
    [<EntryPoint; STAThread>]
    let main _ =
        let exitCode =
            try
                printfn "Starting PQM extraction..."
                run ()
            with ex ->
                eprintfn "Fatal error: %s" (ex.ToString())
                1

        try
            Console.Out.Flush()
            Console.Error.Flush()
            exitCode
        finally
            pauseBeforeExit ()
