module Sabota.Performance

open System
open System.Diagnostics
open System.Collections.Generic

/// A single timed phase entry
type TimingEntry = {
    Phase : string
    Elapsed : TimeSpan
}

/// Collects timing entries for a single run of mutation analysis
type TimingCollector = {
    entries : ResizeArray<TimingEntry>
}

/// Create a new empty timing collector
let createCollector () : TimingCollector =
    { entries = ResizeArray() }

/// Record a completed phase with its elapsed time
let record (collector : TimingCollector) (phase : string) (elapsed : TimeSpan) : unit =
    collector.entries.Add({ Phase = phase; Elapsed = elapsed })

/// Stop a stopwatch and record the phase
let stopAndRecord (collector : TimingCollector) (phase : string) (sw : Stopwatch) : TimeSpan =
    sw.Stop()
    record collector phase sw.Elapsed
    sw.Elapsed

/// Start a new stopwatch for a phase
let startTimer () : Stopwatch =
    Stopwatch.StartNew()

/// Group entries by phase and compute count, total, and average
let private groupByPhase (entries : TimingEntry list) : (string * int * TimeSpan * TimeSpan) list =
    entries
    |> Seq.groupBy (fun e -> e.Phase)
    |> Seq.map (fun (phase, group) ->
        let count = Seq.length group
        let total = group |> Seq.fold (fun acc e -> acc + e.Elapsed) TimeSpan.Zero
        let avgTicks = total.Ticks / int64 count
        (phase, count, total, TimeSpan.FromTicks(avgTicks))
    )
    |> Seq.sortBy (fun (_, _, total, _) -> -total.TotalMilliseconds)
    |> Seq.toList

/// Format a TimeSpan as a human-readable string
let private formatTime (ts : TimeSpan) : string =
    if ts.TotalSeconds >= 1.0 then
        sprintf "%.2fs" ts.TotalSeconds
    else
        sprintf "%.0fms" ts.TotalMilliseconds

/// Print the timing summary to the console
let printSummary (collector : TimingCollector) (overallElapsed : TimeSpan) : unit =
    let entries = collector.entries |> Seq.toList

    let printfnColor (color: ConsoleColor) text : unit =
        Console.ForegroundColor <- color
        printfn text
        Console.ResetColor()

    if List.isEmpty entries then
        printfnColor ConsoleColor.Cyan $"\nPerformance: no timing data collected (overall {formatTime overallElapsed})"
    else
        let groups = groupByPhase entries
        let measuredTotal =
            groups
            |> List.fold (fun acc (_, _, total, _) -> acc + total) TimeSpan.Zero
        let overhead = overallElapsed - measuredTotal

        printfnColor ConsoleColor.Cyan $"\n===== Performance Summary ====="
        printfnColor ConsoleColor.Cyan $"Overall time: {formatTime overallElapsed}"
        printfn ""

        // Header
        printfn "  %-25s %8s %12s %12s" "Phase" "Count" "Total" "Avg"
        printfn "  %s" (String('-', 62))

        // Per-phase rows
        groups
        |> List.iter (fun (phase, count, total, avg) ->
            printfn "  %-25s %8d %12s %12s" phase count (formatTime total) (formatTime avg)
        )

        printfn "  %s" (String('-', 62))
        
        // Overhead
        if overhead > TimeSpan.Zero then
            printfn "  %-25s %12s" "Unmeasured overhead" (formatTime overhead)
        else
            printfn "  %-25s %12s" "Unmeasured overhead" "0ms"

        printfnColor ConsoleColor.Cyan "==============================="
