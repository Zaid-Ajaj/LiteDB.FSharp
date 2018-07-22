// filtering functions have type: int -> int option
let greaterThanFive (n: int) = 
    if n > 5 
    then Some n
    else None 

let lessThanTen (n: int) = 
    if n < 10 
    then Some n
    else None 

let equalsEight (n: int) = 
    if n = 8 
    then Some n 
    else None 

let timesTwo input =
    match input with 
    | Some n -> Some (n * 2)
    | None -> None 

let greaterThanFiveAndLessThanTen (n: int) =
    match greaterThanFive n with 
    | None -> None // did not pass the first predicate 
    | Some firstResult -> 
        match lessThanTen firstResult with 
        | None -> None // did not pass the second predicate
        | Some secondResult -> Some secondResult 
    