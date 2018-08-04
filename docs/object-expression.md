
## [Object expression](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions)
### Usage
```fsharp
Consider follow codes
///library.fs
type IColor =
    abstract member Color : string 

type IBarcode =
    abstract member Barcode : string 

type ISize =
    abstract member Size : int

type IItem = 
    abstract member Id : int
    abstract member Name : string
    abstract member Art : string
    abstract member Number : int

[<CLIMutable>]    
type EOrder=
  { Id: int
    Items : IItem list
    OrderNumRange: string } 
```

```fsharp
///consumer.fs
type Item1 =
    inherit IItem
    inherit IBarcode
let item1 = 
    { new Item1 with 
        member this.Id = 0
        member this.Art = "art"
        member this.Name = "name"
        member this.Number = 1000
        member this.Barcode = "7254301" }
let tp = item1.GetType()
FSharpJsonConverter.registerInheritedConverterType<IItem>(tp)
let eorder = { Id = 1; Items = [item1];  OrderNumRange = "" }
let queryedEOrder =
    db 
    |> LiteRepository.insertItem eorder
    |> LiteRepository.query<EOrder> 
    |> LiteQueryable.first
match queryedEOrder.Items with 
| [item] -> 
    let t = item :? IBarcode
    match item with 
    | :? IBarcode as item1 -> 
        printfn "%A" item1.Barcode
        pass()
    | _ -> fail()    
| _ -> fail()    
```
We serialize `item` as `Item1`
It means that in `library.fs` we treat it as `IItem` and `IBarcode`
while in `consumer.fs` we treat it as `Item1`

### Limitations
*Note:* This is a incompletion feature 
But sometimes is very *useful* for OO feature: interaction between library and consumer
**Limitation1:**
```fsharp
let item1 = 
    { new Item1 with 
        member this.Id = 0
        member this.Art = "art1"
        member this.Name = "name"
        member this.Number = 1000
        member this.Barcode = "7254301" }
let item2 = 
    { new Item1 with 
        member this.Id = 0
        member this.Art = "art2"
        member this.Name = "name"
        member this.Number = 1000
        member this.Barcode = "7254301" }
```
`item1` and `item2` has some different `Art`
The generated c# AST and code is

**item1**:

![image](https://user-images.githubusercontent.com/25994449/43620744-d2a636a0-9706-11e8-85e5-63867d2bc1dd.png)

**item2:**

![image](https://user-images.githubusercontent.com/25994449/43620858-3971e9e2-9707-11e8-87fe-27320624edab.png)

We cannot distinguish serialized data because the `art1`,`art2` are stored in **function** `IItem.Tests-Types-IItem-get_Art` but not **fileds**
You can also find the some issue in https://github.com/JamesNK/Newtonsoft.Json/issues/1451

**Limitation2**
```fsharp
let fields = List.replicate 10000 ["field"]
let item1 = 
    { new Item1 with 
        member this.Id = 0
        member this.Art = fields.[0]
        member this.Name = "name"
        member this.Number = 1000
        member this.Barcode = "7254301" }
```
The generated C# AST and code is

![image](https://user-images.githubusercontent.com/25994449/43620955-c0c5b270-9707-11e8-9749-64f89cdacd6f.png)
The serialized data contains `let fields = List.replicate 10000 ["field"]
` 
It is very large (about 10000 string size)

### The best way i found to solve this limitations now
```fsharp
let fields = List.replicate 10000 "field"
let id = 0
let art = fields.[0]
let name = "name"
let number = 1000
let barcode = "7254301"
let item1 = 
    { new Item1 with 
        member this.Id = id
        member this.Art = art
        member this.Name = name
        member this.Number = number
        member this.Barcode = barcode }
```
Only put last evalutated value to object expression then all datas are serialize **corrently**
The generated c# AST is

![image](https://user-images.githubusercontent.com/25994449/43620916-87066e9e-9707-11e8-807c-06f35bcb423d.png)


