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

```csharp
		// Token: 0x0200000A RID: 10
		[CompilationMapping(6)]
		[Serializable]
		[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
		internal sealed class item1@64-1 : ObjectExpression.Item1
		{
			// Token: 0x06000013 RID: 19 RVA: 0x000022F0 File Offset: 0x000004F0
			public item1@64-1() : this()
			{
			}

			// Token: 0x06000014 RID: 20 RVA: 0x000022FC File Offset: 0x000004FC
			int Types.IItem.Tests-Types-IItem-get_Id()
			{
				return 0;
			}

			// Token: 0x06000015 RID: 21 RVA: 0x00002300 File Offset: 0x00000500
			string Types.IItem.Tests-Types-IItem-get_Art()
			{
				return "art1";
			}

			// Token: 0x06000016 RID: 22 RVA: 0x00002308 File Offset: 0x00000508
			string Types.IItem.Tests-Types-IItem-get_Name()
			{
				return "name";
			}

			// Token: 0x06000017 RID: 23 RVA: 0x00002310 File Offset: 0x00000510
			int Types.IItem.Tests-Types-IItem-get_Number()
			{
				return 1000;
			}

			// Token: 0x06000018 RID: 24 RVA: 0x00002318 File Offset: 0x00000518
			string Types.IBarcode.Tests-Types-IBarcode-get_Barcode()
			{
				return "7254301";
			}
		}
```
**item2:**
![image](https://user-images.githubusercontent.com/25994449/43620858-3971e9e2-9707-11e8-87fe-27320624edab.png)
```csharp
		// Token: 0x0200000B RID: 11
		[CompilationMapping(6)]
		[Serializable]
		[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
		internal sealed class item2@71 : ObjectExpression.Item1
		{
			// Token: 0x06000019 RID: 25 RVA: 0x00002320 File Offset: 0x00000520
			public item2@71() : this()
			{
			}

			// Token: 0x0600001A RID: 26 RVA: 0x0000232C File Offset: 0x0000052C
			int Types.IItem.Tests-Types-IItem-get_Id()
			{
				return 0;
			}

			// Token: 0x0600001B RID: 27 RVA: 0x00002330 File Offset: 0x00000530
			string Types.IItem.Tests-Types-IItem-get_Art()
			{
				return "art2";
			}

			// Token: 0x0600001C RID: 28 RVA: 0x00002338 File Offset: 0x00000538
			string Types.IItem.Tests-Types-IItem-get_Name()
			{
				return "name";
			}

			// Token: 0x0600001D RID: 29 RVA: 0x00002340 File Offset: 0x00000540
			int Types.IItem.Tests-Types-IItem-get_Number()
			{
				return 1000;
			}

			// Token: 0x0600001E RID: 30 RVA: 0x00002348 File Offset: 0x00000548
			string Types.IBarcode.Tests-Types-IBarcode-get_Barcode()
			{
				return "7254301";
			}
		}
```
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

```csharp
		// Token: 0x02000015 RID: 21
		[Serializable]
		internal sealed class objectExpressionTests@149-9 : FSharpFunc<LiteRepository, Unit>
		{
			// Token: 0x06000040 RID: 64 RVA: 0x000026F4 File Offset: 0x000008F4
			[CompilerGenerated, DebuggerNonUserCode]
			internal objectExpressionTests@149-9()
			{
			}

			// Token: 0x06000041 RID: 65 RVA: 0x000026FC File Offset: 0x000008FC
			public override Unit Invoke(LiteRepository db)
			{
				FSharpList<string> fields = ListModule.Replicate<string>(10000, "field");
				ObjectExpression.Item2 item = new ObjectExpression.item2@152-3(fields);
				Type type = item.GetType();
				FSharpJsonConverterModule.registerInheritedConverterType<Types.IItem>(type);
				Types.EOrder item2 = new Types.EOrder(1, FSharpList<Types.IItem>.Cons(item, FSharpList<Types.IItem>.get_Empty()), "");
				Types.EOrder eOrder = Extensions.LiteQueryable.first<Types.EOrder>(Extensions.LiteRepository.query<Types.EOrder>(Extensions.LiteRepository.insertItem<Types.EOrder>(item2, db)));
				FSharpList<Types.IItem> items@ = eOrder.Items@;
				if (items@.get_TailOrNull() != null)
				{
					FSharpList<Types.IItem> fSharpList = items@;
					if (fSharpList.get_TailOrNull().get_TailOrNull() == null)
					{
						Types.IItem headOrDefault = fSharpList.get_HeadOrDefault();
						object obj = headOrDefault;
						bool arg_A7_0;
						if (obj is Types.IColor)
						{
							object obj2 = headOrDefault;
							arg_A7_0 = (obj2 is Types.ISize);
						}
						else
						{
							arg_A7_0 = false;
						}
						if (arg_A7_0)
						{
							ObjectExpression.pass();
							return null;
						}
						ObjectExpression.fail();
						return null;
					}
				}
				ObjectExpression.fail();
				return null;
			}
		}
```
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
The generated AST and c# code is 
![image](https://user-images.githubusercontent.com/25994449/43620916-87066e9e-9707-11e8-807c-06f35bcb423d.png)
```csharp
		// Token: 0x0200000A RID: 10
		[CompilationMapping(6)]
		[Serializable]
		[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
		internal sealed class item1@70-1 : ObjectExpression.Item1
		{
			// Token: 0x06000013 RID: 19 RVA: 0x000022F0 File Offset: 0x000004F0
			public item1@70-1(int id, string art, string name, int number, string barcode) : this()
			{
			}

			// Token: 0x06000014 RID: 20 RVA: 0x00002320 File Offset: 0x00000520
			int Types.IItem.Tests-Types-IItem-get_Id()
			{
				return this.id;
			}

			// Token: 0x06000015 RID: 21 RVA: 0x00002328 File Offset: 0x00000528
			string Types.IItem.Tests-Types-IItem-get_Art()
			{
				return this.art;
			}

			// Token: 0x06000016 RID: 22 RVA: 0x00002330 File Offset: 0x00000530
			string Types.IItem.Tests-Types-IItem-get_Name()
			{
				return this.name;
			}

			// Token: 0x06000017 RID: 23 RVA: 0x00002338 File Offset: 0x00000538
			int Types.IItem.Tests-Types-IItem-get_Number()
			{
				return this.number;
			}

			// Token: 0x06000018 RID: 24 RVA: 0x00002340 File Offset: 0x00000540
			string Types.IBarcode.Tests-Types-IBarcode-get_Barcode()
			{
				return this.barcode;
			}

			// Token: 0x04000004 RID: 4
			public int id = id;

			// Token: 0x04000005 RID: 5
			public string art = art;

			// Token: 0x04000006 RID: 6
			public string name = name;

			// Token: 0x04000007 RID: 7
			public int number = number;

			// Token: 0x04000008 RID: 8
			public string barcode = barcode;
```
