module Runner

open Expecto
open Expecto.Logging
open Tests

let testConfig =  
    { Expecto.Tests.defaultConfig with 
         parallelWorkers = 1
         verbosity = LogLevel.Debug }

[<EntryPoint>]
let main argv = runTests testConfig liteDbTests