module TestApp

open System
open System.IO
open Xunit
open Sabota.Args

/// A record holding the paths of a simulated project environment.
type SimulatedEnvironment = {
    BaseDir : string
    TestProject : string
    SolutionRoot : string
    FileFs : string          /// Convenience: head of Files (for backward compatibility)
    Files : string list      /// List of created .fs (or other) file paths
    Directories : string list /// List of created directory paths
}

type TestApp =
    /// Parse a human-readable command-line string into a string array.
    /// Handles quoted arguments (double quotes) and unquoted tokens separated by whitespace.
    /// Example: "-t test.fsproj -s solution_root -f file.fs" |> TestApp.ParseCommandLine -> [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-f"; "file.fs"|]
    static member ParseCommandLine (line : string) : string[] =
        let tokens = ResizeArray<string>()
        let len = line.Length

        let skipWhitespace pos =
            let mutable p = pos
            while p < len && System.Char.IsWhiteSpace line.[p] do
                p <- p + 1
            p

        let rec parseQuoted (pos : int) (sb : System.Text.StringBuilder) : string * int =
            if pos >= len then
                (sb.ToString(), pos)
            else
                match line.[pos] with
                | '"' ->
                    if pos + 1 < len && line.[pos + 1] = '"' then
                        sb.Append('"') |> ignore
                        parseQuoted (pos + 2) sb
                    else
                        (sb.ToString(), pos + 1)  // skip closing quote
                | _ ->
                    sb.Append(line.[pos]) |> ignore
                    parseQuoted (pos + 1) sb

        let rec parseUnquoted (pos : int) (sb : System.Text.StringBuilder) : string * int =
            if pos >= len || System.Char.IsWhiteSpace line.[pos] then
                (sb.ToString(), pos)
            else
                sb.Append(line.[pos]) |> ignore
                parseUnquoted (pos + 1) sb

        let rec loop pos =
            if pos >= len then ()
            else
                let pos = skipWhitespace pos
                if pos >= len then ()
                else
                    let (token, nextPos) =
                        if line.[pos] = '"' then
                            parseQuoted (pos + 1) (System.Text.StringBuilder())
                        else
                            parseUnquoted pos (System.Text.StringBuilder())
                    tokens.Add(token)
                    loop nextPos

        loop 0
        tokens.ToArray()

    /// Unwrap a ParseResult Ok value, asserting on the Target field's size.
    /// Returns a function ParseResult -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsTarget_Size(expected : int) : ParseResult -> ParsedArgs =
        fun result ->
            match result with
            | Ok parsed ->
                match parsed.Target with
                | Some (File files) ->
                    Assert.Equal(expected, files.Length)
                    parsed
                | Some (Directory dirs) ->
                    Assert.Equal(expected, dirs.Length)
                    parsed
                | None ->
                    Assert.Fail("Expected Some target, got None")
                    Unchecked.defaultof<ParsedArgs>
            | Error msg ->
                Assert.Fail($"Expected Ok, got Error: {msg}")
                Unchecked.defaultof<ParsedArgs>

    /// Assert that the Target file list contains the expected items.
    /// Returns a function ParsedArgs -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsTarget_Contains(expected : string list) : ParsedArgs -> ParsedArgs =
        fun parsed ->
            match parsed.Target with
            | Some (File files) ->
                for e in expected do
                    Assert.True(List.contains e files, $"Expected '{e}' in files list")
                parsed
            | Some (Directory _) ->
                Assert.Fail("Expected File target")
                Unchecked.defaultof<ParsedArgs>
            | None ->
                Assert.Fail("Expected Some target, got None")
                Unchecked.defaultof<ParsedArgs>

    /// Assert that the Target directory list contains the expected items.
    /// Returns a function ParsedArgs -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsDirectory_Contains(expected : string list) : ParsedArgs -> ParsedArgs =
        fun parsed ->
            match parsed.Target with
            | Some (Directory dirs) ->
                for e in expected do
                    Assert.True(List.contains e dirs, $"Expected '{e}' in directories list")
                parsed
            | Some (File _) ->
                Assert.Fail("Expected Directory target")
                Unchecked.defaultof<ParsedArgs>
            | None ->
                Assert.Fail("Expected Some target, got None")
                Unchecked.defaultof<ParsedArgs>

    /// Assert that the parsed result is an Error containing the expected message substring.
    /// Returns a function ParseResult -> unit for pipeline chaining.
    static member Assert_ArgsError_Contains(expected : string) : ParseResult -> unit =
        fun result ->
            match result with
            | Error msg ->
                Assert.Contains(expected, msg)
            | Ok _ ->
                Assert.Fail("Expected Error, got Ok")

    /// Assert the TestProject field matches the expected value.
    /// Returns a function ParsedArgs -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsTestProject(expected : string option) : ParsedArgs -> ParsedArgs =
        fun parsed ->
            Assert.Equal(expected, parsed.TestProject)
            parsed

    /// Assert the SolutionRoot field matches the expected value.
    /// Returns a function ParsedArgs -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsSolutionRoot(expected : string option) : ParsedArgs -> ParsedArgs =
        fun parsed ->
            Assert.Equal(expected, parsed.SolutionRoot)
            parsed

    /// Unwrap a ParseResult Ok value and assert that the Target field is None.
    /// Returns a function ParseResult -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsTarget_IsMissing() : ParseResult -> ParsedArgs =
        fun result ->
            match result with
            | Ok parsed ->
                Assert.Equal(None, parsed.Target)
                parsed
            | Error msg ->
                Assert.Fail($"Expected Ok, got Error: {msg}")
                Unchecked.defaultof<ParsedArgs>

    /// Assert that the TestProject field is None.
    /// Returns a function ParsedArgs -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsTestProject_IsMissing() : ParsedArgs -> ParsedArgs =
        fun parsed ->
            Assert.Equal(None, parsed.TestProject)
            parsed

    /// Assert that the SolutionRoot field is None.
    /// Returns a function ParsedArgs -> ParsedArgs for pipeline chaining.
    static member Assert_ArgsSolutionRoot_IsMissing() : ParsedArgs -> ParsedArgs =
        fun parsed ->
            Assert.Equal(None, parsed.SolutionRoot)
            parsed

    /// Assert that the simulated environment satisfies the minimal requirements for the application to run.
    /// Returns a function ParseResult -> unit for pipeline chaining.
    static member Assert_MinimalRequirementsMet() : ParseResult -> unit =
        fun result ->
            match result with
            | Ok _ -> ()
            | Error msg ->
                Assert.Fail($"Expected Ok, got Error: {msg}")

    /// Assert that a validate result is an Error containing the expected message substring.
    /// Returns a function ParseResult -> unit for pipeline chaining.
    static member Assert_ValidateError_Contains(expected : string) : ParseResult -> unit =
        fun result ->
            match result with
            | Error msg ->
                Assert.Contains(expected, msg)
            | Ok _ ->
                Assert.Fail("Expected Error, got Ok")

    /// Create a simulated project environment with a test project file, a solution subdirectory, source files, and directories.
    /// Accepts the Stub data explicitly as parameters plus the base directory path (file content is hardcoded as it is not validated).
    /// Returns a record with the paths.
    /// Example:
    ///   let baseDir = TestApp.CreateSimulatedBaseDir()
    ///   let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.FileFsName], [], baseDir)
    ///   try
    ///       validate env.TestProject env.SolutionRoot (File [env.FileFs])
    ///   finally
    ///       TestApp.CleanSimulatedEnvironment baseDir
    static member CreateSimulatedEnvironment(testProjectName : string, solutionRootName : string, fileNames : string list, directoryNames : string list, baseDir : string) : SimulatedEnvironment =
        let testPrj = Path.Combine(baseDir, testProjectName)
        let solutionRoot =
            if solutionRootName = "" then
                ""
            else
                let path = Path.Combine(baseDir, solutionRootName)
                Directory.CreateDirectory(path) |> ignore
                path

        let files =
            fileNames
            |> List.map (fun name ->
                let path = Path.Combine(baseDir, name)
                File.WriteAllText(path, "let x = 1")
                path
            )

        let directories =
            directoryNames
            |> List.map (fun name ->
                let path = Path.Combine(baseDir, name)
                Directory.CreateDirectory(path) |> ignore
                path
            )

        File.WriteAllText(testPrj, "<Project/>")

        {
            BaseDir = baseDir
            TestProject = testPrj
            SolutionRoot = solutionRoot
            FileFs = defaultArg (List.tryHead files) ""
            Files = files
            Directories = directories
        }

    /// Create a temporary base directory for simulated environments.
    /// Returns the path to the newly created empty directory.
    /// Example:
    ///   let baseDir = TestApp.CreateSimulatedBaseDir()
    static member CreateSimulatedBaseDir() : string =
        let baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(baseDir) |> ignore
        baseDir

    /// Clean up a simulated project environment by deleting its base directory.
    /// Example:
    ///   let baseDir = TestApp.CreateSimulatedBaseDir()
    ///   let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, Stub.FileFsName, baseDir)
    ///   try
    ///       validate env.TestProject env.SolutionRoot (File [env.FileFs])
    ///   finally
    ///       TestApp.CleanSimulatedEnvironment baseDir
    static member CleanSimulatedEnvironment(baseDir : string) : unit =
        Directory.Delete(baseDir, true)

    /// Temporarily change the current working directory and return a function to restore it.
    /// Returns a unit -> unit function that restores the original directory.
    /// Example:
    ///   let restore = TestApp.SetCurrentDirectory "/some/path"
    ///   try
    ///       // work with relative paths
    ///   finally
    ///       restore()
    static member SetCurrentDirectory(newDir : string) : unit -> unit =
        let originalDir = Environment.CurrentDirectory
        Environment.CurrentDirectory <- newDir
        (fun () -> Environment.CurrentDirectory <- originalDir)

    /// Create a nested directory tree with .fs files at specified relative paths.
    /// Each tuple is (relativeSubDir, fileName) where relativeSubDir can be "" for the root.
    /// Returns the list of absolute file paths created.
    /// Example:
    ///   let files = TestApp.CreateNestedFsFiles(baseDir, [("", "top.fs"); ("sub1", "mid.fs"); ("sub1/sub2", "deep.fs")])
    static member CreateNestedFsFiles(baseDir : string, entries : (string * string) list) : string list =
        entries
        |> List.map (fun (subDir, fileName) ->
            let path = Path.Combine(baseDir, subDir, fileName)
            let parent = defaultArg (Option.ofObj (Path.GetDirectoryName(path))) baseDir
            Directory.CreateDirectory(parent) |> ignore
            File.WriteAllText(path, "let x = 1")
            path
        )

    /// Create a nested directory tree with non-.fs files at specified relative paths.
    /// Each tuple is (relativeSubDir, fileName).
    /// Returns the list of absolute file paths created.
    /// Example:
    ///   let others = TestApp.CreateNestedOtherFiles(baseDir, [("sub1", "readme.txt")])
    static member CreateNestedOtherFiles(baseDir : string, entries : (string * string) list) : string list =
        entries
        |> List.map (fun (subDir, fileName) ->
            let path = Path.Combine(baseDir, subDir, fileName)
            let parent = defaultArg (Option.ofObj (Path.GetDirectoryName(path))) baseDir
            Directory.CreateDirectory(parent) |> ignore
            File.WriteAllText(path, "content")
            path
        )

    /// Recursively discover .fs files in a directory and return their full paths.
    /// Wrapper around Directory.GetFiles for test use.
    static member DiscoverFsFiles(directory : string) : string list =
        Directory.GetFiles(directory, "*.fs", SearchOption.AllDirectories)
        |> Array.toList

    /// Recursively discover .fs files across multiple directories and return their full paths.
    static member DiscoverFsFilesInDirs(directories : string list) : string list =
        directories
        |> List.collect (fun d ->
            Directory.GetFiles(d, "*.fs", SearchOption.AllDirectories)
            |> Array.toList
        )

    /// Assert that a list of file paths contains exactly the expected number of files.
    /// Returns a function string list -> string list for pipeline chaining.
    static member Assert_FileCount(expected : int) : string list -> string list =
        fun files ->
            Assert.Equal(expected, List.length files)
            files

    /// Assert that a list of file paths contains files with the expected names.
    /// Returns a function string list -> string list for pipeline chaining.
    static member Assert_FileNamesContain(expected : string list) : string list -> string list =
        fun files ->
            let names = files |> List.map (fun f -> defaultArg (Option.ofObj (Path.GetFileName(f))) "") |> List.sort
            for e in expected do
                Assert.True(List.contains e names, $"Expected '{e}' in file names")
            files

    /// Assert that a list of file paths does not contain a file with the given name.
    /// Returns a function string list -> string list for pipeline chaining.
    static member Assert_FileNamesNotContain(excluded : string) : string list -> string list =
        fun files ->
            let names = files |> List.map (fun f -> defaultArg (Option.ofObj (Path.GetFileName(f))) "")
            Assert.False(List.contains excluded names, $"Did not expect '{excluded}' in file names")
            files

