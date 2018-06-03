let x = 10

let find<'a> (f: Quotations.Expr<'a -> bool>) = 
    printfn "%A" f


type Record = { Id: int; Name: string }

let value = 10

find<Record> <@ fun record -> record.Id = value @>

let myId = 10
find<Record> <@ fun record -> record.Name = "Hello" || record.Id = myId @>