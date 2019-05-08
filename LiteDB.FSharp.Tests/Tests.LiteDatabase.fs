module Tests.LiteDatabase

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions

open Tests.Types
open Expecto.Logging
open LiteDB
open System.Collections.Generic
open LiteDB
open LiteDB

type MaritalStatus = Single | Married

type PersonDocument = {
    Id: int
    Name: string
    DateAdded: DateTime
    Age: int
    Status: MaritalStatus
}

type RecordWithBoolean = { Id: int; HasValue: bool }

type RecordWithStr = { Id : int; Name: string }

type NestedRecord = { Inner : PersonDocument }

type RecordWithOptionalDate = {
    Id : int
    Released : Option<DateTime>
}

type MutableBoolean = {
    Id: int
    mutable MutableBoolean : bool
}

type RecordWithOptionalRecord = {
    Id : int
    Record : Option<RecordWithStr>
}

type RecOptGuid = {
    Id: int 
    OtherId: Option<Guid>
}

let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"

let useDatabase (f: LiteDatabase -> unit) = 
    let mapper = FSharpBsonMapper()
    use memoryStream = new MemoryStream()
    use db = new LiteDatabase(memoryStream, mapper)
    f db

let liteDatabaseUsage = 
    testList "LiteDatabase usage" [

        testCase "Persisting documents with mutable fields should work" <| fun _ -> 
            useDatabase <| fun db ->
                let records = db.GetCollection<MutableBoolean>("booleans")
                records.Insert { Id = 1; MutableBoolean = false } |> ignore 
                records.FindAll() 
                |> Seq.toList
                |> function 
                    | [ { Id = 1; MutableBoolean = false } ] -> pass()
                    | otherwise -> fail()

        testCase "Uninitialized values are populated with default values" <| fun _ ->
            useDatabase <| fun db ->
                let records = db.GetCollection<BsonDocument>("documents")
                let initialDoc = BsonDocument()
                initialDoc.Add(KeyValuePair("_id", BsonValue(1)))
                // adding { _id: 1 }
                records.Insert initialDoc |> ignore
                // reading { Id: int; HasValue: bool } where HasValue should be deserialized to false by default
                let typedRecords = db.GetCollection<RecordWithBoolean>("documents")
                let firstRec = typedRecords.FindAll() |> Seq.head
                Expect.equal 1 firstRec.Id "Deserialized ID is correct"
                Expect.equal false firstRec.HasValue "Deserialized boolean has default value of false"

        testCase "Inserting typed document then reading it as BsonDocument should work" <| fun _ ->
            useDatabase <| fun db ->
                let typedRecords = db.GetCollection<RecordWithBoolean>("booleans") 
                typedRecords.Insert { Id = 1; HasValue = true } |> ignore 
                
                let documents = db.GetCollection<BsonDocument>("booleans")
                let firstDoc = documents.FindAll() |> Seq.head  
                Expect.equal (Bson.readInt "_id" firstDoc) 1 "Id of BsonDocument is 1"
                Expect.equal (Bson.readBool "HasValue" firstDoc) true "Id of BsonDocument is 1"

        testCase "Inserting and FindById work" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; DateAdded = time; Status = Single }
                people.Insert(person) |> ignore
                let p = people.FindById(BsonValue(1))
                match p with
                | { Id = 1; Name = "Mike"; Age = 10; Status = Single; DateAdded = x } ->
                    Expect.equal 2017 x.Year "Year is mapped correctly"
                    Expect.equal 10 x.Month "Month is mapped correctly"
                    Expect.equal 15 x.Day "Day is mapped correctly"
                | otherwise -> fail()

        testCase "Inserting and findOne with quoted expressions work" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; DateAdded = time; Status = Single }
                people.Insert(person) |> ignore
                let p = people.findOne <@ fun person -> person.Id = 1 @>
                match p with
                | { Id = 1; Name = "Mike"; Age = 10; Status = Single; DateAdded = x } ->
                    Expect.equal 2017 x.Year "Year is mapped correctly"
                    Expect.equal 10 x.Month "Month is mapped correctly"
                    Expect.equal 15 x.Day "Day is mapped correctly"
                | otherwise -> fail()

        testCase "Query expression with literal boolean value is supported" <| fun _ ->
            useDatabase <| fun db ->
                let docs = db.GetCollection<BsonDocument>("docs")
                let doc = BsonDocument()
                doc.Add(KeyValuePair("_id", BsonValue(42)))
                docs.Insert doc |> ignore 
                let inserted = docs.findOne(fun doc -> true) 
                Expect.equal 1 inserted.Keys.Count "Doc has one key (_id)"
                Expect.equal 42 (Bson.readInt "_id" inserted) "_id = 42"
                
        testCase "Documents with optional DateTime = Some can be used" <| fun _ ->
            useDatabase <| fun db -> 
                let docs = db.GetCollection<RecordWithOptionalDate>()
                docs.Insert { Id = 1; Released = Some DateTime.Now } |> ignore 
                docs.FindAll()
                |> Seq.tryHead 
                |> function 
                    | None -> fail() 
                    | Some doc -> 
                        match doc.Id, doc.Released with 
                        | 1, Some date -> pass()
                        | _ -> fail() 

        testCase "Documents with optional Guid = Some can be used" <| fun _ ->
            useDatabase <| fun db -> 
                let docs = db.GetCollection<RecOptGuid>()
                docs.Insert { Id = 1; OtherId = Some (Guid.NewGuid()) } |> ignore 
                docs.FindAll()
                |> Seq.tryHead 
                |> function 
                    | None -> fail() 
                    | Some doc -> 
                        match doc.Id, doc.OtherId with 
                        | 1, Some guid -> pass()
                        | _ -> fail() 

        testCase "Documents with optional Guid = None can be used" <| fun _ ->
            useDatabase <| fun db -> 
                let docs = db.GetCollection<RecOptGuid>()
                docs.Insert { Id = 1; OtherId = None } |> ignore 
                docs.FindAll()
                |> Seq.tryHead 
                |> function 
                    | None -> fail() 
                    | Some doc -> 
                        match doc.Id, doc.OtherId with 
                        | 1, None -> pass()
                        | _ -> fail() 

        testCase "Documents with optional DateTime = None can be used" <| fun _ ->
            useDatabase <| fun db -> 
                let docs = db.GetCollection<RecordWithOptionalDate>()
                docs.Insert { Id = 1; Released = None } |> ignore 
                docs.FindAll()
                |> Seq.tryHead 
                |> function 
                    | None -> fail() 
                    | Some doc -> 
                        match doc.Id, doc.Released with 
                        | 1, None -> pass()
                        | _ -> fail() 

        testCase "Documents with optional Record = Some can be used" <| fun _ ->
            useDatabase <| fun db -> 
                let docs = db.GetCollection<RecordWithOptionalRecord>()
                docs.Insert { Id = 1; Record = Some {Id = 1; Name = "Name"} } |> ignore 
                docs.FindAll()
                |> Seq.tryHead 
                |> function 
                    | None -> fail() 
                    | Some doc -> 
                        match doc.Id, doc.Record with 
                        | 1, Some {Id = 1; Name = "Name"} -> pass()
                        | _ -> fail()             

        testCase "TryFindById extension works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; DateAdded = time; Status = Single }
                people.Insert(person) |> ignore
                // try find an existing person
                match people.TryFindById(BsonValue(1)) with
                | Some person -> pass() 
                | None -> fail()
                // try find a non-existing person
                match people.TryFindById(BsonValue(500)) with
                | Some person -> fail()
                | None -> pass()

        testCase "Search by Query.Between integer field works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore
                let query = Query.And(Query.GT("Age", BsonValue(5)), Query.LT("Age", BsonValue(15)))
                people.Find(query)
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search by compound query expression works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore
                people.findMany <@ fun person -> person.Age > 5 && person.Age < 15 @> 
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search ID by compound query expression works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore

                people.findMany <@ fun person -> person.Id > 0 @> 
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Extracting values from getter works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let mike = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(mike) |> ignore
                people.findMany <@ fun person -> person.Name = mike.Name @> 
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()


        testCase "Extracting values from nested getter works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let mike = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                let nestedRecord = { Inner = mike }
                people.Insert(mike) |> ignore
                people.findMany <@ fun person -> person.Name = nestedRecord.Inner.Name @> 
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "TryFind extension method works" <| fun _ ->
            useDatabase <| fun db ->
               let people = db.GetCollection<PersonDocument>("people")
               let time = DateTime(2017, 10, 15)
               let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
               people.Insert(person) |> ignore 
               match people.TryFind(Query.EQ("Name", BsonValue("Mike"))) with
               | Some insertedPerson when insertedPerson = person ->
                    match people.TryFind(Query.EQ("Name", BsonValue("John"))) with
                    | None -> pass()
                    | otherwise -> fail()
               | otherwise -> fail()

        testCase "tryFindOne works" <| fun _ ->
            useDatabase <| fun db ->
               let people = db.GetCollection<PersonDocument>("people")
               let time = DateTime(2017, 10, 15)
               let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
               people.Insert(person) |> ignore 
               match people.tryFindOne <@ fun person -> person.Name = "Mike" @> with
               | Some insertedPerson when insertedPerson = person ->
                    match people.tryFindOne <@ fun person -> person.Name = "John" @> with
                    | None -> pass()
                    | otherwise -> fail()
               | otherwise -> fail()

        testCase "Search by Exact Name works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore
                let query = Query.EQ("Name", BsonValue("Mike"))
                people.Find(query)
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search by Exact Age works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore
                let query = Query.EQ("Age", BsonValue(10))
                people.Find(query)
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search by Exact Age works with expressions" <| fun _ ->
           useDatabase <| fun db ->
               let people = db.GetCollection<PersonDocument>("people")
               let time = DateTime(2017, 10, 15)
               let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
               people.Insert(person) |> ignore
               
               people.findMany <@ fun person -> person.Age = 10 @>
               |> Seq.length 
               |> function | 1 -> pass()
                           | n -> fail()

        testCase "Search by Exact Age works with auto-quoted expressions" <| fun _ ->
           useDatabase <| fun db ->
               let people = db.GetCollection<PersonDocument>("people")
               let time = DateTime(2017, 10, 15)
               let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
               people.Insert(person) |> ignore
               
               people.findMany (fun person -> person.Age = 10)
               |> Seq.length 
               |> function | 1 -> pass()
                           | n -> fail()

        testCase "String.IsNullOrWhitespace works in query expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithStr>()
                values.Insert({ Id = 1; Name = "" }) |> ignore 

                values.tryFindOne <@ fun value -> String.IsNullOrWhiteSpace value.Name @> 
                |> function 
                    | Some { Id = 1; Name = "" } -> pass() 
                    | _ -> fail()

        testCase "String.IsNullOrEmpty works in query expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithStr>()
                values.Insert({ Id = 1; Name = "" }) |> ignore 

                values.tryFindOne <@ fun value -> String.IsNullOrEmpty value.Name @> 
                |> function 
                    | Some { Id = 1; Name = "" } -> pass() 
                    | _ -> fail()

        testCase "String.IsNullOrEmpty works in auto-quoted query expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithStr>()
                values.Insert({ Id = 1; Name = "" }) |> ignore 

                values.tryFindOne (fun value -> String.IsNullOrEmpty value.Name)
                |> function 
                    | Some { Id = 1; Name = "" } -> pass() 
                    | _ -> fail()

        testCase "String.Contains works in query expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithStr>()
                values.Insert({ Id = 1; Name = "Friday" }) |> ignore 

                values.tryFindOne <@ fun value -> value.Name.Contains("Fri") @> 
                |> function 
                    | Some { Id = 1; Name = "Friday" } -> pass() 
                    | _ -> fail()

                values.tryFindOne <@ fun value -> value.Name.Contains("Hello") @>
                |> function
                    | None -> pass()
                    | _ -> fail()

        testCase "String.Contains works in conposite query expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithStr>()
                values.Insert({ Id = 1; Name = "Friday" }) |> ignore 

                values.tryFindOne <@ fun value -> value.Name.Contains("Fri") && value.Name.Contains("Hello") @> 
                |> function 
                    | None -> pass() 
                    | _ -> fail()

                values.findMany <@ fun value -> value.Name.Contains("Fri") || value.Name.Contains("Hello")  @>
                |> Seq.length
                |> function
                    | 1 -> pass()
                    | _ -> fail() 

        testCase "String.Contains works in conposite auto-quoted query expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithStr>()
                values.Insert({ Id = 1; Name = "Friday" }) |> ignore 

                values.tryFindOne (fun value -> value.Name.Contains("Fri") && value.Name.Contains("Hello")) 
                |> function 
                    | None -> pass() 
                    | _ -> fail()

                values.findMany (fun value -> value.Name.Contains("Fri") || value.Name.Contains("Hello"))
                |> Seq.length
                |> function
                    | 1 -> pass()
                    | _ -> fail()                     

        testCase "Search between time intervals using Query.And" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore

                let dateFrom = DateTime(2017, 01, 01) |> BsonValue
                let dateTo = DateTime(2018, 01, 01) |> BsonValue
                let query = Query.And(Query.GT("DateAdded", dateFrom), Query.LT("DateAdded", dateTo))
                people.Find(query)
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search between time intervals using quoted expressions" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore

                people.findMany <@ fun person -> person.DateAdded > DateTime(2017, 01, 01) 
                                              && person.DateAdded < DateTime(2018, 01, 01) @>
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search between time intervals using auto-quoted expressions" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore

                people.findMany (fun person -> person.DateAdded > DateTime(2017, 01, 01) 
                                            && person.DateAdded < DateTime(2018, 01, 01))
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()

        testCase "Search between time intervals using Query.Between" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore

                let dateFrom = DateTime(2017, 01, 01) |> BsonValue
                let dateTo = DateTime(2018, 01, 01) |> BsonValue
                let query = Query.Between("DateAdded", dateFrom, dateTo)
                people.Find(query)
                |> Seq.length 
                |> function | 1 -> pass()
                            | n -> fail()   

        testCase "Search by using expression on boolean properties" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithBoolean>()
                values.Insert({ Id = 1; HasValue = true }) |> ignore 
                let foundItem = values.tryFindOne <@ fun item -> item.HasValue @>  
                match foundItem with 
                | Some value -> pass()
                | None -> fail() 

        testCase "Search by expression OR works" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithBoolean>()
                values.Insert({ Id = 1; HasValue = true }) |> ignore 
                values.Insert({ Id = 2; HasValue = false }) |> ignore 
                
                values.findMany <@ fun item -> item.Id = 2 || item.HasValue @>
                |> Seq.length
                |> function 
                    | 2 -> pass() 
                    | _ -> fail()

        testCase "Search by created where expression" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithBoolean>()
                values.Insert({ Id = 1; HasValue = true }) |> ignore 
                values.Insert({ Id = 2; HasValue = false }) |> ignore 
                
                let query = values.where <@ fun value -> value.HasValue @> id 
                values.Find(query)
                |> Seq.length
                |> function 
                    | 1 -> pass() 
                    | _ -> fail()

        testCase "Search by created where expression and id selector" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithBoolean>()
                values.Insert({ Id = 1; HasValue = true }) |> ignore 
                values.Insert({ Id = 2; HasValue = false }) |> ignore 
                
                let query = values.where <@ fun value -> value.Id @> (fun id -> id = 1 || id = 2) 
                values.Find(query)
                |> Seq.length
                |> function 
                    | 2 -> pass() 
                    | _ -> fail()

        testCase "Search by expression OR works with NOT operator" <| fun _ ->
            useDatabase <| fun db ->
                let values = db.GetCollection<RecordWithBoolean>()
                values.Insert({ Id = 1; HasValue = true }) |> ignore 
                values.Insert({ Id = 2; HasValue = false }) |> ignore 
                
                values.findMany <@ fun item -> not (item.Id = 2 || item.HasValue) @>
                |> Seq.length
                |> function 
                    | 0 -> pass() 
                    | _ -> fail()

        testCase "Search by discriminated unions works" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore

                let query = Query.EQ("Status", BsonValue("Married"))
                let foundPerson = people.FindOne(query)
                match foundPerson with
                | { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time } -> pass()
                | otherwise -> fail()   

        testCase "Search by discriminated unions using expressions" <| fun _ ->
            useDatabase <| fun db ->
                let people = db.GetCollection<PersonDocument>("people")
                let time = DateTime(2017, 10, 15)
                let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
                people.Insert(person) |> ignore
                let foundPerson = people.findOne <@ fun person -> person.Status = Married @>
                match foundPerson with
                | { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time } -> pass()
                | otherwise -> fail()  

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
                        let shapeValue = Bson.deserializeField<Shape> bsonValue
                        match shapeValue with
                        | Composite [ Circle 2.0; other ] -> true
                        | otherwise -> false
                    )

                records.Find(searchQuery)
                |> Seq.length
                |> function 
                    | 1 -> pass()
                    | n -> fail()

        testCase "Full custom search works by using expressions" <| fun _ ->
            useDatabase <| fun db ->
                let records = db.GetCollection<RecordWithShape> "Shapes"                                     
                let shape = 
                    Composite [ 
                      Circle 2.0;
                      Composite [ Circle 4.0; Rect(2.0, 5.0) ]
                    ]
                let record = { Id = 1; Shape = shape }
                records.Insert(record) |> ignore

                let searchResults = 
                    records.fullSearch 
                        <@ fun r -> r.Shape @> 
                        (fun shape -> 
                            match shape with 
                            | Composite [ Circle 2.0; other ] -> true
                            | otherwise -> false) 
                
                searchResults            
                |> Seq.length
                |> function 
                    | 1 -> pass()
                    | n -> fail()
    ]