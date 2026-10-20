module Mutant1.MutationCandidates

open Mutant1.MutationTypes

type MutationId = int

type MutationCandidate =
    {
        Id : MutationId
        Kind : MutationKind
        File: string
        Range : FSharp.Compiler.Text.Range
        Original : string
        Replacement : string
    }