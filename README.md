# LiteDB.FSharp [![Build Status](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp.svg?branch=master)](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp) [![Nuget](https://img.shields.io/nuget/v/LiteDB.FSharp.svg?colorB=green)](https://www.nuget.org/packages/LiteDB.FSharp)

F# Support for [LiteDB](https://github.com/mbdavid/LiteDB) in .NET Core and full .NET Framework as well.

LiteDB.FSharp provides serialization utilities making it possible for LiteDB to understand F# types such as records, unions, maps etc. 

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
let id = BsonValue(1)
// result : Album
let result = albums.FindById(id)
```
### Query many documents depending on the value of a field
```fsharp
// Find all albums where Album["Name"] = "Metallica"
let name = BsonValue("Metallica")
let query = Query.EQ("Name", name)
// metallicaAlbums : Seq<Album>
let metallicaAlbums = albums.Find(query)
```
### Query documents by value of discriminated union
```fsharp
// find all albums where Genre = Rock
let genre = BsonValue("Rock")
let query = Query.EQ("Genre", genre)
// metallicaAlbums : Seq<Album>
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
### Customized search using Query.Where
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

let defaultCompany=
      { Id = 0
        Name = "test"}  
let defaultOrder =
  { Id = 0
    Company = defaultCompany}
File.Delete("simple.db")|>ignore
let mapper = FSharpBsonMapper()
//Add DbRef Fluently 
mapper.Entity<Order>().DbRef(convertExpr <@ fun c -> c.Company @>)
use db = new LiteRepository("simple.db",mapper)

db.Insert(defaultCompany)
db.Insert(defaultOrder)

// Id auto-incremented So Id is1
db.Update({ defaultCompany with Name="Hello"; Id = 1 })

let ordersWithCompanies = db.Query<Order>().Include(convertExpr <@ fun c -> c.Company @>)
let companyName = ordersWithCompanies.FirstOrDefault().Company.Name
match companyName with 
| "Hello" -> pass()
| otherwise -> fail()
```

