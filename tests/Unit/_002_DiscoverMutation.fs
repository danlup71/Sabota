module Tests._002_DiscoverMutation

open System
open System.IO
open Sabota.MutationTypes
open Xunit
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax
open Sabota.MutationCandidates
open Sabota.Visitor

let mutationCategories = [ MutationCategory.Equality
                           MutationCategory.RelationalBoundary
                           MutationCategory.RelationalReversal
                           MutationCategory.Boolean
                           MutationCategory.Arithmetic
                           MutationCategory.Conditional
                           MutationCategory.Option
                           ]

let parseSource (source: string) : ParsedInput =
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

// --- SynExpr tests: boolean constants ---

[<Fact>]
let ``discover TrueToFalse mutation in expression`` () =
    let source = """
module TestModule
let flag = true
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)
    Assert.Single trueToFalse

[<Fact>]
let ``discover FalseToTrue mutation in expression`` () =
    let source = """
module TestModule
let flag = false
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let falseToTrue = candidates |> List.filter (fun c -> c.Kind = MutationKind.FalseToTrue)
    Assert.Single falseToTrue

[<Fact>]
let ``discover both TrueToFalse and FalseToTrue in if-then-else`` () =
    let source = """
module TestModule
let check x =
    if x = 1 then
        true
    else
        false
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)
    let falseToTrue = candidates |> List.filter (fun c -> c.Kind = MutationKind.FalseToTrue)
    Assert.Single trueToFalse |> ignore
    Assert.Single falseToTrue

// --- SynExpr tests: comparison operators ---

[<Fact>]
let ``discover EqualityToInequality mutation for = operator`` () =
    let source = """
module TestModule
let isEqual x y = x = y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let eqToIn = candidates |> List.filter (fun c -> c.Kind = MutationKind.EqualityToInequality)
    Assert.Single eqToIn

[<Fact>]
let ``discover InequalityToEquality mutation for <> operator`` () =
    let source = """
module TestModule
let isNotEqual x y = x <> y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let ineqToEq = candidates |> List.filter (fun c -> c.Kind = MutationKind.InequalityToEquality)
    Assert.Single ineqToEq

[<Fact>]
let ``discover GreaterToGreaterOrEqual mutation for > operator`` () =
    let source = """
module TestModule
let isGreater x y = x > y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let gt = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToGreaterOrEqual)
    Assert.Single gt

[<Fact>]
let ``discover GreaterOrEqualToGreater mutation for >= operator`` () =
    let source = """
module TestModule
let isGreaterOrEqual x y = x >= y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let gte = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterOrEqualToGreater)
    Assert.Single gte

[<Fact>]
let ``discover LessToLessOrEqual mutation for < operator`` () =
    let source = """
module TestModule
let isLess x y = x < y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let lt = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessToLessOrEqual)
    Assert.Single lt

[<Fact>]
let ``discover LessOrEqualToLess mutation for <= operator`` () =
    let source = """
module TestModule
let isLessOrEqual x y = x <= y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let lte = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessOrEqualToLess)
    Assert.Single lte

// --- RelationalReversal mutation tests ---

[<Fact>]
let ``discover GreaterToLess mutation for > operator`` () =
    let source = """
module TestModule
let isGreater x y = x > y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let gt = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToLess)
    Assert.Single gt

[<Fact>]
let ``discover GreaterToLessOrEqual mutation for > operator`` () =
    let source = """
module TestModule
let isGreater x y = x > y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let gt = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToLessOrEqual)
    Assert.Single gt

[<Fact>]
let ``discover LessToGreater mutation for < operator`` () =
    let source = """
module TestModule
let isLess x y = x < y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let lt = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessToGreater)
    Assert.Single lt

[<Fact>]
let ``discover LessToGreaterOrEqual mutation for < operator`` () =
    let source = """
module TestModule
let isLess x y = x < y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let lt = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessToGreaterOrEqual)
    Assert.Single lt

[<Fact>]
let ``discover all 3 mutation candidates for > operator`` () =
    let source = """
module TestModule
let isGreater x y = x > y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let greaterToGreaterOrEqual = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToGreaterOrEqual)
    let greaterToLess = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToLess)
    let greaterToLessOrEqual = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToLessOrEqual)

    Assert.Single greaterToGreaterOrEqual |> ignore
    Assert.Single greaterToLess |> ignore
    Assert.Single greaterToLessOrEqual

[<Fact>]
let ``discover all 3 mutation candidates for < operator`` () =
    let source = """
module TestModule
let isLess x y = x < y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let lessToLessOrEqual = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessToLessOrEqual)
    let lessToGreater = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessToGreater)
    let lessToGreaterOrEqual = candidates |> List.filter (fun c -> c.Kind = MutationKind.LessToGreaterOrEqual)

    Assert.Single lessToLessOrEqual |> ignore
    Assert.Single lessToGreater |> ignore
    Assert.Single lessToGreaterOrEqual

// --- SynExpr tests: multiple candidates in one expression ---

[<Fact>]
let ``discover multiple mutation candidates in a single function`` () =
    let source = """
