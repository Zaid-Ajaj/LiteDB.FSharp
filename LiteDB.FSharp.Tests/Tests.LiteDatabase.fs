module Tests.LiteDatabase

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions

open Tests.Types

type MaritalStatus = Single | Married

type PersonDocument = {
    Id: int
    Name: string
    DateAdded: DateTime
    Age: int
    Status: MaritalStatus
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
        testCase "Inserting and FindById works" <| fun _ ->
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
    ]