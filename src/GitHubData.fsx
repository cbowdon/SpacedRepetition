#!/usr/bin/fsharpi --exec
#r "System.Runtime.Serialization.dll"
#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"

open System
open System.IO
open System.Text
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

[<Literal>]
let repoSample = "repos_sample.json"

[<Literal>]
let langSample = "langs_sample.json"

[<Literal>] 
let commitSample = "commits_sample.json"

// HACK: The json files _were_ downloaded by this script
// but before the type providers were added
type Repos = JsonProvider<repoSample>
(*
type Commits = JsonProvider<commitSample>

type Languages = JsonProvider<langSample>
*)

module Json =
    open System.Runtime.Serialization.Json

    let serialize<'a> (x: 'a) : string = 
        let ser = new DataContractJsonSerializer(typedefof<'a>)
        use stream = new MemoryStream()
        ser.WriteObject(stream, x)
        stream.ToArray() |> Encoding.UTF8.GetString

    let deserialize<'a> (x: string) : 'a = 
        let ser = new DataContractJsonSerializer(typedefof<'a>)
        use stream = new MemoryStream()
        ser.ReadObject(stream) :?> 'a

module GitHubData =

    open Json

    [<Literal>]
    let username = "cbowdon"

    [<Literal>]
    let tokenFile = "token"

    let repoCache = "repo_cache.json"
    let langCache = "lang_cache.json"
    let commitCache = "commit_cache.json"

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

    let concatJsons (jsons: string seq) : string = String.Join(",", jsons) |> sprintf "[ %s ]"

    let saveTo (filename: string) (data: string) : unit = File.WriteAllText(filename, data)

    let arrayHead (a:'T[]) : 'T = 
        match List.ofArray a with
        | x::_  -> x
        | _     -> failwith "Empty array" // this is bad, shouldn't use partial function, let alone write one

    let downloadReposData : Async<unit> = async { 
        let! repos = queryGitHub (reposUrl username) []
        do repos |> saveTo repoSample
        do repos |> saveTo repoCache
    }

    let downloadLangData (repoNames: string seq) : Async<unit> = async {
        let! langs = repoNames |> Seq.map (fun x -> queryGitHub (languageUrl username x) []) |> Async.Parallel
        do langs |> arrayHead |> saveTo langSample
        Seq.zip repoNames langs 
        |> Map.ofSeq
        |> serialize 
        |> saveTo langCache        
    }

    let downloadCommitData (repoNames: string seq) : Async<unit> = async {
        let! commits = repoNames |> Seq.map (fun x -> queryGitHub (commitsUrl username x) [ "author", username ]) |> Async.Parallel
        do commits |> arrayHead |> saveTo commitSample
        Seq.zip repoNames commits
        |> Map.ofSeq
        |> serialize 
        |> saveTo commitCache
    }

open GitHubData

async {
    let! repos = Repos.AsyncGetSamples()
    let names = repos |> Seq.map (fun x -> x.Name)
    do! downloadLangData names
    do! downloadCommitData names
} |> Async.RunSynchronously

(*
let repos = Repos.GetSamples()
let langs = Languages.GetSamples()
let commits = Commits.GetSamples()

printfn "Repos: %i" repos.Length
printfn "Langs: %i" langs.Length
printfn "Commits: %i" commits.Length

type Repository = { Name: string }

type Change = {
    Filename: string
    Additions: int
    Deletions: int
}

type Commit = {
    Repository: Repository
    Date: DateTime
    //Changes: Change seq // TODO
}

for c in commits do
    for c' in c do
        c'.Commit.Author.Date.ToString("yyyy-MM-dd") |> printfn "%s" 

*)
