module Runner

open Expecto
open Expecto.Logging
open Tests.Bson
open Tests.LiteDatabase
open Tests.DBRef
open Tests.InheritedType

let testConfig =  
    { Expecto.Tests.defaultConfig with 
         parallelWorkers = 1
         verbosity = LogLevel.Debug }

let liteDbTests = 
    testList "All tests" [  
        bsonConversions
        liteDatabaseUsage
        dbRefTests
        inheritedTypeTests
    ]


[<EntryPoint>]
let main argv = runTests testConfig liteDbTests