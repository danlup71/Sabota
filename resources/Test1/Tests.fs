module Tests

open System
open Sample1.Sample3
open Xunit

[<Fact>]
let ``Compare Equals test`` () =
    
    let equal = compareEqual 2 2
    Assert.True(equal)
    
[<Fact>]
let ``Compare Different test`` () =
    
    let equal = compareEqual 2 1
    Assert.False(equal)    