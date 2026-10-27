module TestApp

open System
open System.IO
open Xunit
open Sabota.Args
open Sabota.MutationTypes
open Sabota.MutationCandidates
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

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

    /// Write a file with custom content into an existing directory.
    /// Returns the full file path.
    /// Example:
    ///   let baseDir = TestApp.CreateSimulatedBaseDir()
    ///   let filePath = TestApp.CreateFileWithContent(baseDir, "test.fs", "let x = 1")
    static member CreateFileWithContent(baseDir : string, fileName : string, content : string) : string =
        let path = Path.Combine(baseDir, fileName)
        File.WriteAllText(path, content)
        path

    /// Returns the path to the sabota parent directory in the system temp folder.
    /// Example:
    ///   let sabotaDir = TestApp.GetSabotaDir()
    static member GetSabotaDir() : string =
        Path.Combine(Path.GetTempPath(), "sabota")

    /// Clean up a simulated project environment by deleting its base directory.
    /// Example:
    ///   let baseDir = TestApp.CreateSimulatedBaseDir()
    ///   let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, Stub.FileFsName, baseDir)
    ///   try
    ///       validate env.TestProject env.SolutionRoot (File [env.FileFs])
    ///   finally
    ///       TestApp.CleanSimulatedEnvironment baseDir
    static member CleanSimulatedEnvironment(baseDir : string) : unit =
        if Directory.Exists baseDir then
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

    /// Assert that a MutationCategory is present in the given list of categories.
    /// Returns a function MutationCategory -> MutationCategory for pipeline chaining.
    static member Assert_Category_InList (categories : MutationCategory list) : MutationCategory -> MutationCategory =
        fun category ->
            Assert.True(List.contains category categories, $"Category {category} not in the list")
            category

    /// Assert that a MutationDefinition has non-empty Original and Replacement fields.
    /// Returns a function MutationDefinition -> MutationDefinition for pipeline chaining.
    static member Assert_Mutation_Definition_NonEmpty: Sabota.MuationDefinitions.MutationDefinition -> Sabota.MuationDefinitions.MutationDefinition =
        fun def ->
            Assert.False(String.IsNullOrEmpty def.Original, "Definition has empty Original")
            Assert.False(String.IsNullOrEmpty def.Replacement, "Definition has empty Replacement")
            def

    /// Assert that a string label is non-empty.
    /// Returns a function string -> string for pipeline chaining.
    static member Assert_Mutation_Label_NonEmpty: string -> string =
        fun label ->
            Assert.False(String.IsNullOrEmpty label, "Label is empty")
            label

    /// Assert that a string contains the expected substring.
    /// Returns a function string -> string for pipeline chaining.
    static member Assert_Contains (expected : string) : string -> string =
        fun actual ->
            Assert.Contains(expected, actual)
            actual

    /// Assert that a string does not contain the excluded substring.
    /// Returns a function string -> string for pipeline chaining.
    static member Assert_NotContains (excluded : string) : string -> string =
        fun actual ->
            Assert.DoesNotContain(excluded, actual)
            actual

    /// Assert that a MutationResult has the expected MutationId.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_MutationId (expected : int) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Equal(expected, result.MutationId)
            result

    /// Assert that a MutationResult has the expected MutationType.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_MutationType (expected : MutationKind) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Equal(expected, result.MutationType)
            result

    /// Assert that a MutationResult has the expected File path.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_File (expected : string) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Equal(expected, result.File)
            result

    /// Assert that a MutationResult Outcome has the expected Status.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_OutcomeStatus (expected : Sabota.Report.MutationStatus) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Equal(expected, result.Outcome.Status)
            result

    /// Assert that a MutationResult PreviewOriginal is non-empty.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_PreviewOriginal_NonEmpty : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.NotEmpty(result.PreviewOriginal)
            result

    /// Assert that a MutationResult PreviewMutated is non-empty.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_PreviewMutated_NonEmpty : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.NotEmpty(result.PreviewMutated)
            result

    /// Assert that a MutationResult Link is non-empty.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_Link_NonEmpty : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.NotEmpty(result.Link)
            result

    /// Assert that a MutationResult Link contains the expected substring.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_Link_Contains (expected : string) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Contains(expected, result.Link)
            result

    /// Assert that a MutationResult PreviewOriginal contains the expected substring.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_PreviewOriginal_Contains (expected : string) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Contains(expected, result.PreviewOriginal)
            result

    /// Assert that a MutationResult PreviewMutated contains the expected substring.
    /// Returns a function MutationResult -> MutationResult for pipeline chaining.
    static member Assert_Result_PreviewMutated_Contains (expected : string) : Sabota.Report.MutationResult -> Sabota.Report.MutationResult =
        fun result ->
            Assert.Contains(expected, result.PreviewMutated)
            result


    /// Assert that the candidates list contains exactly one mutation of the given Kind.
    /// Returns a function MutationCandidate list -> MutationCandidate list for pipeline chaining.
    static member Assert_MutationKind_Has (kind : MutationKind) : MutationCandidate list -> MutationCandidate list =
        fun candidates ->
            let count = candidates |> List.fold (fun acc c -> if c.Kind = kind then acc + 1 else acc) 0
            Assert.Equal(1, count)
            candidates

    /// Assert that the candidates list contains at least one mutation of the given Kind.
    /// Returns a function MutationCandidate list -> MutationCandidate list for pipeline chaining.
    static member Assert_MutationKind_Contains (kind : MutationKind) : MutationCandidate list -> MutationCandidate list =
        fun candidates ->
            let count = candidates |> List.fold (fun acc c -> if c.Kind = kind then acc + 1 else acc) 0
            Assert.True(count >= 1, $"Expected at least 1 candidate of kind {kind}, found {count}")
            candidates

    /// Assert that the candidates list contains exactly the expected count of mutations of the given Kind.
    /// Returns a function MutationCandidate list -> MutationCandidate list for pipeline chaining.
    static member Assert_MutationKind_Count (kind : MutationKind) (expected : int) : MutationCandidate list -> MutationCandidate list =
        fun candidates ->
            let count = candidates |> List.fold (fun acc c -> if c.Kind = kind then acc + 1 else acc) 0
            Assert.Equal(expected, count)
            candidates

    /// Assert that the candidates list is empty (no mutation candidates found).
    /// Returns a function MutationCandidate list -> MutationCandidate list for pipeline chaining.
    static member Assert_NoCandidates : MutationCandidate list -> MutationCandidate list =
        fun candidates ->
            Assert.Empty(candidates)
            candidates

    /// Assert that all candidates have the expected file path.
    /// Returns a function MutationCandidate list -> MutationCandidate list for pipeline chaining.
    static member Assert_AllCandidatesFile (expected : string) : MutationCandidate list -> MutationCandidate list =
        fun candidates ->
            Assert.True(List.forall (fun c -> c.File = expected) candidates, $"Expected all candidates to have file '{expected}'")
            candidates

    /// Parse F# source code into a ParsedInput (parse tree) using the F# compiler service.
    /// Creates a temporary file, parses it, and cleans up the temp file.
    /// Example:
    ///   let parsed = TestApp.parseSource "let x = 1"
    static member parseSource (source: string) : ParsedInput =
        let checker = FSharpChecker.Create()
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".fs")
        try
            File.WriteAllText(tempFile, source)
            let parsingOptions = { FSharpParsingOptions.Default with SourceFiles = [| tempFile |] }
            let parseResult =
                checker.ParseFile(
                    tempFile,
                    SourceText.ofString source,
                    parsingOptions)
                |> Async.RunSynchronously
            parseResult.ParseTree
        finally
            if File.Exists tempFile then
                File.Delete(tempFile)

    /// Assert that a file's content contains the expected substring.
    /// Returns a function string -> string for pipeline chaining (string is the file path).
    static member Assert_FileContent_Contains (expected : string) : string -> string =
        fun filePath ->
            let content = File.ReadAllText(filePath)
            Assert.Contains(expected, content)
            filePath

    /// Assert that a file's content does not contain the expected substring.
    /// Returns a function string -> string for pipeline chaining (string is the file path).
    static member Assert_FileContent_NotContains (excluded : string) : string -> string =
        fun filePath ->
            let content = File.ReadAllText(filePath)
            Assert.DoesNotContain(excluded, content)
            filePath

    /// Assert that a file's content equals the expected string exactly.
    /// Returns a function string -> string for pipeline chaining (string is the file path).
    static member Assert_FileContent_Equal (expected : string) : string -> string =
        fun filePath ->
            let content = File.ReadAllText(filePath)
            Assert.Equal(expected, content)
            filePath

    /// Assert that a directory exists.
    /// Returns a function string -> string for pipeline chaining (string is the directory path).
    static member Assert_Directory_Exists : string -> string =
        fun dirPath ->
            Assert.True(Directory.Exists(dirPath), $"Expected directory to exist: {dirPath}")
            dirPath

    /// Assert that a directory does not exist.
    /// Returns a function string -> string for pipeline chaining (string is the directory path).
    static member Assert_Directory_NotExists : string -> string =
        fun dirPath ->
            Assert.False(Directory.Exists(dirPath), $"Expected directory to not exist: {dirPath}")
            dirPath

    /// Assert that a string contains the expected substring (general purpose).
    /// Returns a function string -> string for pipeline chaining.
    static member Assert_String_Contains (expected : string) : string -> string =
        fun value ->
            Assert.Contains(expected, value)
            value

    /// Assert that a file exists.
    /// Returns a function string -> string for pipeline chaining (string is the file path).
    static member Assert_File_Exists : string -> string =
        fun filePath ->
            Assert.True(File.Exists(filePath), $"Expected file to exist: {filePath}")
            filePath

    /// Assert that the enriched candidate's Original field matches the expected value.
    /// Returns a function MutationCandidate -> MutationCandidate for pipeline chaining.
    static member Assert_Enriched_Original (expected : string) : MutationCandidate -> MutationCandidate =
        fun candidate ->
            Assert.Equal(expected, candidate.Original)
            candidate

    /// Assert that the enriched candidate's Replacement field matches the expected value.
    /// Returns a function MutationCandidate -> MutationCandidate for pipeline chaining.
    static member Assert_Enriched_Replacement (expected : string) : MutationCandidate -> MutationCandidate =
        fun candidate ->
            Assert.Equal(expected, candidate.Replacement)
            candidate

    /// Assert that the enriched candidate's metadata fields are preserved.
    /// Returns a function MutationCandidate -> MutationCandidate for pipeline chaining.
    static member Assert_Enriched_Metadata_Preserved (originalCandidate : MutationCandidate) : MutationCandidate -> MutationCandidate =
        fun enriched ->
            Assert.Equal(originalCandidate.File, enriched.File)
            Assert.Equal(originalCandidate.Kind, enriched.Kind)
            Assert.Equal(originalCandidate.Id, enriched.Id)
            Assert.Equal(originalCandidate.Range, enriched.Range)
            enriched

    /// Create a mutation test environment: a temp directory with a .fs file containing the given source,
    /// parsed and enriched candidate for the specified MutationKind.
    /// Returns a tuple of (baseDir, filePath, enrichedCandidate) for use in the test.
    /// The caller is responsible for cleaning up baseDir.
    /// Example:
    ///   let baseDir, filePath, candidate = TestApp.CreateMutationTestEnv("let flag = true", MutationKind.TrueToFalse, categories)
    ///   try
    ///       applyMutation candidate
    ///       filePath |> TestApp.Assert_FileContent_Contains "false"
    ///   finally
    ///       TestApp.CleanSimulatedEnvironment baseDir
    static member CreateMutationTestEnv(source : string, kind : MutationKind, categories : MutationCategory list) : string * string * MutationCandidate =
        let baseDir = TestApp.CreateSimulatedBaseDir()
        let filePath = Path.Combine(baseDir, "test.fs")
        File.WriteAllText(filePath, source)
        let parsed = TestApp.parseSource source
        let candidates = Sabota.Visitor.findMutationCandidates categories filePath parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = kind)
        let enriched = Sabota.MuationDefinitions.enrich candidate
        (baseDir, filePath, enriched)

    /// Create a source directory structure for copy directory recursive tests.
    /// Accepts a list of (relativeSubDir, fileName, content) tuples for files and a list of directory names to create.
    /// Returns a tuple of (srcDir, tgtDir) where tgtDir is the sibling target path.
    /// Example:
    ///   let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(baseDir, "source", [(".git", "config", "git"); ("src", "Module.fs", "module")], [])
    static member CreateCopyDirectoryStructure(baseDir : string, srcName : string, files : (string * string * string) list, directories : string list) : string * string =
        let srcDir = Path.Combine(baseDir, srcName)
        let tgtDir = Path.Combine(baseDir, "target")
        // Create all directories first
        directories
        |> List.iter (fun dirName ->
            let path = Path.Combine(srcDir, dirName)
            Directory.CreateDirectory(path) |> ignore
        )
        // Create files with their subdirectories
        files
        |> List.iter (fun (subDir, fileName, content) ->
            let path = Path.Combine(srcDir, subDir, fileName)
            let parent = defaultArg (Option.ofObj (Path.GetDirectoryName(path))) srcDir
            Directory.CreateDirectory(parent) |> ignore
            File.WriteAllText(path, content)
        )
        (srcDir, tgtDir)

    /// Check whether a file path passes through an excluded build directory.
    /// Splits the path on '/' and '\' and checks if any component matches the excluded set (case-insensitive).
    /// Returns true if the file is excluded, false otherwise.
    static member isExcludedFile (file : string) : bool =
        let parts = file.Split([|'\\'; '/'|], System.StringSplitOptions.RemoveEmptyEntries)
        parts |> Array.exists (fun part -> Stub.ExcludedBuildDirs.Contains(part.ToLower()))

    /// Assert that a file path is excluded (passes through an excluded build directory).
    /// Returns a function string -> string for pipeline chaining.
    static member Assert_IsExcludedFile : string -> string =
        fun file ->
            Assert.True(TestApp.isExcludedFile(file), $"Expected '{file}' to be excluded")
            file

    /// Assert that a file path is included (does not pass through an excluded build directory).
    /// Returns a function string -> string for pipeline chaining.
    static member Assert_IsIncluded : string -> string =
        fun file ->
            Assert.False(TestApp.isExcludedFile(file), $"Expected '{file}' to be included")
            file

    /// Assert that dot-prefixed directories were not copied to the target directory.
    /// Returns a function string -> string for pipeline chaining (string is the target directory path).
    static member Assert_Copy_SkipsDotPrefixed (dotDirs : string list) : string -> string =
        fun tgtDir ->
            for dir in dotDirs do
                let path = Path.Combine(tgtDir, dir)
                Assert.False(Directory.Exists(path), $"Expected '{dir}' to not be copied")
            tgtDir


