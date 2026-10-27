module Tests._005_MutationTypesConsistency

open System
open Sabota.MutationTypes
open Sabota.MuationDefinitions
open Sabota.Report
open Xunit
open Microsoft.FSharp.Reflection
open TestApp

[<Fact>]
let ``01. all mutation kinds have a category mapping`` () =
    // Arrange
    Stub.AllMutationKinds
    // Act & Assert
    |> List.iter (fun kind ->
        getCategory kind
        |> TestApp.Assert_Category_InList  Stub.AllMutationCategories
        |> ignore)

[<Fact>]
let ``02. all mutation categories have at least one kind`` () =
    // Arrange 
    let coveredCategories = Stub.AllMutationKinds |> List.map getCategory |> List.distinct
    // Act & Assert
    Stub.AllMutationCategories
    |> List.iter (fun category ->
        category |> TestApp.Assert_Category_InList coveredCategories
        |> ignore)

[<Fact>]
let ``03. all mutation kinds have a definition`` () =
    // Arrange
    Stub.AllMutationKinds
    // Act & Assert
    |> List.iter (fun kind ->
        getDefinition kind
        |> TestApp.Assert_Mutation_Definition_NonEmpty
        |> ignore)

[<Fact>]
let ``04. all mutation kinds have a report label`` () =
    // Arrange
    Stub.AllMutationKinds
    // Act & Assert
    |> List.iter (fun kind ->
        mutationKindLabel kind
        |> TestApp.Assert_Mutation_Label_NonEmpty
        |> ignore)

[<Fact>]
let ``05. mutation kinds count matches type definition`` () =
    // Arrange
    let kinds = Stub.AllMutationKinds
    
    // Act
    let duCases = FSharpType.GetUnionCases(typeof<MutationKind>)
    let duCount = duCases.Length
    
    // Assert
    Assert.Equal(duCount, kinds.Length)

[<Fact>]
let ``06. mutation categories count matches type definition`` () =
    // Arrange
    let categories = Stub.AllMutationCategories
    
    // Act
    let duCases = FSharpType.GetUnionCases(typeof<MutationCategory>)
    let duCount = duCases.Length
    
    // Assert
    Assert.Equal(duCount, categories.Length)
