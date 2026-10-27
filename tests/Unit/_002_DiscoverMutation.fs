module Tests._002_DiscoverMutation

open Sabota.MutationTypes
open Xunit
open Sabota.MutationCandidates
open Sabota.Visitor
open TestApp

let mutationCategories = Stub.AllMutationCategories
let testFile = "test.fs"


// --- SynExpr tests: boolean constants ---

[<Fact>]
let ``01. discover TrueToFalse mutation in expression`` () =
    // Arrange
    """
    module TestModule
    let flag = true
    """    
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse

[<Fact>]
let ``02. discover FalseToTrue mutation in expression`` () =
    // Arrange
    """
    module TestModule
    let flag = false
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.FalseToTrue

[<Fact>]
let ``03. discover both TrueToFalse and FalseToTrue in if-then-else`` () =
    // Arrange
    """
    module TestModule
    let check x =
        if x = 1 then
            true
        else
            false
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse
    |> TestApp.Assert_MutationKind_Has MutationKind.FalseToTrue

// --- SynExpr tests: comparison operators ---

[<Fact>]
let ``04. discover EqualityToInequality mutation for = operator`` () =
    // Arrange
    """
    module TestModule
    let isEqual x y = x = y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality

[<Fact>]
let ``05. discover InequalityToEquality mutation for <> operator`` () =
    // Arrange
    """
    module TestModule
    let isNotEqual x y = x <> y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.InequalityToEquality

[<Fact>]
let ``06. discover GreaterToGreaterOrEqual mutation for > operator`` () =
    // Arrange
    """
    module TestModule
    let isGreater x y = x > y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToGreaterOrEqual

[<Fact>]
let ``07. discover GreaterOrEqualToGreater mutation for >= operator`` () =
    // Arrange
    """
    module TestModule
    let isGreaterOrEqual x y = x >= y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterOrEqualToGreater

[<Fact>]
let ``08. discover LessToLessOrEqual mutation for < operator`` () =
    // Arrange
    """
    module TestModule
    let isLess x y = x < y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.LessToLessOrEqual

[<Fact>]
let ``09. discover LessOrEqualToLess mutation for <= operator`` () =
    // Arrange
    """
    module TestModule
    let isLessOrEqual x y = x <= y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.LessOrEqualToLess

// --- RelationalReversal mutation tests ---

[<Fact>]
let ``10. discover GreaterToLess mutation for > operator`` () =
    // Arrange
    """
    module TestModule
    let isGreater x y = x > y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToLess

[<Fact>]
let ``11. discover GreaterToLessOrEqual mutation for > operator`` () =
    // Arrange
    """
    module TestModule
    let isGreater x y = x > y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToLessOrEqual

[<Fact>]
let ``12. discover LessToGreater mutation for < operator`` () =
    // Arrange
    """
    module TestModule
    let isLess x y = x < y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.LessToGreater

[<Fact>]
let ``13. discover LessToGreaterOrEqual mutation for < operator`` () =
    // Arrange
    """
    module TestModule
    let isLess x y = x < y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.LessToGreaterOrEqual

[<Fact>]
let ``14. discover all 3 mutation candidates for > operator`` () =
    // Arrange
    """
    module TestModule
    let isGreater x y = x > y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToGreaterOrEqual
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToLess
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToLessOrEqual

[<Fact>]
let ``15. discover all 3 mutation candidates for < operator`` () =
    // Arrange
    """
    module TestModule
    let isLess x y = x < y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.LessToLessOrEqual
    |> TestApp.Assert_MutationKind_Has MutationKind.LessToGreater
    |> TestApp.Assert_MutationKind_Has MutationKind.LessToGreaterOrEqual

// --- SynExpr tests: multiple candidates in one expression ---

[<Fact>]
let ``16. discover multiple mutation candidates in a single function`` () =
    // Arrange
    """
    module TestModule
    let compare x y =
        if x = y then
            true
        elif x <> y then
            false
        else
            true
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality
    |> TestApp.Assert_MutationKind_Has MutationKind.InequalityToEquality
    |> TestApp.Assert_MutationKind_Count MutationKind.TrueToFalse 2

// --- SynModuleDecl tests: module-level let bindings ---

[<Fact>]
let ``17. discover mutation in module-level let binding`` () =
    // Arrange
    """
    module TestModule
    let result = 1 = 2
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality

