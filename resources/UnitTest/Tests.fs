module Tests

open Xunit
open Samples

[<Fact>]
let ``Compare Equals test`` () =
    
    let equal = Equality.compareEqual 2 2
    Assert.True(equal)
    
[<Fact>]
let ``Compare Different test`` () =
    
    let equal = Equality.compareEqual 2 1
    Assert.False(equal)
    
[<Fact>]
let ``Compare collection`` () =
        
    Assert.Equal(2,Collection.returnCollection.Length)        