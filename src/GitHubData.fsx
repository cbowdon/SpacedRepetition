#!/usr/bin/fsharpi --exec
#r "System.Runtime.Serialization.dll"
#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"
#r "./FsPickler.1.0.6/lib/net45/FsPickler.dll"

open System
open System.IO
open System.Text
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open Nessos.FsPickler

[<Literal>]
let repoSample = "sample_repo.json"

[<Literal>] 
let commitSample = "sample_commit.json"

type Repos = JsonProvider<repoSample>
type Commits = JsonProvider<commitSample>

[<Literal>]
let username = "cbowdon"

[<Literal>]
let tokenFile = "token"

let repoCache = "cache_repo.pickle"
let langCache = "cache_lang.pickle"
let commitCache = "cache_commit.pickle"

type Url = string

type Query = (string * string) list

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
let saveTo (filename: string) (data: byte[]) : unit = File.WriteAllBytes(filename, data)

let cache (cacheFile: string) (func: unit -> Async<'a>) : Async<'a> = async {
    let binary = FsPickler.CreateBinary()
    if File.Exists(cacheFile)
    then
        let res = cacheFile |> File.ReadAllText
        return cacheFile |> File.ReadAllBytes |> binary.UnPickle
    else
        let! result = func() 
        result |> binary.Pickle |> saveTo cacheFile
        return result
}

// It turns out I miss fmap
let asyncFmap (f: 'a -> 'b) (a: Async<'a>) : Async<'b> = async { let! a' = a in return f a' }

let downloadReposData : Async<string> = queryGitHub (reposUrl username) []

let getRepos : Async<Repos.Root[]> = 
    (fun () -> downloadReposData) 
    |> cache repoCache
    |> asyncFmap Repos.Parse

let downloadLangData (repoNames: string seq) : Async<Map<string,string>> = async {
    let! langs = 
        repoNames 
        |> Seq.map (fun x -> queryGitHub (languageUrl username x) []) 
        |> Async.Parallel
    // TODO parse JSON into dict
    let result = 
        langs
        |> Seq.zip repoNames 
        |> Map.ofSeq
    return result
}

let getLangs : Async<Map<string,string>> = async {
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
    let result = 
        commits 
        |> Seq.zip repoNames 
        |> Map.ofSeq
    return result
}

let getCommits : Async<Map<string,Commits.Root[]>> = async {
    let! repos = getRepos |> asyncFmap (Seq.map (fun r -> r.Name))
    let! data = cache commitCache (fun () -> downloadCommitData repos) 
    return data |> Map.map (fun k v -> Commits.Parse v)
}

getCommits |> Async.RunSynchronously
