#!/usr/bin/fsharpi --exec
#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"

open System
open FSharp.Data

type Repos = JsonProvider<"https://api.github.com/users/cbowdon/repos">

type Language = String

type RepoMetaData = {
    Language: Language
    LastTouched: DateTime
}

let data : Async<RepoMetaData seq> = async {
    let! repos = Repos.AsyncGetSamples()
    return repos 
        |> Seq.filter (fun x -> x.Language.IsSome)
        |> Seq.map (fun x -> { Language = x.Language.Value; LastTouched = x.UpdatedAt })
        |> Seq.sortBy (fun x -> DateTime.MaxValue - x.LastTouched)
        |> Seq.fold (fun acc x ->
            let exists = acc |> Seq.exists (fun x' -> x.Language = x'.Language)
            if exists then acc else Seq.append acc [x]) Seq.empty
}

printfn "Language stats:"
let result = data |> Async.RunSynchronously
for d in result do
    printfn "%s - %s" d.Language (d.LastTouched.ToString("yyyy-MM-dd"))
