#!/usr/bin/fsharpi --exec
#r "System.Runtime.Serialization.dll"
#r "./FSharp.Data.2.1.1/lib/net40/FSharp.Data.dll"

open System
open System.IO
open System.Text
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

module Json =
    open System.Runtime.Serialization.Json

    let serialize<'a> (x: 'a) : string =
        let ser = new DataContractJsonSerializer(typedefof<'a>)
        use stream = new MemoryStream()
        ser.WriteObject(stream, x)
        stream.ToArray() |> Encoding.UTF8.GetString

    let deserialize<'a> (x: string) : 'a =
        let ser = new DataContractJsonSerializer(typedefof<'a>)
        let bytes = x |> Encoding.UTF8.GetBytes
        use stream = new MemoryStream(bytes)
        ser.ReadObject(stream) :?> 'a

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

let repoCache = "cache_repo.json"
let langCache = "cache_lang.json"
let commitCache = "cache_commit.json"

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

open Json

let cache (cacheFile: string) (func: 'a) : 'a = 
    if File.Exists(cacheFile)
    then
        let res = cacheFile |> File.ReadAllText
        res |> printfn "%s"
        cacheFile |> File.ReadAllText |> deserialize
    else
        let result = func 
        result |> serialize |> saveTo cacheFile
        result

let downloadReposData : Async<Repos.Root[]> = async { 
    let! repos = queryGitHub (reposUrl username) []
    let result = repos |> Repos.Parse
    return result
}

let getRepos : Async<Repos.Root[]> = downloadReposData |> cache repoCache

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
    return! repos
    |> Seq.map (fun r -> r.Name)
    |> downloadLangData 
    |> cache langCache
}

let downloadCommitData (repoNames: string seq) : Async<Map<string,Commits.Root[]>> = async {
    let! commits = 
        repoNames 
        |> Seq.map (fun x -> queryGitHub (commitsUrl username x) [ "author", username ]) 
        |> Async.Parallel
    let result = 
        commits 
        |> Seq.map Commits.Parse 
        |> Seq.zip repoNames 
        |> Map.ofSeq
    return result
}

let getCommits : Async<Map<string,Commits.Root[]>> = async {
    let! repos = getRepos
    return! repos
    |> Seq.map (fun r -> r.Name)
    |> downloadCommitData
    |> cache commitCache
}

getCommits |> Async.RunSynchronously
