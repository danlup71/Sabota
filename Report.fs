module Mutant1.Report

open Mutant1.MutationTypes

type MutationStatus =
    | Killed
    | Survived
    
type MutationOutcome =
    {
        Status: MutationStatus
        Interpretation: string
    }


type MutationResult =
    {
        MutationId: int
        MutationType: MutationKind
        File: string
        Range: FSharp.Compiler.Text.Range
        Original: string
        Mutated: string
        Outcome: MutationOutcome
    }

    
//      Mutation #17
//      Status: SURVIVED
//     
//      File: ( link ? )
//          SampleProject/Calculator.fs (42,13)
//     
//      Mutation:
//          EqualityToInequality =  ->  <>
//     
//      Code:
//          if x = y then ...
//             ^
//
//    Interpretation:
//        Potential missing assertion. Tests never verified the false branch.
//       
