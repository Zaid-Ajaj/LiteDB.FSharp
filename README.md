# LiteDB.FSharp [![Build Status](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp.svg?branch=master)](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp) [![Nuget](https://img.shields.io/nuget/v/LiteDB.FSharp.svg?colorB=green)](https://www.nuget.org/packages/LiteDB.FSharp)

F# Support for [LiteDB](https://github.com/mbdavid/LiteDB) in .NET Core and full .NET Framework as well.

LiteDB.FSharp provides serialization utilities making it possible for LiteDB to understand F# types such as records, unions, maps etc. with support for type-safe query expression through F# quotations

### Usage
LiteDB.FSharp comes with a custom `BsonMapper` called `FSharpBsonMapper` that you would pass to a `LiteDatabase` instance during initialization:

```fsharp
open LiteDB
open LiteDB.FSharp

let mapper = FSharpBsonMapper()
use db = new LiteDatabase("simple.db", mapper)
```
LiteDB.FSharp is made mainly to work with records as representations of the persisted documents. The library *requires* that records have a primary key called `Id` or `id`. This field is then mapped to `_id` when converted to a bson document for indexing.

```fsharp
type Genre = Rock | Pop | Metal

type Album = {
    Id: int
    Name: string
    DateReleased: DateTime
    Genre: Genre
}
```
Get a typed collection from the database:
```fsharp
let albums = db.GetCollection<Album>("albums")
```
### Insert documents
```fsharp
let metallica = 
    { Id = 1; 
      Name = "Metallica";
      Genre = Metal;
      DateReleased = DateTime(1991, 8, 12) }

albums.Insert(metallica)
```
### Query one document by Id
```fsharp
// result : Album
let result = albums.findOne <@ fun album -> album.Id = 1 @> 

// OR
let id = BsonValue(1)
// result : Album
let result = albums.FindById(id)
```
### Query many documents depending on the value of a field
```fsharp
// metallicaAlbums : Seq<Album>
let metallicaAlbums = albums.findMany <@ fun album -> album.Name = "Metallica" @>
// OR
let name = BsonValue("Metallica")
let query = Query.EQ("Name", name)
// metallicaAlbums : Seq<Album>
let metallicaAlbums = albums.Find(query)
```
### Query documents by value of discriminated union
```fsharp
// find all albums where Genre = Rock
// rockAlbums : Seq<Album>
let rockAlbums = albums.findMany <@ fun album -> album.Genre = Rock @>

// OR 

let genre = BsonValue("Rock")
let query = Query.EQ("Genre", genre)
// rockAlbums : Seq<Album>
let rockAlbums = albums.Find(query)
```
### Query documents between or time intervals
```fsharp
// find all albums released last year
let now = DateTime.Now
let dateFrom = DateTime(now.Year - 1, 01, 01) |> BsonValue
let dateTo = DateTime(now.Year, 01, 01) |> BsonValue
let query = Query.Between("DateReleased", dateFrom, dateTo)
// albumsLastYear : Seq<Album>
let albumsLastYear = albums.Find(query)
```
### Customized Full Search using quoted expressions
```fs
// Filtering albums released a year divisble by 5
// filtered : Seq<Album>
let filtered = 
    albums.fullSearch 
        <@ fun album -> album.DateReleased @> 
        (fun dateReleased -> dateReleased.Year % 5 = 0)
```

### Customized Full Search using Query.Where
The function `Query.Where` expects a field name and a filter function of type `BsonValue -> bool`. You can deserialize the `BsonValue` using `Bson.deserializeField<'t>` where `'t` is the type of the serialized value. 

```fsharp
// Filtering albums released a year divisble by 5
let searchQuery = 
    Query.Where("DateReleased", fun bsonValue ->
        // dateReleased : DateTime
        let dateReleased = Bson.deserializeField<DateTime> bsonValue
        let year = dateReleased.Year
        year % 5 = 0
    )

let searchResult = albums.Find(searchQuery)
```
### Query.Where: Filtering documents by matching with values of a nested DU
```fsharp
type Shape = 
    | Circle of float
    | Rect of float * float
    | Composite of Shape list

type RecordWithShape = { Id: int; Shape: Shape }

let records = db.GetCollection<RecordWithShape>("shapes")

let shape = 
    Composite [ 
      Circle 2.0;
      Composite [ Circle 4.0; Rect(2.0, 5.0) ]
    ]

let record = { Id = 1; Shape = shape }
records.Insert(record) |> ignore

let searchQuery = 
    Query.Where("Shape", fun bsonValue -> 
        let shapeValue = Bson.deserializeField<Shape> bsonValue
        match shapeValue with
        | Composite [ Circle 2.0; other ] -> true
        | otherwise -> false
    )
records.Find(searchQuery)
|> Seq.length
|> function 
    | 1 -> pass() // passed!
    | n -> fail()
```
### Id auto-incremented
 Add CLIMutableAttribute to record type and set Id 0
 ```fsharp
[<CLIMutable>]
 type Album = {
    Id: int
    Name: string
    DateReleased: DateTime
    Genre: Genre
}
let metallica = 
    { Id = 0; 
      Name = "Metallica";
      Genre = Metal;
      DateReleased = DateTime(1991, 8, 12) }
 ```    

### DbRef
just as https://github.com/mbdavid/LiteDB/wiki/DbRef

```fsharp
open LiteDB.FSharp.Linq

[<CLIMutable>]
type Company=
  { Id : int
    Name : string}   

[<CLIMutable>]    
type Order=
  { Id :int
    Company :Company }
    
let mapper = FSharpBsonMapper()
mapper.DbRef<Order,_>(fun c -> c.Company)

```

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


