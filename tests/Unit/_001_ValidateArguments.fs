module Tests._001_ValidateArguments

open System
open System.IO
open Xunit
open Mutant1.Args

[<Fact>]
let ``parseArgs with -t -p -f returns Ok with File target`` () =
    let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"; "-f"; "file.fs"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal("test.fsproj", parsed.TestProject)
        Assert.Equal("source.fsproj", parsed.SourceProject)
        match parsed.Target with
        | File f -> Assert.Equal("file.fs", f)
        | Directory _ -> Assert.Fail("Expected File target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with -t -p -d returns Ok with Directory target`` () =
    let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"; "-d"; "src_dir"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal("test.fsproj", parsed.TestProject)
        Assert.Equal("source.fsproj", parsed.SourceProject)
        match parsed.Target with
        | Directory d -> Assert.Equal("src_dir", d)
        | File _ -> Assert.Fail("Expected Directory target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with both -f and -d returns Error`` () =
    let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"; "-f"; "file.fs"; "-d"; "src_dir"|]
    match parseArgs args with
    | Error msg ->
        Assert.Contains("mutually exclusive", msg)
    | Ok _ ->
        Assert.Fail("Expected Error when both -f and -d are provided")

[<Fact>]
let ``parseArgs with neither -f nor -d returns Error`` () =
    let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"|]
    match parseArgs args with
    | Error msg ->
        Assert.Contains("-f", msg)
        Assert.Contains("-d", msg)
    | Ok _ ->
        Assert.Fail("Expected Error when neither -f nor -d is provided")

[<Fact>]
let ``parseArgs missing -t returns Error`` () =
    let args = [|"-p"; "source.fsproj"; "-f"; "file.fs"|]
    match parseArgs args with
    | Error msg ->
        Assert.Contains("-t", msg)
    | Ok _ ->
        Assert.Fail("Expected Error when -t is missing")

[<Fact>]
let ``parseArgs missing -p returns Error`` () =
    let args = [|"-t"; "test.fsproj"; "-f"; "file.fs"|]
    match parseArgs args with
    | Error msg ->
        Assert.Contains("-p", msg)
    | Ok _ ->
        Assert.Fail("Expected Error when -p is missing")

[<Fact>]
let ``parseArgs with empty array returns Error`` () =
    let args = [||]
    match parseArgs args with
    | Error msg ->
        Assert.Contains("-t", msg)
        Assert.Contains("-p", msg)
    | Ok _ ->
        Assert.Fail("Expected Error with empty args")

[<Fact>]
let ``parseArgs ignores unknown arguments`` () =
    let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"; "-f"; "file.fs"; "-x"; "unknown"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal("test.fsproj", parsed.TestProject)
        Assert.Equal("source.fsproj", parsed.SourceProject)
        match parsed.Target with
        | File f -> Assert.Equal("file.fs", f)
        | Directory _ -> Assert.Fail("Expected File target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs accepts arguments in any order`` () =
    let args = [|"-f"; "file.fs"; "-t"; "test.fsproj"; "-p"; "source.fsproj"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal("test.fsproj", parsed.TestProject)
        Assert.Equal("source.fsproj", parsed.SourceProject)
        match parsed.Target with
        | File f -> Assert.Equal("file.fs", f)
        | Directory _ -> Assert.Fail("Expected File target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``validate with non-existent files returns Error`` () =
    let parsed = {
        TestProject = "C:\nonexistent\test.fsproj"
        SourceProject = "C:\nonexistent\source.fsproj"
        Target = File "C:\nonexistent\file.fs"
    }
    match validate parsed with
    | Error msg ->
        Assert.Contains("Test project not found", msg)
        Assert.Contains("Source project not found", msg)
        Assert.Contains("File not found", msg)
    | Ok _ ->
        Assert.Fail("Expected Error for non-existent files")

[<Fact>]
let ``validate with existing files returns Ok`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    let parsed = {
        TestProject = testPrj
        SourceProject = sourcePrj
        Target = File fileFs
    }

    try
        match validate parsed with
        | Ok _ -> ()
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate source project without .fsproj extension returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.csproj")
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    let parsed = {
        TestProject = testPrj
        SourceProject = sourcePrj
        Target = File fileFs
    }

    try
        match validate parsed with
        | Error msg ->
            Assert.Contains(".fsproj", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-.fsproj source project")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate file without .fs extension returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let fileCs = Path.Combine(tempDir, "file.cs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")
    File.WriteAllText(fileCs, "class Foo {}")

    let parsed = {
        TestProject = testPrj
        SourceProject = sourcePrj
        Target = File fileCs
    }

    try
        match validate parsed with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-.fs file")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with existing directory returns Ok`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let src_dir = Path.Combine(tempDir, "src")
    Directory.CreateDirectory(src_dir) |> ignore

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")

    let parsed = {
        TestProject = testPrj
        SourceProject = sourcePrj
        Target = Directory src_dir
    }

    try
        match validate parsed with
        | Ok _ -> ()
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with non-existent directory returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let nonExistentDir = Path.Combine(tempDir, "no_dir")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")

    let parsed = {
        TestProject = testPrj
        SourceProject = sourcePrj
        Target = Directory nonExistentDir
    }

    try
        match validate parsed with
        | Error msg ->
            Assert.Contains("Directory not found", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-existent directory")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate with -f resolves relative paths to absolute`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    let originalDir = Environment.CurrentDirectory
    Environment.CurrentDirectory <- tempDir

    try
        let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"; "-f"; "file.fs"|]
        match parseAndValidate args with
        | Ok parsed ->
            Assert.True(Path.IsPathRooted parsed.TestProject)
            Assert.True(Path.IsPathRooted parsed.SourceProject)
            match parsed.Target with
            | File f ->
                Assert.True(Path.IsPathRooted f)
                Assert.Equal(fileFs, f)
            | Directory _ ->
                Assert.Fail("Expected File target")
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Environment.CurrentDirectory <- originalDir
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate with -d resolves relative paths to absolute`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let src_dir = Path.Combine(tempDir, "src")
    Directory.CreateDirectory(src_dir) |> ignore

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")

    let originalDir = Environment.CurrentDirectory
    Environment.CurrentDirectory <- tempDir

    try
        let args = [|"-t"; "test.fsproj"; "-p"; "source.fsproj"; "-d"; "src"|]
        match parseAndValidate args with
        | Ok parsed ->
            Assert.True(Path.IsPathRooted parsed.TestProject)
            Assert.True(Path.IsPathRooted parsed.SourceProject)
            match parsed.Target with
            | Directory d ->
                Assert.True(Path.IsPathRooted d)
                Assert.Equal(src_dir, d)
            | File _ ->
                Assert.Fail("Expected Directory target")
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Environment.CurrentDirectory <- originalDir
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate with missing args returns Error`` () =
    let args = [|"-t"; "test.fsproj"|]
    match parseAndValidate args with
    | Error msg ->
        Assert.Contains("Missing", msg)
    | Ok _ ->
        Assert.Fail("Expected Error for missing arguments")

[<Fact>]
let ``parseAndValidate with non-existent files returns Error`` () =
    let args = [|"-t"; "C:\no\test.fsproj"; "-p"; "C:\no\source.fsproj"; "-f"; "C:\no\file.fs"|]
    match parseAndValidate args with
    | Error msg ->
        Assert.Contains("not found", msg)
    | Ok _ ->
        Assert.Fail("Expected Error for non-existent files")

[<Fact>]
let ``parseAndValidate rejects -f with wrong extension`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.fsproj")
    let fileCs = Path.Combine(tempDir, "file.cs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")
    File.WriteAllText(fileCs, "class Foo {}")

    try
        let args = [|"-t"; testPrj; "-p"; sourcePrj; "-f"; fileCs|]
        match parseAndValidate args with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for .cs file")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate rejects -p with wrong extension`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let sourcePrj = Path.Combine(tempDir, "source.csproj")
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(sourcePrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    try
        let args = [|"-t"; testPrj; "-p"; sourcePrj; "-f"; fileFs|]
        match parseAndValidate args with
        | Error msg ->
            Assert.Contains(".fsproj", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for .csproj source project")
    finally
        Directory.Delete(tempDir, true)
