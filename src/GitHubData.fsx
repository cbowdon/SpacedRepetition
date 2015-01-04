#!/usr/bin/fsharpi --exec
#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"

open System
open System.IO
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

type Url = string
type Query = (string * string) list

[<Literal>]
let username = "cbowdon"

[<Literal>]
let repoFile = "repos.json"

// GitHub API URLs
let languageUrl user repo = sprintf "/repos/%s/%s/languages" user repo
let commitsUrl user repo = sprintf "/repos/%s/%s/commits?author=%s" user repo user
let reposUrl user = sprintf "/users/%s/repos" user

let queryGitHub (u:Url) (q:Query) : Async<string> = 
    let gitHubApiUrl = sprintf "https://api.github.com%s"
    let url = gitHubApiUrl u
    let h = [ Accept "application/vnd.github.v3+json"
            ; UserAgent "cbowdon - F# script" ]
    Http.AsyncRequestString(url, httpMethod = "GET", query = q, headers = h)

let downloadRawReposData : Async<unit> = async { 
    let! repos = queryGitHub (reposUrl username) []
    File.WriteAllText(repoFile, repos)
}

// Run sync 
downloadRawReposData |> Async.RunSynchronously

printfn "Finished downloading repo data"

(*
type Repos = JsonProvider<repoFile>

type RepoMetaData = {
    Language: string
    LastTouched: DateTime
}

let lastTouchedStats : Async<RepoMetaData seq> = async {
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
let result = lastTouchedStats |> Async.RunSynchronously
for d in result do
    printfn "%s - %s" d.Language (d.LastTouched.ToString("yyyy-MM-dd"))
*)
