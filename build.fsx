#r "paket:
nuget Fake.Core.Target
nuget Fake.DotNet.Cli
nuget Fake.DotNet.Paket
nuget Fake.IO.FileSystem
//"

#load ".fake/build.fsx/intellisense.fsx"

open System.IO

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators

let cwd = __SOURCE_DIRECTORY__
let projectPath = cwd </> "LiteDB.FSharp"
let testsPath = cwd </> "LiteDB.FSharp.Tests"

Target.create "RunTests" <| fun _ ->
    "LiteDB.FSharp.Tests.fsproj"
    |> DotNet.exec (fun defaults -> { defaults with WorkingDirectory = testsPath}) "run"
    |> ignore

Target.create "Clean" <| fun _ -> 
    [
        cwd </> "bin"
        projectPath </> "bin"
        projectPath </> "obj"
        testsPath </> "bin"
        testsPath </> "obj"
    ]
    |> Shell.cleanDirs

Target.create "Build" <| fun _ ->
    let setParams (defaults: DotNet.BuildOptions) =
        {
            defaults with
                Configuration = DotNet.BuildConfiguration.Release
        }

    DotNet.build setParams (projectPath </> "LiteDB.FSharp.fsproj")

Target.create "PackNuget" <| fun _ ->
    Paket.pack (fun defaults ->
        {
            defaults with
                TemplateFile = projectPath </> "paket.template"
                BuildConfig = "Release"
                MinimumFromLockFile = true
                OutputPath = cwd </> "bin"
        })

Target.create "PublishNuget" <| fun _ ->
    let nugetKey =
        match Environment.environVarOrNone "NUGET_KEY" with
        | Some nugetKey -> nugetKey
        | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
    let nupkg = Directory.GetFiles(cwd </> "bin") |> Seq.head

    let setParams (defaults: DotNet.NuGetPushOptions) =
        {
            defaults with
                PushParams =
                    {
                        defaults.PushParams with
                            ApiKey = Some nugetKey
                            Source = Some "nuget.org"
                    }
        }

    DotNet.nugetPush setParams nupkg

"Clean"
   ==> "Build"
   ==> "PackNuget"
   ==> "PublishNuget"

"Clean"
   ==> "RunTests"

Target.runOrDefault "RunTests"