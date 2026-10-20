open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open Mutant1.Files
open Mutant1.MuationDefinitions
open Mutant1.MutationCandidates
open Mutant1.Visitor
open Mutant1.Report

let checker = FSharpChecker.Create()

let testPrj = $"..\\..\\..\\Test1\\Test1.fsproj"
let sourcePrj = $"..\\..\\..\\Sample1\\Sample1.fsproj"
let file = $"..\\..\\..\\Sample1\\Sample3.fs"
let source = File.ReadAllText(file)

// Parse file
printfn "parse file"

let parsingOptions =
    { FSharpParsingOptions.Default with
        SourceFiles = [| file |] }

let parseResult =
    checker.ParseFile(
        file,
        FSharp.Compiler.Text.SourceText.ofString source,
        parsingOptions)
    |> Async.RunSynchronously

printfn "detect mutation candidates"

let result =
    findMutationCandidates file parseResult.ParseTree
    |> List.map enrich
    |> List.map ( fun candidate ->
            
            copyFileAsOriginal candidate.File |> ignore

            applyMutation candidate

            build sourcePrj |> ignore

            let tr = test testPrj

            let outcome =
                if tr.Failed > 0 then
                    {
                        Status = Killed
                        Interpretation = "Tests detected the behavioural change."
                    }
                else
                    {
                        Status = Survived
                        Interpretation = ""
                    }                    
                                                    
            restoreFromOriginal file |> ignore
            
            {
                MutationId = candidate.Id
                MutationType = candidate.Kind
                File = file
                Range = candidate.Range
                Original = candidate.Original
                Mutated = candidate.Replacement
                Outcome = outcome
            }) 

let killed = result |> List.filter _.Outcome.Status.IsKilled |> List.length
let survived = result |> List.filter _.Outcome.Status.IsSurvived |> List.length
printfnColor ConsoleColor.DarkGreen $"\nKILLED {killed} !"
printfnColor ConsoleColor.Red $"SURVIVED {survived} !"
           

0 |> ignore