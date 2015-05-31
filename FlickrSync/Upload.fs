module Upload

open System
open System.Net
open System.IO
open System.Text
open System.Globalization
open PhotoContainers

let ConvertNonSeekableStreamToByteArray (nonSeekableStream:Stream) = 
    if nonSeekableStream.CanSeek then
        nonSeekableStream.Position <- 0L
        nonSeekableStream
    else
        let mutable ms = new MemoryStream()
        let buffer = [|for x in 1..1024 -> new byte()|]
        let mutable bytes = nonSeekableStream.Read(buffer, 0, buffer.Length)
        while bytes > 0 do
            ms.Write(buffer, 0, bytes) |> ignore
            bytes <- nonSeekableStream.Read(buffer, 0, buffer.Length)

        ms.Position <- 0L;
        ms :> Stream

let CreateUploadData imageStream fileName  (parameters:Map<string,string>) boundary (tokenSecret:string) :Stream list= 
    let keys = [for key in parameters-> key.Key]
               |> List.sort

    let mutable hashStringBuilder = new StringBuilder(tokenSecret, 2 * 1024);
    let mutable ms1 = new MemoryStream();
    let mutable contentStringBuilder = new StreamWriter(ms1, new UTF8Encoding(false));

    [for key in keys ->
        hashStringBuilder.Append(key) |> ignore
        hashStringBuilder.Append(parameters.[key]) |> ignore
        contentStringBuilder.Write("--" + boundary + "\r\n") |> ignore
        contentStringBuilder.Write("Content-Disposition: form-data name=\"" + key + "\"\r\n") |> ignore
        contentStringBuilder.Write("\r\n") |> ignore
        contentStringBuilder.Write(parameters.[key] + "\r\n") |> ignore]
    |> ignore

    // Photo
    contentStringBuilder.Write("--" + boundary + "\r\n") |> ignore
    contentStringBuilder.Write("Content-Disposition: form-data; name=\"photo\"; filename=\"" + Path.GetFileName(fileName) + "\"\r\n") |> ignore
    contentStringBuilder.Write("Content-Type: image/jpeg\r\n") |> ignore
    contentStringBuilder.Write("\r\n") |> ignore

    contentStringBuilder.Flush() |> ignore

    let photoContents = ConvertNonSeekableStreamToByteArray(imageStream)

    let mutable ms2 = new MemoryStream()
    let postFooterWriter = new StreamWriter(ms2, new UTF8Encoding(false))
    postFooterWriter.Write("\r\n--" + boundary + "--\r\n") |> ignore
    postFooterWriter.Flush() |> ignore

    [ms1 :> Stream; photoContents; ms2 :> Stream]

//let UploadPhoto path setName = 
//    let request = FtpWebRequest.Create("https://up.flickr.com/services/upload") :?> FtpWebRequest
//    request.Credentials <- new NetworkCredential("...", "...")
//    request.Method <- WebRequestMethods.Http.Post
//    request.Timeout <- 1000000
//
//    let stream = File.OpenRead(path)
//    use targetStream = new MemoryStream()
//    stream.CopyTo(targetStream)
//    stream.Close()
//
//    let reqStream = request.GetRequestStream()
//    let buffer = targetStream.GetBuffer()
//    reqStream.Write(buffer, 0, buffer.Length)
//    reqStream.Close()
//
//    use response = request.GetResponse()
//    use responseStream = response.GetResponseStream()
//    use reader = new StreamReader(responseStream)
//    reader.ReadToEnd()

let rec CopyStreamListTo (streamList: Stream list) (stream: Stream) (bufSize: int option) = 
    let bufferSize = match bufSize with
                     | Some x -> x
                     | None -> 1024 * 16

    match streamList with
    | [] -> ignore
    | h::t ->  
            h.Position <- Convert.ToInt64(0) 

            let mutable buffer = [|for x in 1..bufferSize -> new byte()|]
            let l = h.Length
            let mutable soFar = 0

            let mutable read = h.Read(buffer, 0, buffer.Length)
            while 0 < read do
                soFar <- soFar + read
                stream.Write(buffer, 0, read) |> ignore
