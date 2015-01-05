#!/usr/bin/fsharpi --exec
#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"

open System
open System.IO
open System.Text
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

type Url = string
type Query = (string * string) list

[<Literal>]
let repoFile = "repos.json"

[<Literal>]
let langFile = "langs.json"

[<Literal>] 
let commitFile = "commits.json"

[<Literal>]
let username = "cbowdon"
let password = Environment.GetCommandLineArgs() |> Seq.last

let auth : string = 
    sprintf "%s:%s" username password 
        |> Encoding.ASCII.GetBytes 
        |> Convert.ToBase64String 
        |> sprintf "Basic %s"

// GitHub API URLs
let reposUrl user = sprintf "/users/%s/repos" user
let languageUrl user repo = sprintf "/repos/%s/%s/languages" user repo
let commitsUrl user repo = sprintf "/repos/%s/%s/commits" user repo

let queryGitHub (u:Url) (q:Query) : Async<string> = 
    let gitHubApiUrl = sprintf "https://api.github.com%s"
    let url = gitHubApiUrl u
    let h = [ Accept "application/vnd.github.v3+json"
            ; Authorization auth
            ; UserAgent "cbowdon - F# script" ]
    Http.AsyncRequestString(url, httpMethod = "GET", query = q, headers = h)

let downloadRawReposData : Async<unit> = async { 
    let! repos = queryGitHub (reposUrl username) []
    File.WriteAllText(repoFile, repos)
}

// Run sync 
// why am I even doing async?
downloadRawReposData |> Async.RunSynchronously

printfn "Finished downloading repo data"

// Oh, dirty
type Repos = JsonProvider<repoFile>

let repoNames : Async<string seq> = async {
    let! repos = Repos.AsyncGetSamples()
    return repos |> Seq.map (fun x -> x.Name)
}

let concatJsons (jsons: string seq) : string = String.Join(",", jsons) |> sprintf "[ %s ]"
let saveTo (filename: string) (data: string) : unit = File.WriteAllText(filename, data)

let downloadLangData (repos: string seq) : Async<unit> = async {
    let! langs = repos |> Seq.map (fun x -> queryGitHub (languageUrl username x) []) |> Async.Parallel
    langs |> concatJsons |> saveTo langFile
}

let downloadCommitData (repos: string seq) : Async<unit> = async {
    let! commits = repos |> Seq.map (fun x -> queryGitHub (commitsUrl username x) [ "author", username ]) |> Async.Parallel
    commits |> concatJsons |> saveTo langFile
}

async {
    let! repos = repoNames
    return! downloadLangData repos
    return! downloadCommitData repos
} |> Async.RunSynchronously

(*
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
