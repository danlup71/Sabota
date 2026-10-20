module Mutant1.MutationDefinitions

open Mutant1.MutationCandidates

type MutationDefinition =
    {
        Original : string
        Replacement : string
    }

let getDefinition = function
    | EqualityToInequality ->
        { Original = "="
          Replacement = "<>" }

    | InequalityToEquality ->
        { Original = "<>"
          Replacement = "=" }

    | TrueToFalse ->
        { Original = "true"
          Replacement = "false" }

    | FalseToTrue ->
        { Original = "false"
          Replacement = "true" }
        
let enrich (candidate:MutationCandidate) =
    let definition =
        getDefinition candidate.Kind

    { candidate with Replacement = definition.Replacement; Original = definition.Original }
