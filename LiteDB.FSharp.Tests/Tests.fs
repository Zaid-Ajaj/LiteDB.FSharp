module Tests

open Expecto

open System.IO

open LiteDB
open LiteDB.FSharp

type Person = {
    Id: int
    Name: string
}

let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"
  
let liteDbTests =
  testList "All Tests" [

    testCase "Bson serialize works" <| fun _ -> 
      let person = { Id = 1; Name = "Mike" }
      let doc = Bson.serialize person
      Expect.equal 2 doc.Keys.Count "Generated BSON document has 2 keys"      
      Expect.equal (doc.["_id"].AsString) "1" "_id is serialized correctly"
      Expect.equal (doc.["Name"].AsString) "Mike" "Name is serialized correctly"

    testCase "Bson serilialize/deserialize works" <| fun _ ->
      let person = { Id = 1; Name = "Mike" }
      let doc = Bson.serialize person
      let incarnated = Bson.deserialize<Person> doc
      match incarnated with 
      | { Id = 1; Name = "Mike" } -> pass()
      | otherwise -> fail()
  ]