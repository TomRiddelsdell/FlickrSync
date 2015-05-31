﻿module OAuth

open System
open System.Text
open FSharp.Data
open System.Security.Cryptography

let consumerKey = "8b2e1dfbe88471e9e5fdc7b558ce0b5d"
let consumerSecret = "4433e410c1a894b3"

let requestTokenURI = "http://www.flickr.com/services/oauth/request_token"
let accessTokenURI = "https://www.flickr.com/services/oauth/access_token"
let authorizeURI = "https://www.flickr.com/services/oauth/authorize"

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

let BasicParameters token tokenSecret = 
    ["oauth_consumer_key", consumerKey;
     "oauth_nonce", nonce;
     "oauth_signature_method", "HMAC-SHA1";
     "oauth_token", token;
     "oauth_timestamp", currentUnixTime.ToString();
     "oauth_version", "1.0"]
