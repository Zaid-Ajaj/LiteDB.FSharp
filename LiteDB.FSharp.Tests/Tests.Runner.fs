module Runner

open Expecto
open Expecto.Logging
open LiteDB.FSharp
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

let liteDbTests = 
    testList "All tests" [  
        defaultValueTests
        bsonConversions
        liteDatabaseUsage
        dbRefTests
        inheritedTypeTests
    ]


[<EntryPoint>]
let main argv = runTests testConfig liteDbTests