module TestModule
let compare x y =
    if x = y then
        true
    elif x <> y then
        false
    else
        true
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let eqToIn = candidates |> List.filter (fun c -> c.Kind = MutationKind.EqualityToInequality)
    let ineqToEq = candidates |> List.filter (fun c -> c.Kind = MutationKind.InequalityToEquality)
    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)

    Assert.Single eqToIn |> ignore
    Assert.Single ineqToEq |> ignore
    Assert.Equal(2, trueToFalse.Length)

// --- SynModuleDecl tests: module-level let bindings ---

[<Fact>]
let ``discover mutation in module-level let binding`` () =
    let source = """
module TestModule
let result = 1 = 2
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let eqToIn = candidates |> List.filter (fun c -> c.Kind = MutationKind.EqualityToInequality)
    Assert.Single eqToIn

[<Fact>]
let ``discover mutation in module-level expression`` () =
    let source = """
module TestModule
let x = 1
let y = 2
printfn "%b" (x = y)
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let eqToIn = candidates |> List.filter (fun c -> c.Kind = MutationKind.EqualityToInequality)
    Assert.Single eqToIn

// --- SynModuleDecl tests: nested modules ---

[<Fact>]
let ``discover mutation in nested module`` () =
    let source = """
module TestModule
module Nested =
    let flag = true
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)
    Assert.Single trueToFalse

// --- SynMemberDefn tests: class members ---

[<Fact>]
let ``discover mutation in class member`` () =
    let source = """
module TestModule
type MyClass() =
    member this.Check(x, y) =
        if x = y then true else false
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let eqToIn = candidates |> List.filter (fun c -> c.Kind = MutationKind.EqualityToInequality)
    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)
    let falseToTrue = candidates |> List.filter (fun c -> c.Kind = MutationKind.FalseToTrue)

    Assert.Single eqToIn |> ignore
    Assert.Single trueToFalse |> ignore
    Assert.Single falseToTrue

[<Fact>]
let ``discover mutation in class let binding`` () =
    let source = """
module TestModule
type MyClass() =
    let internalFlag = false
    member this.GetFlag() = internalFlag
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let falseToTrue = candidates |> List.filter (fun c -> c.Kind = MutationKind.FalseToTrue)
    Assert.Single falseToTrue

[<Fact>]
let ``discover mutation in auto-property with default value`` () =
    let source = """
module TestModule
type Config() =
    member val Enabled = true with get, set
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)
    Assert.Single trueToFalse

// --- Arithmetic mutation tests ---

[<Fact>]
let ``discover PlusToMinus mutation for + operator`` () =
    let source = """
module TestModule
let add x y = x + y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let plusToMinus = candidates |> List.filter (fun c -> c.Kind = MutationKind.PlusToMinus)
    Assert.Single plusToMinus

[<Fact>]
let ``discover MinusToPlus mutation for - operator`` () =
    let source = """
module TestModule
let subtract x y = x - y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let minusToPlus = candidates |> List.filter (fun c -> c.Kind = MutationKind.MinusToPlus)
    Assert.Single minusToPlus

[<Fact>]
let ``discover TimesToDivide mutation for * operator`` () =
    let source = """
module TestModule
let multiply x y = x * y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let timesToDivide = candidates |> List.filter (fun c -> c.Kind = MutationKind.TimesToDivide)
    Assert.Single timesToDivide

[<Fact>]
let ``discover DivideToTimes mutation for / operator`` () =
    let source = """
module TestModule
let divide x y = x / y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let divideToTimes = candidates |> List.filter (fun c -> c.Kind = MutationKind.DivideToTimes)
    Assert.Single divideToTimes

[<Fact>]
let ``discover all 4 arithmetic mutation candidates in one function`` () =
    let source = """
module TestModule
let calc x y =
    let a = x + y
    let b = x - y
    let c = x * y
    let d = x / y
    a + b + c + d
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let plusToMinus = candidates |> List.filter (fun c -> c.Kind = MutationKind.PlusToMinus)
    let minusToPlus = candidates |> List.filter (fun c -> c.Kind = MutationKind.MinusToPlus)
    let timesToDivide = candidates |> List.filter (fun c -> c.Kind = MutationKind.TimesToDivide)
    let divideToTimes = candidates |> List.filter (fun c -> c.Kind = MutationKind.DivideToTimes)

    Assert.True(plusToMinus.Length >= 1)
    Assert.Single minusToPlus |> ignore
    Assert.Single timesToDivide |> ignore
    Assert.Single divideToTimes

// --- Conditional mutation tests ---

[<Fact>]
let ``discover AndToOr mutation for && operator`` () =
    let source = """
module TestModule
let check x y = x && y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let andToOr = candidates |> List.filter (fun c -> c.Kind = MutationKind.AndToOr)
    Assert.Single andToOr

