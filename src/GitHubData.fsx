#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"

open System
open FSharp.Data

type Repos = JsonProvider<"https://api.github.com/users/cbowdon/repos">

type Language = String

type RepoMetaData = {
    Language: Language
    LastTouched: DateTime
}

let data : Async<seq<RepoMetaData>> = async {
    let! repos = Repos.AsyncGetSamples()
    return repos 
        |> Seq.filter (fun x -> x.Language.IsSome)
        |> Seq.map (fun x -> { Language = x.Language.Value; LastTouched = x.UpdatedAt })
}

printfn "Language stats:"
let result = data |> Async.RunSynchronously
for d in result do
    printfn "%s - %s" d.Language (d.LastTouched.ToString("yyyy-MM-dd"))
