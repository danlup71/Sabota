module Sabota.Config

open System
open System.IO
open System.Text.Json
open Sabota.MutationTypes



let defaultConfigPath () = Path.Combine(Directory.GetCurrentDirectory(), "sabota.json")

let oldConfigFileName = "sabota_config.json"
let newConfigFileName = "sabota.json"

/// Renames the old config file (sabota_config.json) to the new name (sabota.json)
/// if the old file exists and the new one does not. Returns true if migration happened.
let migrateOldConfig () : bool =
    let configDir = Directory.GetCurrentDirectory()
    let oldPath = Path.Combine(configDir, oldConfigFileName)
    let newPath = Path.Combine(configDir, newConfigFileName)
    if File.Exists oldPath && not (File.Exists newPath) then
        try
            File.Move(oldPath, newPath)
            printfn "Info: renamed old config file '%s' to '%s'" oldConfigFileName newConfigFileName
            true
        with
        | :? System.IO.IOException ->
            printfn "Warning: could not rename old config file '%s' to '%s'" oldConfigFileName newConfigFileName
            false
    else
        false

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

/// Creates a sabota.json config file in the current directory if it does not already exist.
/// The file has all categories enabled and placeholder paths for testProject and solutionRoot.
/// Returns true if the file was created, false if it already existed.
let initConfig () : bool =
    let path = defaultConfigPath ()
    if File.Exists path then
        printfn "Config file already exists at '%s'" path
        false
    else
        let categoriesJson =
            categoryMap
            |> Map.toList
            |> List.map (fun (name, _) -> sprintf "        \"%s\": true" name)
            |> String.concat ",\n"
        let json =
            "{\n" +
            "    \"testProject\": \"Path_To_UnitTest.fsproj\",\n" +
            "    \"solutionRoot\": \"Path_To_Solution_Folder\",\n" +
            "    \"file\": \"\",\n" +
            "    \"directory\": \"\",\n" +
            "    \"categories\":\n" +
            "    {\n" +
            categoriesJson + "\n" +
            "    }\n" +
            "}"
        File.WriteAllText(path, json)
        printfn "Created config file at '%s'" path
        true

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


