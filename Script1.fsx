open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns


let x = 10

let find<'a> (f: Quotations.Expr<'a -> bool>) = 
    printfn "%A" f


type Record = { Id: int; Name: string; HasFamily: bool }

let value = 10

find<Record> <@ fun record -> record.Id = value @>

let myId = 10

let x = <@ fun record -> not record.HasFamily @>
match x with 
| Lambda(_, Call(_, meth, [PropertyGet(_, propInfo, [])])) -> Some meth, propInfo 
| _ -> None 