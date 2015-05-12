module FsTestApp

open System
open System.IO
open System.Net
open System.Security.Cryptography
open System.Text
open FSharp.Data
open System.Collections.Generic
open PhotoContainers
 
// Twitter OAuth Constants
let consumerKey = "8b2e1dfbe88471e9e5fdc7b558ce0b5d"
let consumerSecret = "4433e410c1a894b3"
let requestTokenURI = "http://www.flickr.com/services/oauth/request_token"
let accessTokenURI = "https://www.flickr.com/services/oauth/access_token"
let authorizeURI = "https://www.flickr.com/services/oauth/authorize"
let apiMethodCallsURI = "https://api.flickr.com/services/rest"
 
let nonce = System.Guid.NewGuid().ToString().Substring(24)
 
// Utilities
let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)
 
 
// Core Algorithms
let hmacsha1 signingKey str =
    let converter = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey : string))
    let inBytes = Encoding.ASCII.GetBytes(str : string)
    let outBytes = converter.ComputeHash(inBytes)
    Convert.ToBase64String(outBytes)
 
let compositeSigningKey consumerSecret tokenSecret =
    urlEncode(consumerSecret) + "&" + urlEncode(tokenSecret)
 
let baseString httpMethod baseUri queryParameters =
    httpMethod + "&" +
    urlEncode(baseUri) + "&" +
    (queryParameters
     |> Seq.sortBy (fun (k,v) -> k)
     |> Seq.map (fun (k,v) -> urlEncode(k)+"%3D"+urlEncode(v))
     |> String.concat "%26")
 
let createAuthorizeHeader queryParameters =
    let headerValue =
        "OAuth " +
        (queryParameters
         |> Seq.map (fun (k,v) -> urlEncode(k)+"\x3D\""+urlEncode(v)+"\"")
         |> String.concat ",")
    headerValue
 
let currentUnixTime = floor (DateTime.UtcNow - DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds
 
 
/// Request a token from Twitter and return:
///  oauth_token, oauth_token_secret, oauth_callback_confirmed
let requestToken() =
    let signingKey = compositeSigningKey consumerSecret ""
 
    let queryParameters =
        ["oauth_callback", "oob";
         "oauth_consumer_key", consumerKey;
         "oauth_nonce", nonce;
         "oauth_signature_method", "HMAC-SHA1";
         "oauth_timestamp", currentUnixTime.ToString();
         "oauth_version", "1.0"]
 
    let signingString = baseString "POST" requestTokenURI queryParameters
    let oauth_signature = hmacsha1 signingKey signingString
 
    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParameters
   
    let resp = Http.RequestString(requestTokenURI, body = FormValues realQueryParameters, httpMethod = "POST")
 
    seq { for mapping in resp.Split('&') do
            let kv = mapping.Split('=')
            yield kv.[0], kv.[1]}
    |> Map.ofSeq
 
/// Get an access token from Flikr and returns:
///   oauth_token, oauth_token_secret
let accessToken token tokenSecret verifier =
    let signingKey = compositeSigningKey consumerSecret tokenSecret
 
    let queryParameters =
        [
         "oauth_consumer_key", consumerKey;
         "oauth_nonce", nonce;
         "oauth_signature_method", "HMAC-SHA1";
         "oauth_timestamp", currentUnixTime.ToString();
         "oauth_token", token;
         "oauth_verifier", verifier;
         "oauth_version", "1.0"]
 
    let signingString = baseString "POST" accessTokenURI queryParameters
    let oauth_signature = hmacsha1 signingKey signingString
   
    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParameters
   
    let headerValue = createAuthorizeHeader realQueryParameters
   
    let resp = Http.RequestString(  accessTokenURI ,
                                    //headers = [ HttpRequestHeader.Authorization.ToString(), headerValue ],
                                    body = FormValues realQueryParameters,
                                    httpMethod = "POST")
   
    seq { for mapping in resp.Split('&') do
            let kv = mapping.Split('=')
            yield kv.[0], kv.[1]}
    |> Map.ofSeq
 
/// Compute the 'Authorization' header for the given request data
let authHeaderAfterAuthenticated url httpMethod token tokenSecret queryParams =
    let signingKey = compositeSigningKey consumerSecret tokenSecret
 
    let queryParameters =
            ["oauth_consumer_key", consumerKey;
             "oauth_nonce", nonce;
             "oauth_signature_method", "HMAC-SHA1";
             "oauth_token", token;
             "oauth_timestamp", currentUnixTime.ToString();
             "oauth_version", "1.0"]
 
    let signingQueryParameters =
        List.append queryParameters queryParams
 
    let signingString = baseString httpMethod url signingQueryParameters
    let oauth_signature = hmacsha1 signingKey signingString
    let realQueryParameters = ("oauth_signature", oauth_signature)::queryParameters
    let headerValue = createAuthorizeHeader realQueryParameters
    headerValue
 
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