module TestApp

open Xunit
open Sabota.Args

/// Unwrap a ParseResult Ok value, asserting on the Target field's size.
/// Returns the ParsedArgs record for further chaining.
let Assert_ArgsTarget_Size (expected : int) (result : ParseResult) : ParsedArgs =
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
/// Returns the ParsedArgs record for further chaining.
let Assert_ArgsTarget_Contains (expected : string list) (parsed : ParsedArgs) : ParsedArgs =
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
/// Returns the ParsedArgs record for further chaining.
let Assert_ArgsDirectory_Contains (expected : string list) (parsed : ParsedArgs) : ParsedArgs =
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
let Assert_ArgsError_Contains (expected : string) (result : ParseResult) : unit =
    match result with
    | Error msg ->
        Assert.Contains(expected, msg)
    | Ok _ ->
        Assert.Fail("Expected Error, got Ok")

/// Assert the TestProject field matches the expected value.
/// Returns the ParsedArgs record for further chaining.
let Assert_ArgsTestProject (expected : string option) (parsed : ParsedArgs) : ParsedArgs =
    Assert.Equal(expected, parsed.TestProject)
    parsed

/// Assert the SolutionRoot field matches the expected value.
/// Returns the ParsedArgs record for further chaining.
let Assert_ArgsSolutionRoot (expected : string option) (parsed : ParsedArgs) : ParsedArgs =
    Assert.Equal(expected, parsed.SolutionRoot)
    parsed
