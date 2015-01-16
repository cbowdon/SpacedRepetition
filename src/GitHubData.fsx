#!/usr/bin/fsharpi --exec
#I "./FSharp.Data.2.1.1/lib/net40/"
#I "./Newtonsoft.Json.6.0.5/lib/net45/"
#I "./FsPickler.1.0.6/lib/net45/"
#I "./FsPickler.Json.1.0.6/lib/net45/"

#r "FSharp.Data.dll"
#r "Newtonsoft.Json.dll"
#r "FsPickler.dll"
#r "FsPickler.Json.dll"

open System
open System.IO
open System.Text
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open FSharp.Data.JsonExtensions
open Nessos.FsPickler
open Nessos.FsPickler.Json

[<Literal>]
let repoSample = "sample_repo.json"

[<Literal>] 
let commitSample = "sample_commit.json"

type Url = string
type Query = (string * string) list
type LangStats = Map<string,int>

type Repos = JsonProvider<repoSample>
type Commits = JsonProvider<commitSample>

[<Literal>]
let username = "cbowdon"

[<Literal>]
let tokenFile = "token"

let repoCache = "cache_repo.json"
let langCache = "cache_lang.json"
let commitCache = "cache_commit.json"

let auth : string = 
    let token = File.ReadAllText(tokenFile).TrimEnd(Environment.NewLine.ToCharArray())
    sprintf "%s:x-oauth-basic" token
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

// TODO modules for cleanliness
let saveTo (filename: string) (data: string) : unit = File.WriteAllText(filename, data)

let cache (cacheFile: string) (func: unit -> Async<'a>) : Async<'a> = async {
    let json = FsPickler.CreateJson()
    if File.Exists(cacheFile)
    then
        let res = cacheFile |> File.ReadAllText
        return cacheFile |> File.ReadAllText |> json.UnPickleOfString
    else
        let! result = func() 
        result |> json.PickleToString |> saveTo cacheFile
        return result
}

let downloadReposData : Async<string> = queryGitHub (reposUrl username) []

let getRepos : Async<Repos.Root[]> = async {
    let! repos = cache repoCache (fun () -> downloadReposData)
    return Repos.Parse repos
}

// GitHub lang stats are given as Map<string,int>
// Irregular structure defies type providers
let parseLangMap : string -> LangStats =
    JsonValue.Parse
    >> (fun l -> l.Properties)
    >> Seq.map (fun pair -> let k, v = pair in (k, v.AsInteger()))
    >> Map.ofSeq

let downloadLangData (repoNames: string seq) : Async<Map<string,LangStats>> = async {
    let! langs = 
        repoNames 
        |> Seq.map (fun x -> queryGitHub (languageUrl username x) []) 
        |> Async.Parallel
    return langs 
        |> Seq.map parseLangMap
        |> Seq.zip repoNames 
        |> Map.ofSeq
}

let getLangs : Async<Map<string,LangStats>> = async {
    let! repos = getRepos 
    return! cache langCache (fun () ->
        repos
        |> Seq.map (fun r -> r.Name)
        |> downloadLangData )
}

let downloadCommitData (repoNames: string seq) : Async<Map<string,string>> = async {
    let! commits = 
        repoNames 
        |> Seq.map (fun x -> queryGitHub (commitsUrl username x) [ "author", username ]) 
        |> Async.Parallel
    return commits
        |> Seq.zip repoNames 
        |> Map.ofSeq
}

let getCommits : Async<Map<string,Commits.Root[]>> = async {
    let! repos = getRepos
    let names = repos |> Seq.map (fun r -> r.Name)
    let! data = cache commitCache (fun () -> downloadCommitData names) 
    return data |> Map.map (fun k v -> Commits.Parse v)
}

Async.RunSynchronously <| async {
    let! repos = getRepos
    let! langs = getLangs
    let! commits = getCommits
    failwith "TODO"
} 
