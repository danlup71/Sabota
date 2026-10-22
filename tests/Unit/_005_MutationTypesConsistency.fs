module Tests._005_MutationTypesConsistency

open System
open Sabota.MutationTypes
open Sabota.MuationDefinitions
open Sabota.Report
open Xunit
open Microsoft.FSharp.Reflection

// Enumerate all implemented mutation kinds explicitly
// If a new kind is added to MutationTypes but forgotten here, the count test will fail
let getAllMutationKinds () : MutationKind list =
    [ EqualityToInequality
      InequalityToEquality
      TrueToFalse
      FalseToTrue
      GreaterToGreaterOrEqual
      GreaterOrEqualToGreater
      LessToLessOrEqual
      LessOrEqualToLess
      GreaterToLess
      GreaterToLessOrEqual
      LessToGreater
      LessToGreaterOrEqual
      PlusToMinus
      MinusToPlus
      TimesToDivide
      DivideToTimes
      AndToOr
      OrToAnd
      SomeToNone
      ReturnListValueToEmpty ]

// Enumerate all implemented mutation categories explicitly
// If a new category is added to MutationTypes but forgotten here, the count test will fail
let getAllMutationCategories () : MutationCategory list =
    [ Equality
      RelationalBoundary
      RelationalReversal
      Boolean
      Arithmetic
      Conditional
      Option
      ReturnListValue ]

[<Fact>]
let ``all mutation kinds have a category mapping`` () =
    let kinds = getAllMutationKinds ()
    let categories = getAllMutationCategories ()
    
    let missing = 
        kinds 
        |> List.filter (fun kind ->
            try
                let category = getCategory kind
                not (List.contains category categories)
            with _ -> true)
    
    Assert.Empty missing

[<Fact>]
let ``all mutation categories have at least one kind`` () =
    let kinds = getAllMutationKinds ()
    let categories = getAllMutationCategories ()
    
    let coveredCategories = kinds |> List.map getCategory |> List.distinct
    
    let missing = categories |> List.filter (fun cat -> not (List.contains cat coveredCategories))
    
    Assert.Empty missing

[<Fact>]
let ``all mutation kinds have a definition`` () =
    let kinds = getAllMutationKinds ()
    
    let missing = 
        kinds 
        |> List.filter (fun kind ->
            try
                let def = getDefinition kind
                String.IsNullOrEmpty def.Original || String.IsNullOrEmpty def.Replacement
            with _ -> true)
    
    Assert.Empty missing

[<Fact>]
let ``all mutation kinds have a report label`` () =
    let kinds = getAllMutationKinds ()
    
    let missing = 
        kinds 
        |> List.filter (fun kind ->
            try
                let label = mutationKindLabel kind
                String.IsNullOrEmpty label
            with _ -> true)
    
    Assert.Empty missing

[<Fact>]
let ``mutation kinds count matches type definition`` () =
    let kinds = getAllMutationKinds ()
    
    // Compare against actual DU cases so future additions from config won't break this test
    let duCases = FSharpType.GetUnionCases(typeof<MutationKind>)
    let duCount = duCases.Length
    
    Assert.Equal(duCount, kinds.Length)

[<Fact>]
let ``mutation categories count matches type definition`` () =
    let categories = getAllMutationCategories ()
    
    // Compare against actual DU cases so future additions from config won't break this test
    let duCases = FSharpType.GetUnionCases(typeof<MutationCategory>)
    let duCount = duCases.Length
    
    Assert.Equal(duCount, categories.Length)
