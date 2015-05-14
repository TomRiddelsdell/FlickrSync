module FsTestApp

open System
open System.Net
open System.Text
open System.IO
open FSharp.Data
open System.Collections.Generic
open OAuth
open PhotoContainers
 
let apiMethodCallsURI = "https://api.flickr.com/services/rest"
 
let genericQuery token tokenSecret flickrMethod methodParams =
    let signingKey = compositeSigningKey consumerSecret tokenSecret
 
    let queryParameters =
        ["oauth_consumer_key", consumerKey;
         "oauth_nonce", nonce;
         "oauth_signature_method", "HMAC-SHA1";
         "oauth_token", token;
         "oauth_timestamp", currentUnixTime.ToString();
         "oauth_version", "1.0"]
    let queryParametersMethod = ("method", flickrMethod)::queryParameters
    let queryParametersUnsigned = methodParams @ queryParametersMethod
 
    let signingString = baseString "POST" accessTokenURI queryParametersUnsigned
    let oauth_signature = hmacsha1 signingKey signingString
   
    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParametersUnsigned
   
    let headerValue = createAuthorizeHeader realQueryParameters
   
    Http.RequestString( apiMethodCallsURI,
                        query = realQueryParameters,
                        httpMethod = "GET")
    
let getAllSets token tokenSecret =
    genericQuery token tokenSecret "flickr.photosets.getList" []
    |> FlickrParser.ParseSets

let getPhotos token tokenSecret setId =
    genericQuery token tokenSecret "flickr.photosets.getPhotos" ["photoset_id", setId; "media", "photos"]
    |> FlickrParser.ParseSet

let getVideos token tokenSecret setId =
    genericQuery token tokenSecret "flickr.photosets.getPhotos" ["photoset_id", setId; "media", "videos"]
    |> FlickrParser.ParseSet

[<EntryPoint>]
let main argsv =
    let args =
        if argsv.Length = 4 then
            [   "local_dir", argsv.[0];
                "oauth_token", argsv.[1];
                "oauth_token_secret", argsv.[2];
                "user_nsid", argsv.[3] ]
            |> Map.ofList
        else
            let authres = requestToken()
            let url = authorizeURI + "?oauth_token=" + authres.["oauth_token"]
 
            System.Diagnostics.Process.Start("iexplore.exe", url) |> ignore
            Console.WriteLine("Enter authorisation code: ")
            let authcode = Console.ReadLine().Replace("-","").Replace(" ","")
 
            Console.WriteLine("Authorisation code {0} used to get access token", authcode)
 
            accessToken (authres.["oauth_token"]) (authres.["oauth_token_secret"]) (authcode.ToString())
            |> fun x -> x.Add ("local_dir", argsv.[0])
 
    printfn "Access Response: %A" args
 
    // Get the list of albums from Flikr
    let flickrAlbums = 
        getAllSets (args.["oauth_token"]) (args.["oauth_token_secret"])
        |> Array.map (fun set -> 
                                set, 
                                (getPhotos (args.["oauth_token"]) (args.["oauth_token_secret"]) (set.Id.ToString())),
                                (getVideos (args.["oauth_token"]) (args.["oauth_token_secret"]) (set.Id.ToString())))
        |> Array.map (fun tup -> match tup with (set, photos, videos) -> 
                                                                        set, 
                                                                        photos |> Array.map (fun p -> Photo(p.Id, p.Title, "", Convert.ToBoolean(p.Ispublic), Convert.ToBoolean(p.Isfriend), Convert.ToBoolean(p.Isfamily))),
                                                                        videos |> Array.map (fun v -> Video(v.Id, v.Title, "", Convert.ToBoolean(v.Ispublic), Convert.ToBoolean(v.Isfriend), Convert.ToBoolean(v.Isfamily))) )
        |> Array.map (fun tup -> match tup with (set, photos, videos) -> 
                                                                        Album(set.Id, set.Title, set.Description, set.Primary, photos, videos, set.VisibilityCanSeeSet) )

    // Scan local directory and build up album list for comparison
    let localAlbums = 
        [| for dir in Directory.GetDirectories(args.["local_dir"]) ->
                let photos = [| for pic in Directory.GetFiles(dir, "*.jpg;*.JPG", SearchOption.AllDirectories) -> Photo("", pic, "", false, true, true) |]
                let videos = [| for vid in Directory.GetFiles(dir, "*.mp4;*.MP4", SearchOption.AllDirectories) -> Video("", vid, "", false, true, true) |]
                Album("", dir, "", "", photos, videos, PhotoContainers.PhotoVisibility.FamilyAndFriends) |]

    (*
      Compare albums and build missing album list
         - Album names will be matched by date and partial name match
         - Existing Flicker album info will be maintained
    *)
    AlbumComparison.Partition localAlbums flickrAlbums
    |> fun part -> match part with (matching, missing) -> 
                                    [for a in matching -> printfn "Flickr already has: %s" a.Title] |> ignore
                                    [for a in missing-> printfn "Flickr is missing: %s" a.Title] |> ignore

    0