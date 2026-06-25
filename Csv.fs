namespace Functional.Esapi.PqmExtractor

open System.IO
open System.Text

module Csv =

    /// Escapes one CSV field.
    let escape (value: string) =
        let safe = if isNull value then "" else value

        if safe.Contains(",") || safe.Contains("\"") || safe.Contains("\r") || safe.Contains("\n") then
            "\"" + safe.Replace("\"", "\"\"") + "\""
        else
            safe

    /// Converts one metric row to a CSV line.
    let rowToCsv (row: MetricRow) =
        [
            row.PatientId
            row.CourseId
            row.PlanId
            row.Backend
            row.RequestedStructureId
            row.ActualStructureId
            row.MetricId
            row.OriginalPrescriptionGy
            row.IntendedFractions
            row.MayoQuery
            row.FullCourseDoseThresholdGy
            row.RawValue
            row.Value
            row.NormalizationFactor
            row.Unit
            row.Status
            row.Error
        ]
        |> List.map escape
        |> String.concat ","

    /// Writes metric rows to the target CSV path.
    let writeRows (path: string) (rows: MetricRow list) =
        let header = "PatientId,CourseId,PlanId,Backend,RequestedStructureId,ActualStructureId,MetricId,OriginalPrescriptionGy,IntendedFractions,MayoQuery,FullCourseDoseThresholdGy,RawValue,Value,NormalizationFactor,Unit,Status,Error"
        let lines = rows |> List.map rowToCsv
        File.WriteAllLines(path, header :: lines, Encoding.UTF8)
