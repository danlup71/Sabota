module Sabota.MultiError

type MultiError =
    | GenericError of string
    | ParseError of string
    | MissingTestProject of string
    | MissingSolutionRoot of string