//                if not UploadProgress = null then
//                    UploadProgress(this, new UploadProgressEventArgs { BytesSent = soFar, TotalBytesToSend = l });
                read <- h.Read(buffer, 0), buffer.Length

            stream.Flush()
            CopyStreamListTo t stream (Some bufferSize)

let UploadData imageStream fileName (uploadUri:string) parameters token tokenSecret= 
    let boundary = sprintf "FLICKR_MIME_%s" (DateTime.Now.ToString("yyyyMMddhhmmss", DateTimeFormatInfo.InvariantInfo))

    let authHeader = 
        let signingKey = OAuth.compositeSigningKey OAuth.consumerSecret tokenSecret
        let signingString = OAuth.baseString "POST" OAuth.accessTokenURI OAuth.BasicParameters
        let oauth_signature = OAuth.hmacsha1 signingKey signingString
        let realQueryParameters = ("oauth_signature", oauth_signature)::OAuth.BasicParameters
        OAuth.createAuthorizeHeader realQueryParameters

    let dataBuffer = CreateUploadData imageStream fileName parameters boundary tokenSecret
    
    let mutable req = HttpWebRequest.Create uploadUri
    req.Method <- "POST";
    //if not Proxy = null then req.Proxy <- Proxy
    req.Timeout <- 1000000;
    req.ContentType <- sprintf "multipart/form-data; boundary=%s" boundary

    if not (String.IsNullOrEmpty(authHeader)) then
        req.Headers.["Authorization"] <- authHeader

    req.ContentLength <- Convert.ToInt64 dataBuffer.Length

    use reqStream = req.GetRequestStream()
    let mutable bufferSize = 32 * 1024
    if dataBuffer.Length / 100 > bufferSize then bufferSize <- bufferSize * 2
    //dataBuffer.UploadProgress += (o, e) => (fun () -> if not OnUploadProgress = null then OnUploadProgress(this, e) )
    CopyStreamListTo dataBuffer reqStream (Some bufferSize) |> ignore
    reqStream.Flush()

    let res = req.GetResponse() :?> HttpWebResponse
    let stream = res.GetResponseStream()
    if stream = null then failwith "Unable to retrieve stream from web response."

    let sr = new StreamReader(stream)
    let s = sr.ReadToEnd()
    sr.Close()
    s
 
let UploadPicture stream (photo: Photo) = 
    let uploadUri = new Uri("https://up.flickr.com/services/upload")

    let parameters = 
        [ "title", photo.Title;
          "description", photo.Description;
          "is_public", photo.Visibility &&& PhotoVisibility.Public = PhotoVisibility.Public;
          "is_friend", photo.Visibility &&& PhotoVisibility.Friend = PhotoVisibility.Friend;
          "is_family", photo.Visibility &&& PhotoVisibility.Family = PhotoVisibility.Family;
          "safety_level", sprintf "%d" 1;
          "content_type", match photo with
                          | :? Video -> 3
                          | :? Photo -> 1
                          | _ -> invalidArg photo "Can only upload photos and videos" ;
          "hidden", sprintf "%d" 1; // This means it will appear in public searches
        ]

    if (!String.IsNullOrEmpty(OAuthAccessToken))
    {
        OAuthGetBasicParameters(parameters);
        parameters.Add("oauth_token", OAuthAccessToken);

        string sig = OAuthCalculateSignature("POST", uploadUri.AbsoluteUri, parameters, OAuthAccessTokenSecret);
        parameters.Add("oauth_signature", sig);
    }
    else
    {
        parameters.Add("api_key", apiKey);
        parameters.Add("auth_token", apiToken);
    }

    string responseXml = UploadData(stream, fileName, uploadUri, parameters);

    var settings = new XmlReaderSettings {IgnoreWhitespace = true};
    var reader = XmlReader.Create(new StringReader(responseXml), settings);

    if (!reader.ReadToDescendant("rsp"))
    {
        throw new XmlException("Unable to find response element 'rsp' in Flickr response");
    }
    while (reader.MoveToNextAttribute())
    {
        if (reader.LocalName == "stat" && reader.Value == "fail")
            throw ExceptionHandler.CreateResponseException(reader);
    }

    reader.MoveToElement();
    reader.Read();

    var t = new UnknownResponse();
    ((IFlickrParsable) t).Load(reader);
    return t.GetElementValue("photoid");