[<Fact>]
let ``18. discover mutation in module-level expression`` () =
    // Arrange
    """
    module TestModule
    let x = 1
    let y = 2
    printfn "%b" (x = y)
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality

// --- SynModuleDecl tests: nested modules ---

[<Fact>]
let ``19. discover mutation in nested module`` () =
    // Arrange
    """
    module TestModule
    module Nested =
        let flag = true
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse

// --- SynMemberDefn tests: class members ---

[<Fact>]
let ``20. discover mutation in class member`` () =
    // Arrange
    """
    module TestModule
    type MyClass() =
        member this.Check(x, y) =
            if x = y then true else false
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality
    |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse
    |> TestApp.Assert_MutationKind_Has MutationKind.FalseToTrue

[<Fact>]
let ``21. discover mutation in class let binding`` () =
    // Arrange
    """
    module TestModule
    type MyClass() =
        let internalFlag = false
        member this.GetFlag() = internalFlag
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.FalseToTrue

[<Fact>]
let ``22. discover mutation in auto-property with default value`` () =
    // Arrange
    """
    module TestModule
    type Config() =
        member val Enabled = true with get, set
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse

// --- Arithmetic mutation tests ---

[<Fact>]
let ``23. discover PlusToMinus mutation for + operator`` () =
    // Arrange
    """
    module TestModule
    let add x y = x + y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.PlusToMinus

[<Fact>]
let ``24. discover MinusToPlus mutation for - operator`` () =
    // Arrange
    """
    module TestModule
    let subtract x y = x - y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.MinusToPlus

[<Fact>]
let ``25. discover TimesToDivide mutation for * operator`` () =
    // Arrange
    """
    module TestModule
    let multiply x y = x * y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.TimesToDivide

[<Fact>]
let ``26. discover DivideToTimes mutation for / operator`` () =
    // Arrange
    """
    module TestModule
    let divide x y = x / y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.DivideToTimes

[<Fact>]
let ``27. discover all 4 arithmetic mutation candidates in one function`` () =
    // Arrange
    """
    module TestModule
    let calc x y =
        let a = x + y
        let b = x - y
        let c = x * y
        let d = x / y
        a + b + c + d
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Contains MutationKind.PlusToMinus
    |> TestApp.Assert_MutationKind_Has MutationKind.MinusToPlus
    |> TestApp.Assert_MutationKind_Has MutationKind.TimesToDivide
    |> TestApp.Assert_MutationKind_Has MutationKind.DivideToTimes

// --- Conditional mutation tests ---

[<Fact>]
let ``28. discover AndToOr mutation for && operator`` () =
    // Arrange
    """
    module TestModule
    let check x y = x && y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.AndToOr

[<Fact>]
let ``29. discover OrToAnd mutation for || operator`` () =
    // Arrange
    """
    module TestModule
    let check x y = x || y
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.OrToAnd

[<Fact>]
let ``30. discover both AndToOr and OrToAnd in one function`` () =
    // Arrange
    """
    module TestModule
    let check x y z = (x && y) || z
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.AndToOr
    |> TestApp.Assert_MutationKind_Has MutationKind.OrToAnd

// --- Option mutation tests ---

[<Fact>]
let ``31. discover SomeToNone mutation for Some constructor`` () =
    // Arrange
    """
    module TestModule
    let getValue x = Some x
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.SomeToNone

[<Fact>]
let ``32. discover SomeToNone mutation in Option return`` () =
    // Arrange
    """
    module TestModule
    let tryParse x =
        if x > 0 then Some x else None
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.SomeToNone

[<Fact>]
let ``33. discover SomeToNone in nested Option expression`` () =
    // Arrange
    """
    module TestModule
    let mapOption f opt =
        match opt with
        | Some x -> Some (f x)
        | None -> None
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Contains MutationKind.SomeToNone

[<Fact>]
let ``34. discover SomeToNone in record update expression`` () =
    // Arrange
    """
    module TestModule
    type User = { name: string; email: string option }
    let setEmail f email = { f with email = Some email }
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.SomeToNone

[<Fact>]
let ``35. discover SomeToNone in record update inside lambda pipeline`` () =
    // Arrange
    """
    module TestModule
    type Field = { name: string; value: string option }
    let setEmail f email =
        textField "email" "Email"
        |> fun f -> { f with enabled = false }
        |> fun f -> { f with value = Some email }
    and textField name label = { name = name; value = None; enabled = true }
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.SomeToNone

