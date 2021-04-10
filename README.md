# LiteDB.FSharp [![Build Status](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp.svg?branch=master)](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp) [![Nuget](https://img.shields.io/nuget/v/LiteDB.FSharp.svg?colorB=green)](https://www.nuget.org/packages/LiteDB.FSharp)

F# Support for [LiteDB](https://github.com/mbdavid/LiteDB) in .NET Core and full .NET Framework as well.

LiteDB.FSharp provides serialization utilities making it possible for LiteDB to understand F# types such as records, unions, maps etc. with support for type-safe query expression through F# quotations

### Usage
LiteDB.FSharp comes with a custom `BsonMapper` called `FSharpBsonMapper` that you would pass to a `LiteDatabase` instance during initialization:

```fsharp
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions

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

### Experiment: Single private case union
We must implement ISingleCaseInfo<'T> here 
Why: More disscussion [here](https://github.com/Zaid-Ajaj/LiteDB.FSharp/issues/55#issuecomment-817077584) 
```fsharp
type PhoneNumber = private PhoneNumber of int64
with 
    member x.Value =
        let (PhoneNumber v) = x
        v

    static member Create(phoneNumber: int64) = 
        match phoneNumber.ToString().Length with 
        | 11 -> PhoneNumber phoneNumber
        | _ -> failwithf "phone number %d 's length should be 11" phoneNumber

    interface ISingleCaseInfo<int64> with 
        member x.CaseInfo(_) =
            let (PhoneNumber v) = x
            v

```

### Inheritence 
`Item1` and `Item2` are inherited from `IItem`

we must register the type relations first globally
```fsharp
FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item1>()
FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item2>()
```
By conversion,
The inherited type must has mutable field for serializable and deserializable
```fsharp 
val mutable Id : int
```
*Note*:
Because [json converter](https://github.com/Zaid-Ajaj/LiteDB.FSharp/blob/master/LiteDB.FSharp/Json.fs) find inherited type by comparing the fields names from inherited type and database
```fsharp
let findType (jsonFields: seq<string>) =
    inheritedTypes |> Seq.maxBy (fun tp ->
        let fields = tp.GetFields() |> Seq.map (fun fd -> fd.Name)
        let fieldsLength = Seq.length fields
        (jsonFields |> Seq.filter(fun jsonField ->
            Seq.contains jsonField fields
        )
        |> Seq.length),-fieldsLength
    )        
```

This means that we should not implement the some interface with different fields
For example,we should not do below implementations
```fsharp 
type Item1 =

    val mutable Id : int
    val mutable Art : string
    val mutable Name : string
    val mutable Number : int

    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number

/// unexpected codes
type Item2 =

    val mutable Id2 : int
    val mutable Art2 : string
    val mutable Name2 : string
    val mutable Number2 : int

    interface IItem with 
        member this.Art = this.Art2
        member this.Id = this.Id2
        member this.Name = this.Name2
        member this.Number = this.Number2

/// expected codes
type Item2 =

    val mutable Id : int
    val mutable Art : string
    val mutable Name : string
    val mutable Number : int

    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number
```  

Full sample codes:
```fsharp
/// classlibray.fs
[<CLIMutable>]    
type EOrder=
  { Id: int
    Items : IItem list
    OrderNumRange: string }   


/// consumer.fs
type Item1 =
    /// val mutable will make field serializable and deserializable
    val mutable Id : int
    val mutable Art : string
    val mutable Name : string
    val mutable Number : int

    
    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number
    val mutable Barcode : string

    interface IBarcode with 
        member this.Barcode = this.Barcode    

    /// type constructor 
    new (id, art, name, number, barcode) =
        { Id = id; Art = art; Name = name; Number = number; Barcode = barcode }

type Item2 = 
    val mutable Id : int
    val mutable Art : string
    val mutable Name : string
    val mutable Number : int

    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number

    val mutable Size : int
    interface ISize with 
        member this.Size = this.Size 
    val mutable Color : string

    interface IColor with 
        member this.Color = this.Color 

    new (id, art, name, number, size, color) =
        { Id = id; Art = art; Name = name; Number = number; Size = size; Color = color }

FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item1>()
FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item2>()

let item1 = 
    Item1 ( 
        id = 0,
        art = "art",
        name = "name",
        number = 1000,
        barcode = "7254301" 
    )

let item2 = 
    Item2 ( 
        id = 0,
        art = "art",
        name = "name",
        number = 1000,
        color = "red" ,
        size = 39 
    )

let eorder = { Id = 1; Items = [item1;item2];  OrderNumRange = "" }

let queryedEOrder =
    db 
    |> LiteRepository.insertItem eorder
    |> LiteRepository.query<EOrder> 
    |> LiteQueryable.first

match queryedEOrder.Items with 
| [item1;item2] -> 
    match item1,item2 with 
    | :? IBarcode,:? IColor -> 
        pass()
    | _ -> fail()    
| _ -> fail()   
```
