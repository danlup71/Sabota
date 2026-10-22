module Sabota.MutationCandidates

open Sabota.MutationTypes

type MutationId = int

type MutationCandidate =
    {
        Id : MutationId
        Kind : MutationKind
        File: string
        Range : FSharp.Compiler.Text.Range
        Original : string
        Replacement : string
        SurvivorInterpretation : string
    }