[<Fact>]
let ``36. discover SomeToNone with qualified Option.Some`` () =
    // Arrange
    """
    module TestModule
    type Field = { name: string; value: string option }
    let setEmail f email = { f with value = Option.Some email }
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.SomeToNone

// --- Qualified operator tests ---

[<Fact>]
let ``37. discover PlusToMinus with qualified LanguagePrimitives.op_Addition`` () =
    // Arrange
    """
    module TestModule
    let add x y = LanguagePrimitives.op_Addition(x, y)
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.PlusToMinus

[<Fact>]
let ``38. discover AndToOr with qualified LanguagePrimitives.op_BooleanAnd`` () =
    // Arrange
    """
    module TestModule
    let check x y = LanguagePrimitives.op_BooleanAnd(x, y)
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.AndToOr

[<Fact>]
let ``39. discover GreaterToLess with qualified LanguagePrimitives.op_GreaterThan`` () =
    // Arrange
    """
    module TestModule
    let check x y = LanguagePrimitives.op_GreaterThan(x, y)
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToLess

[<Fact>]
let ``40. discover TrueToFalse with qualified bool true`` () =
    // Arrange
    """
    module TestModule
    let flag = bool true
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse

// --- ReturnValue mutation tests ---

[<Fact>]
let ``41. discover ListReturnValueToEmpty for list expression`` () =
    // Arrange
    """
    module TestModule
    let items = [1; 2; 3]
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.ReturnListValueToEmpty

[<Fact>]
let ``42. discover ListReturnValueToEmpty in function return`` () =
    // Arrange
    """
    module TestModule
    let getItems x =
        if x > 0 then [1; 2; 3] else []
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.ReturnListValueToEmpty

[<Fact>]
let ``43. discover ListReturnValueToEmpty in nested expression`` () =
    // Arrange
    """
    module TestModule
    let process items =
        let filtered = [for i in items do if i > 0 then yield i]
        filtered @ [0]
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Contains MutationKind.ReturnListValueToEmpty

// --- Edge cases ---

[<Fact>]
let ``44. return empty list when no mutation candidates exist`` () =
    // Arrange
    """
    module TestModule
    let x = 42
    let y = "hello"
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_NoCandidates

[<Fact>]
let ``45. candidate file path matches the input file`` () =
    // Arrange
    let customFile = "customPath.fs"
    """
    module TestModule
    let flag = true
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories customFile
    // Assert
    |> TestApp.Assert_AllCandidatesFile customFile

[<Fact>]
let ``46. candidate IDs are unique and sequential`` () =
    // Arrange
    """
    module TestModule
    let a = true
    let b = false
    let c = 1 = 2
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> fun candidates ->
        let ids = candidates |> List.map (fun c -> c.Id)
        let sortedIds = ids |> List.sort
        (sortedIds = ids) |> Assert.True
        ((ids |> List.distinct |> List.length) = ids.Length) |> Assert.True

// --- ControlFlow mutation tests ---

[<Fact>]
let ``47. discover ConditionToNotCondition in simple if-then`` () =
    // Arrange
    """
    module TestModule
    let check x =
        if x > 0 then
            1
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.ConditionToNotCondition

[<Fact>]
let ``48. discover ConditionToNotCondition in if-then-else`` () =
    // Arrange
    """
    module TestModule
    let check x =
        if x > 0 then
            1
        else
            0
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.ConditionToNotCondition

[<Fact>]
let ``49. discover NotConditionToCondition in negated if`` () =
    // Arrange
    """
    module TestModule
    let check x =
        if not (x > 0) then
            1
        else
            0
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.NotConditionToCondition

[<Fact>]
let ``50. discover ConditionToNotCondition in nested if`` () =
    // Arrange
    """
    module TestModule
    let check x y =
        if x > 0 then
            if y > 0 then
                1
            else
                0
        else
            -1
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Count MutationKind.ConditionToNotCondition 2

[<Fact>]
let ``51. discover both ConditionToNotCondition and NotConditionToCondition`` () =
    // Arrange
    """
    module TestModule
    let check x y =
        if x > 0 then
            if not (y > 0) then
                1
            else
                0
    """
    |> TestApp.parseSource
    // Act
    |> findMutationCandidates mutationCategories testFile
    // Assert
    |> TestApp.Assert_MutationKind_Has MutationKind.ConditionToNotCondition
    |> TestApp.Assert_MutationKind_Has MutationKind.NotConditionToCondition
