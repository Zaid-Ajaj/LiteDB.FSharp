module Tests

open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp

type Person = { Id: int; Name: string }
type LowerCaseId = { id: int; age:int }
type SimpleUnion = One | Two
type RecordWithSimpleUnion = { Id: int; Union: SimpleUnion }
type RecordWithList = { Id: int; List: int list }
type Maybe<'a> = Just of 'a | Nothing
type RecordWithGenericUnion<'t> = { Id: int; GenericUnion: Maybe<'t> }
type RecordWithDateTime = { id: int; created: DateTime }
type RecordWithMap = { id : int; map: Map<string, string> }
let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"
  
let liteDbTests =
  testList "Bson conversions" [

    testCase "Fields are mapped correctly with indetifier Id" <| fun _ -> 
      let person = { Id = 1; Name = "Mike" }
      let doc = Bson.serialize person
      Expect.equal 2 doc.Keys.Count "Generated BSON document has 2 keys"      
      Expect.equal (Bson.readInt "_id" doc) 1 "_id property is serialized correctly"
      Expect.equal (Bson.readStr "Name" doc) "Mike" "Name property is serialized correctly"

    testCase "Fields are mapped correctly with indetifier lowercase id" <| fun _ -> 
      let record = { id = 1; age = 19 }
      let doc = Bson.serialize record
      Expect.equal 2 doc.Keys.Count "Generated BSON document has 2 keys"      
      Expect.equal (Bson.readInt "_id" doc) 1 "_id is serialized correctly"
      Expect.equal (Bson.readInt "age" doc) 19 "age property is serialized correctly"

    testCase "simple records with lowercase id" <| fun _ ->
      let record = { id = 1; age = 19 }
      let doc = Bson.serialize record
      match Bson.deserialize<LowerCaseId> doc with
      | { id = 1; age = 19 } -> pass()
      | otherwise -> fail()


    testCase "record with map" <| fun _ -> 
      let map = 
        Map.empty<string, string>
        |> Map.add "Hello" "There"
        |> Map.add "Anyone" "Here"

      let record = { id = 1; map = map }
      let doc = Bson.serialize record
      
      match Bson.deserialize<RecordWithMap> doc with
      | { id = 1; map = x } -> 
        match x.["Hello"], x.["Anyone"] with 
        | "There", "Here" -> pass()
        | otherwise -> fail() 
      | otherwisee -> fail()

    testCase "simple records" <| fun _ ->
      let person = { Id = 1; Name = "Mike" }
      let doc = Bson.serialize person
      let reincarnated = Bson.deserialize<Person> doc
      match reincarnated with 
      | { Id = 1; Name = "Mike" } -> pass()
      | otherwise -> fail()

    testCase "records with DateTime" <| fun _ ->
      let time = DateTime(2017, 10, 15)
      let record = { id = 1; created = time }
      let doc = Bson.serialize record
      match Bson.deserialize<RecordWithDateTime> doc with
      | { id = 1; created = timeCreated } -> 
          Expect.equal 2017 timeCreated.Year "Year is mapped correctly"
          Expect.equal 10 timeCreated.Month "Month is mapped correctly"
          Expect.equal 15 timeCreated.Day "Day is mapped correctly"
      | otherwise -> fail()

    testCase "records with unions" <| fun _ ->
      let fstRecord = { Id = 1; Union = One }
      let sndRecord = { Id = 2; Union = Two }
      let fstDoc, sndDoc = Bson.serialize fstRecord, Bson.serialize sndRecord
      match Bson.deserialize<RecordWithSimpleUnion> fstDoc with
      | { Id = 1; Union = One } -> pass()
      | otherwise -> fail()
      match Bson.deserialize<RecordWithSimpleUnion> sndDoc with
      | { Id = 2; Union = Two } -> pass()
      | otherwise -> fail() 

    testCase "records with lists" <| fun _ ->
      let fstRecord = { Id = 1; List = [1 .. 10] }
      let doc = Bson.serialize fstRecord
      match Bson.deserialize<RecordWithList> doc with
      | { Id = 1; List = xs } -> 
        match Seq.sum xs with
        | 55 -> pass()
        | otherwise  -> fail()
      | otherwise -> fail()

    testCase "record with generic union" <| fun _ ->
      let record = { Id = 1; GenericUnion = Just "kidding"  }
      let doc = Bson.serialize record
      match Bson.deserialize<RecordWithGenericUnion<string>> doc with
      | { Id = 1; GenericUnion = Just "kidding" } -> pass()
      | otherwise -> fail()
  ]