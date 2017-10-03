# LiteDB.FSharp [![Build Status](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp.svg?branch=master)](https://travis-ci.org/Zaid-Ajaj/LiteDB.FSharp) [![Nuget](https://img.shields.io/nuget/v/LiteDB.FSharp.svg?colorB=green)](https://www.nuget.org/packages/LiteDB.FSharp)

F# Support for [LiteDB](https://github.com/mbdavid/LiteDB) in .NET Core

LiteDB.FSharp provides serialization utilities making it possible for LiteDB to understand F# types such as records, unions, maps etc. 

> The library is experiemental and not production-ready. The Api is subject to change.

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
### Finally, example of fully customized search by nested discriminated unions
```fsharp
type Shape = 
    | Circle of float
    | Rect of float * float
    | Composite of Shape list

type RecordWithShape = { Id: int; Shape: Shape }

testCase "Full custom search works by BsonValue deserialization" <| fun _ ->
  useDatabase <| fun db ->
    let records = db.GetCollection<RecordWithShape> "Shapes"                       
    let shape = 
        Composite [ 
          Circle 2.0;
          Composite [ Circle 4.0; Rect(2.0, 5.0) ]
        ]
   let record = { Id = 1; Shape = shape }

    records.Insert(record) |> ignore
    let searchQuery = 
        Query.Where("Shape", fun bsonValue -> 
            // Bson.derserializa<'t> won't work because that is 
            // only for documents,
            // that's why use Bson.deserializeField<'t> because you are filtering BsonValues
            let shapeValue = Bson.deserializeField<Shape> bsonValue
            shapeValue = shape
        )

    records.Find(searchQuery)
    |> Seq.length
    |> function 
        | 1 -> pass() // passed!
        | n -> fail()
```