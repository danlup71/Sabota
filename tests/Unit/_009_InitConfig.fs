module Tests._009_InitConfig

open System.IO
open Xunit
open Sabota.Args
open Sabota.Config
open Sabota.MutationTypes
open TestApp

[<Fact>]
let ``01. when command line specifies init, parse returns Init`` () =
    // Arrange
    "init"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ParseResult_IsInit()

[<Fact>]
let ``02. when init is followed by other flags, parse still returns Init`` () =
    // Arrange
    "init -t test.fsproj -s solution"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ParseResult_IsInit()

[<Fact>]
let ``03. when config file does not exist, init creates it with all categories enabled`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let originalDir = Directory.GetCurrentDirectory()
    try
        Directory.SetCurrentDirectory(baseDir)
        // Act
        let created = initConfig ()
        // Assert
        Assert.True(created, "initConfig should return true when file is created")
        let configPath = Path.Combine(baseDir, Stub.ConfigFileName)
        Assert.True(File.Exists configPath, $"Config file should exist at {configPath}")
        let config = loadConfig configPath
        config
        |> TestApp.Assert_Config_Categories_Count 9
        |> ignore
    finally
        Directory.SetCurrentDirectory(originalDir)
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``04. when config file already exists, init does not overwrite it`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let originalDir = Directory.GetCurrentDirectory()
    try
        Directory.SetCurrentDirectory(baseDir)
        let existingPath = Path.Combine(baseDir, Stub.ConfigFileName)
        File.WriteAllText(existingPath, "{ existing content }") |> ignore
        // Act
        let created = initConfig ()
        // Assert
        Assert.False(created, "initConfig should return false when file already exists")
        let content = File.ReadAllText(existingPath)
        Assert.Equal("{ existing content }", content)
    finally
        Directory.SetCurrentDirectory(originalDir)
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``05. when init creates config, testProject and solutionRoot are placeholder paths`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let originalDir = Directory.GetCurrentDirectory()
    try
        Directory.SetCurrentDirectory(baseDir)
        // Act
        initConfig () |> ignore
        let configPath = Path.Combine(baseDir, Stub.ConfigFileName)
        let content = File.ReadAllText(configPath)
        // Assert
        Assert.Contains("Path_To_UnitTest.fsproj", content)
        Assert.Contains("Path_To_Solution_Folder", content)
    finally
        Directory.SetCurrentDirectory(originalDir)
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``06. when init creates config, all categories are set to true`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let originalDir = Directory.GetCurrentDirectory()
    try
        Directory.SetCurrentDirectory(baseDir)
        // Act
        initConfig () |> ignore
        let configPath = Path.Combine(baseDir, Stub.ConfigFileName)
        let content = File.ReadAllText(configPath)
        // Assert
        let expectedCategories = ["Equality"; "RelationalBoundary"; "RelationalReversal"; "Boolean"; "Arithmetic"; "Conditional"; "Option"; "ReturnListValue"; "ControlFlow"]
        for cat in expectedCategories do
            Assert.Contains($"\"{cat}\": true", content)
    finally
        Directory.SetCurrentDirectory(originalDir)
        TestApp.CleanSimulatedEnvironment baseDir
