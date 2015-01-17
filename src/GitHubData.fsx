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
type History = seq<DateTime * int>

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

let notContainingDay (d:DateTime) : History -> bool = Seq.exists (fun (d', _) -> d' = d) >> not

let history (commits: Commits.Root[]) : History = 
    let hist = commits |> Seq.countBy (fun c -> c.Commit.Author.Date.Date) 
    hist
    |> Seq.filter (fun (d, c) -> 
        // Discount one off changes
        c <= 1 
        && notContainingDay (d.AddDays(1.0)) hist
        && notContainingDay (d.AddDays(-1.0)) hist)

let histogram: History -> seq<string> =
    Seq.map (fun dc -> 
        let d, c = dc
        let d' = d.ToString("yyyy-MM-dd")
        let c' = String.replicate c "="
        sprintf "%s\t%s" d' c')

let langHistory (ls: LangStats) (h: History) : Map<string,History> = 
    let total = ls |> Map.fold (fun s k v -> s + float v) 0.0
    let proportions = ls |> Map.map (fun k v -> (float v) / total)
    proportions 
    |> Map.map (fun k v -> 
        h |> Seq.map (fun dc -> 
            let d, c = dc 
            let c' = int (v * float c)
            d, c'))

Async.RunSynchronously <| async {
    let! repos = getRepos
    let! langs = getLangs
    let! commits = getCommits
    let histories = commits |> Map.map (fun k v -> history v)

    histories 
    |> Map.iter (fun name hist ->
        printfn "%s" name
        hist |> histogram |> Seq.iter (printfn "%s"))
}
