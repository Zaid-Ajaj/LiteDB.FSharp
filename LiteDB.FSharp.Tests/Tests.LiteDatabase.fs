module Tests.LiteDatabase

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp

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

let liteDatabaseUsage = 
    testList "LiteDatabase usage" [
        testCase "Inserting and FindById works" <| fun _ ->
            let mapper = FSharpBsonMapper()
            use memoryStream = new MemoryStream()
            use db = new LiteDatabase(memoryStream, mapper)
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

        testCase "Search by Query works" <| fun _ ->
            let mapper = FSharpBsonMapper()
            use memoryStream = new MemoryStream()
            use db = new LiteDatabase(memoryStream, mapper)
            let people = db.GetCollection<PersonDocument>("people")
            let time = DateTime(2017, 10, 15)
            let person = { Id = 1; Name = "Mike"; Age = 10; Status = Married; DateAdded = time }
            people.Insert(person) |> ignore
            let query = Query.And(Query.GT("Age", Bson.fromInt 5), Query.LT("Age", Bson.fromInt 15))
            people.Find(query)
            |> Seq.length 
            |> function | 1 -> pass() 
                        | n -> fail()
    ]