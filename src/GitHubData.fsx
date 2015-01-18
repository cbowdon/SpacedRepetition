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

module GitHub =

    [<Literal>]
    let repoSample = "sample_repo.json"

    [<Literal>] 
    let commitSample = "sample_commit.json"

    [<Literal>]
    let username = "cbowdon"

    [<Literal>]
    let tokenFile = "token"

    let repoCache = "cache_repo.json"
    let langCache = "cache_lang.json"
    let commitCache = "cache_commit.json"

    type Url = string
    type Query = (string * string) list

    type Repos = JsonProvider<repoSample>
    type Commits = JsonProvider<commitSample>

    type LangStats = Map<string,float>
    type CommitHistory = Map<DateTime,Commits.Root seq>
    type Language = string

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
    // Irregular structure defies type provider
    let parseLangMap (json: string) : LangStats =
        let pairs = 
            json
            |> JsonValue.Parse
            |> (fun l -> l.Properties)
            |> Seq.map (fun pair -> let k, v = pair in (k, v.AsFloat()))
        let total = pairs |> Seq.fold (fun sum (_, v) -> sum + v) 0.0
        pairs 
        |> Seq.map (fun (k, v) -> (k, v / total))
        |> Map.ofSeq

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

let history (commits: GitHub.Commits.Root[]) : GitHub.CommitHistory = 
    commits 
    |> Seq.groupBy (fun c -> c.Commit.Author.Date.Date) 
    |> Map.ofSeq

let lastNonTrivialWork (history : GitHub.CommitHistory) : DateTime = 

    let isTrivial (k: DateTime) (v: seq<'a>) : bool =
        let dayBefore, dayAfter = k.AddDays(-1.0), k.AddDays(1.0)
        Seq.length v <= 1
        && history |> Map.containsKey dayBefore |> not
        && history |> Map.containsKey dayAfter |> not

    let rec firstNonTrivial lst = 
        match lst with
        | (k,v)::rest   -> if isTrivial k v then firstNonTrivial rest else Some k
        | []            -> None

    let sorted = 
        history
            |> Map.toList
            |> List.sortBy (fun (k,_) -> k)
            |> List.rev

    match firstNonTrivial sorted with
    | Some d    -> d
    | None      -> fst (Seq.head sorted) 
    // will error if empty history, as it should

let format (d:DateTime) : string = d.ToString("yyyy-MM-dd")

Async.RunSynchronously <| async {
    let! repos = GitHub.getRepos
    let! langs = GitHub.getLangs
    let! commits = GitHub.getCommits
    let repoHistories = commits |> Map.map (fun k v -> history v)

    // TODO mapping from the above to Map<Language,CommitHistory>
    
    // Test with Haskell
    // Last time non-trivial work with language
    let lastHsDay = lastNonTrivialWork (failwith "unfinished")
    format lastHsDay |> printfn "Haskell: %s" 

    // Total activity with language
    // Number of groups of activity
}