[<Fact>]
let ``discover OrToAnd mutation for || operator`` () =
    let source = """
module TestModule
let check x y = x || y
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let orToAnd = candidates |> List.filter (fun c -> c.Kind = MutationKind.OrToAnd)
    Assert.Single orToAnd

[<Fact>]
let ``discover both AndToOr and OrToAnd in one function`` () =
    let source = """
module TestModule
let check x y z = (x && y) || z
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let andToOr = candidates |> List.filter (fun c -> c.Kind = MutationKind.AndToOr)
    let orToAnd = candidates |> List.filter (fun c -> c.Kind = MutationKind.OrToAnd)

    Assert.Single andToOr |> ignore
    Assert.Single orToAnd

// --- Option mutation tests ---

[<Fact>]
let ``discover SomeToNone mutation for Some constructor`` () =
    let source = """
module TestModule
let getValue x = Some x
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let someToNone = candidates |> List.filter (fun c -> c.Kind = MutationKind.SomeToNone)
    Assert.Single someToNone

[<Fact>]
let ``discover SomeToNone mutation in Option return`` () =
    let source = """
module TestModule
let tryParse x =
    if x > 0 then Some x else None
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let someToNone = candidates |> List.filter (fun c -> c.Kind = MutationKind.SomeToNone)
    Assert.Single someToNone

[<Fact>]
let ``discover SomeToNone in nested Option expression`` () =
    let source = """
module TestModule
let mapOption f opt =
    match opt with
    | Some x -> Some (f x)
    | None -> None
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let someToNone = candidates |> List.filter (fun c -> c.Kind = MutationKind.SomeToNone)
    Assert.True(someToNone.Length >= 1)

[<Fact>]
let ``discover SomeToNone in record update expression`` () =
    let source = """
module TestModule
type User = { name: string; email: string option }
let setEmail f email = { f with email = Some email }
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let someToNone = candidates |> List.filter (fun c -> c.Kind = MutationKind.SomeToNone)
    Assert.Single someToNone

[<Fact>]
let ``discover SomeToNone in record update inside lambda pipeline`` () =
    let source = """
module TestModule
type Field = { name: string; value: string option }
let setEmail f email =
    textField "email" "Email"
    |> fun f -> { f with enabled = false }
    |> fun f -> { f with value = Some email }
and textField name label = { name = name; value = None; enabled = true }
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let someToNone = candidates |> List.filter (fun c -> c.Kind = MutationKind.SomeToNone)
    Assert.Single someToNone

[<Fact>]
let ``discover SomeToNone with qualified Option.Some`` () =
    let source = """
module TestModule
type Field = { name: string; value: string option }
let setEmail f email = { f with value = Option.Some email }
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let someToNone = candidates |> List.filter (fun c -> c.Kind = MutationKind.SomeToNone)
    Assert.Single someToNone

// --- Qualified operator tests ---

[<Fact>]
let ``discover PlusToMinus with qualified LanguagePrimitives.op_Addition`` () =
    let source = """
module TestModule
let add x y = LanguagePrimitives.op_Addition(x, y)
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let plusToMinus = candidates |> List.filter (fun c -> c.Kind = MutationKind.PlusToMinus)
    Assert.Single plusToMinus

[<Fact>]
let ``discover AndToOr with qualified LanguagePrimitives.op_BooleanAnd`` () =
    let source = """
module TestModule
let check x y = LanguagePrimitives.op_BooleanAnd(x, y)
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let andToOr = candidates |> List.filter (fun c -> c.Kind = MutationKind.AndToOr)
    Assert.Single andToOr

[<Fact>]
let ``discover GreaterToLess with qualified LanguagePrimitives.op_GreaterThan`` () =
    let source = """
module TestModule
let check x y = LanguagePrimitives.op_GreaterThan(x, y)
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let greaterToLess = candidates |> List.filter (fun c -> c.Kind = MutationKind.GreaterToLess)
    Assert.Single greaterToLess

[<Fact>]
let ``discover TrueToFalse with qualified bool true`` () =
    let source = """
module TestModule
let flag = bool true
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let trueToFalse = candidates |> List.filter (fun c -> c.Kind = MutationKind.TrueToFalse)
    Assert.Single trueToFalse

// --- Edge cases ---

[<Fact>]
let ``return empty list when no mutation candidates exist`` () =
    let source = """
module TestModule
let x = 42
let y = "hello"
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    Assert.Empty candidates

[<Fact>]
let ``candidate file path matches the input file`` () =
    let source = """
module TestModule
let flag = true
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "customPath.fs" parsed

    Assert.True(candidates |> List.forall (fun c -> c.File = "customPath.fs"))

[<Fact>]
let ``candidate IDs are unique and sequential`` () =
    let source = """
module TestModule
let a = true
let b = false
let c = 1 = 2
"""
    let parsed = parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed

    let ids = candidates |> List.map (fun c -> c.Id)
    let sortedIds = ids |> List.sort
    Assert.True((sortedIds = ids))
    Assert.True((ids |> List.distinct |> List.length) = ids.Length)
