module Runner

open Expecto
open Expecto.Logging
open LiteDB.FSharp
open LiteDB.FSharp.Experimental
open Tests.Bson
open Tests.LiteDatabase
open Tests.DBRef
open Tests.InheritedType

let testConfig =  
    { Expecto.Tests.defaultConfig with 
         parallelWorkers = 1
         verbosity = LogLevel.Debug }

let defaultValueTests = 
    testList "DefaultValue.fromType" [
        testCase "Works for booleans" <| fun _ ->
            let value = DefaultValue.fromType (typeof<bool>) |> unbox<bool>
            Expect.equal false value "Default boolean value is false"

        testCase "Works with optionals" <| fun _ ->
            let value = DefaultValue.fromType (typeof<Option<int>>) |> unbox<Option<int>>
            Expect.equal None value "Option<'t> has None a default value"

        testCase "Default of string is an empty string" <| fun _ ->
            let value = DefaultValue.fromType (typeof<string>) |> unbox<string>
            Expect.equal "" value "An empty string is the default string"
    ]

let liteDbTests mapper=
    testList "All tests" [  
        defaultValueTests
        bsonConversions
        liteDatabaseUsage mapper
        dbRefTests mapper
        inheritedTypeTests mapper
    ]


[<EntryPoint>]
let main argv =
    let tests=
         [
         FSharpBsonMapper()
         TypeShapeMapper() :> FSharpBsonMapper
         ]|>List.map liteDbTests
    tests |> List.map(fun t ->runTests testConfig t)|>List.tryFind (fun r->r<>0)|>Option.defaultValue 0
    
     
     
     
    