module Samples.ControlFlow

let validatePositive x =
    if x > 0 then
        "valid"
    else
        "invalid"

let validateNegative x =
    if not (x > 0) then
        "negative"
    else
        "positive"
