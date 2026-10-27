module Sabota.Config

open System
open System.IO
open System.Text.Json
open Sabota.MutationTypes

type ConfigDefaults = {
    TestProject : string
    SolutionRoot : string
    File : string option
    Directory : string option
    Categories : MutationCategory list
}

let defaultConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "sabota_config.json")

let categoryMap : Map<string, MutationCategory> =
    Map.ofList [
        "Equality", MutationCategory.Equality
        "RelationalBoundary", MutationCategory.RelationalBoundary
        "RelationalReversal", MutationCategory.RelationalReversal
        "Boolean", MutationCategory.Boolean
        "Arithmetic", MutationCategory.Arithmetic
        "Conditional", MutationCategory.Conditional
        "Option", MutationCategory.Option
        "ReturnListValue", MutationCategory.ReturnListValue
        "ControlFlow", MutationCategory.ControlFlow
    ]

let allCategories () : MutationCategory list =
    categoryMap.Values |> Seq.toList

let parseCategoriesElement (categoriesElement : JsonElement) : MutationCategory list =
    if categoriesElement.ValueKind = JsonValueKind.Object then
        categoriesElement.EnumerateObject()
        |> Seq.choose (fun (prop : JsonProperty) ->
            match categoryMap.TryGetValue prop.Name with
            | true, category ->
                let enabled = prop.Value.GetBoolean()
                if enabled then Some category else None
            | _ -> None
        )
        |> Seq.toList
    else
        printfn "Warning: 'categories' is not an object, using all categories"
        allCategories ()

let parseCategoriesString (categoriesStr : string) : MutationCategory list =
    categoriesStr.Split(',')
    |> Array.map (fun s -> s.Trim())
    |> Array.choose (fun name ->
        match categoryMap.TryGetValue name with
        | true, category -> Some category
        | _ ->
            printfn $"Warning: unknown category '{name}'"
            None
    )
    |> Array.toList


let resolvePathRelativeToConfig (configPath : string) (path : string) : string =
    let configDir =
        match Path.GetDirectoryName configPath with
        | null -> Environment.CurrentDirectory
        | dir -> dir
    path.Split(',')
    |> Array.map _.Trim()
    |> Array.map (fun p ->
        // If the path contains a rooted path (e.g., "-d D:\path"), extract it
        let separators : char array = [|' '; '\t'|]
        let parts = p.Split(separators, StringSplitOptions.RemoveEmptyEntries)
        let rootedPart =
            parts
            |> Array.tryFind (fun (part : string) -> Path.IsPathRooted part)
        match rootedPart with
        | Some rooted -> Path.GetFullPath rooted
        | None ->
            if Path.IsPathRooted p then
                Path.GetFullPath p
            else
                Path.GetFullPath (Path.Combine(configDir, p))
    )
    |> String.concat ","

let loadConfig (configPath : string) : ConfigDefaults =
    if not (File.Exists configPath) then
        printfn $"Warning: config file not found at {configPath}, using defaults"
        {
            TestProject = ""
            SolutionRoot = ""
            File = None
            Directory = None
            Categories = allCategories ()
        }
    else
        let json = File.ReadAllText configPath
        use doc = JsonDocument.Parse(json)
        let root = doc.RootElement

        let getStringOpt (key : string) : string option =
            let mutable elem : JsonElement = Unchecked.defaultof<JsonElement>
            if root.TryGetProperty(key, &elem) then
                match elem.GetString() with
                | null -> None
                | value ->
                    if String.IsNullOrEmpty value then None else Some (resolvePathRelativeToConfig configPath value)
            else
                None

        let testProject = getStringOpt "testProject"
        let solutionRoot = getStringOpt "solutionRoot"
        let file = getStringOpt "file"
        let directory = getStringOpt "directory"

        let categories =
            let mutable elem : JsonElement = Unchecked.defaultof<JsonElement>
            if root.TryGetProperty("categories", &elem) then
                parseCategoriesElement elem
            else
                printfn "Warning: 'categories' key not found in config, using all categories"
                allCategories ()

        {
            TestProject = Option.defaultValue "" testProject
            SolutionRoot = Option.defaultValue "" solutionRoot
            File = file
            Directory = directory
            Categories = categories
        }


