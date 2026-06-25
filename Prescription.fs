namespace Functional.Esapi.PqmExtractor

open System
open System.Globalization
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open VMS.TPS.Common.Model.API

module Prescription =

    /// Parses the original prescription dose in Gy from one matched PTV1 structure id.
    let parseDoseGyFromStructureId (structureId: string) =
        if String.IsNullOrWhiteSpace structureId then
            Error "Matched PTV1 structure id was blank."
        else
            let matched = Regex.Match(structureId, "PTV1_(\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase)

            if not matched.Success then
                Error(sprintf "Could not parse original prescription from PTV1 structure id '%s'." structureId)
            else
                match Double.TryParse(matched.Groups.[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture) with
                | true, value -> Ok value
                | false, _ -> Error(sprintf "Could not parse prescription dose text '%s' from PTV1 structure id '%s'." matched.Groups.[1].Value structureId)

    /// Gets the intended total fraction count for one original prescription dose.
    let intendedFractionsFromPrescriptionGy (prescriptionGy: float) =
        if abs (prescriptionGy - 66.0) < 0.001 then
            Ok 33
        elif abs (prescriptionGy - 68.0) < 0.001 then
            Ok 34
        else
            Error(sprintf "Unsupported original prescription %.6g Gy inferred from PTV1 structure id." prescriptionGy)

    /// Finds the original full-course prescription from the matched PTV1 structure.
    let findOriginalPrescriptionFromPtv1 (plan: PlanSetup) =
        result {
            match EsapiQuery.tryFindStructureForMetric plan "PTV1" with
            | None ->
                return! Error "Could not infer original prescription because PTV1 was not found by exact or normalized lookup."
            | Some structureMatch ->
                let! originalPrescriptionGy = parseDoseGyFromStructureId structureMatch.ActualStructureId
                let! intendedFractions = intendedFractionsFromPrescriptionGy originalPrescriptionGy

                return
                    {
                        OriginalPrescriptionGy = originalPrescriptionGy
                        IntendedFractions = intendedFractions
                        ActualPtv1StructureId = structureMatch.ActualStructureId
                    }
        }

    /// Builds the Mayo query for a relative volume threshold tied to the original full-course prescription.
    let buildRelativeVolumeMayoQuery (fullCourseDoseGy: float) (originalPrescriptionGy: float) =
        let relativeDosePercent = fullCourseDoseGy / originalPrescriptionGy * 100.0
        sprintf "V%.6g%%[%%]" relativeDosePercent

    /// Builds the Mayo query for a dose-at-volume metric.
    let buildDoseAtVolumeMayoQuery (volumeCc: float) = sprintf "D%.6gcc[Gy]" volumeCc
