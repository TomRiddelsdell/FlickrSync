module Upload

open System
open System.Net
open System.IO
open System.Text
open System.Globalization

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

let CreateUploadData imageStream fileName  (parameters:Map<string,string>) boundary (sharedSecret:string) = 
    let keys = [for key in parameters-> key.Key]
               |> List.sort

    let mutable hashStringBuilder = new StringBuilder(sharedSecret, 2 * 1024);
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

let UploadPhoto path setName = 
    let request = FtpWebRequest.Create("https://up.flickr.com/services/upload") :?> FtpWebRequest
    request.Credentials <- new NetworkCredential("...", "...")
    request.Method <- WebRequestMethods.Http.Post
    request.Timeout <- 1000000

    let stream = File.OpenRead(path)
    use targetStream = new MemoryStream()
    stream.CopyTo(targetStream)
    stream.Close()

    let reqStream = request.GetRequestStream()
    let buffer = targetStream.GetBuffer()
    reqStream.Write(buffer, 0, buffer.Length)
    reqStream.Close()

    use response = request.GetResponse()
    use responseStream = response.GetResponseStream()
    use reader = new StreamReader(responseStream)
    reader.ReadToEnd()

let UploadData imageStream fileName uploadUri parameters = 
    let boundary = sprintf "FLICKR_MIME_%s" (DateTime.Now.ToString("yyyyMMddhhmmss", DateTimeFormatInfo.InvariantInfo))

    let authHeader = OAuth.createAuthorizeHeader 
    let dataBuffer = CreateUploadData imageStream fileName parameters boundary
    
    let mutable req = WebRequest.Create(uploadUri) :> HttpWebRequest
    req.Method <- "POST";
    if not Proxy = null then req.Proxy <- Proxy
    req.Timeout <- HttpTimeout;
    req.ContentType <- sprintfn "multipart/form-data; boundary=%s" boundary

    if not (String.IsNullOrEmpty(authHeader)) then
        req.Headers["Authorization"] <- authHeader

    req.ContentLength <- dataBuffer.Length

    use reqStream = req.GetRequestStream()
    let mutable bufferSize = 32 * 1024
    if dataBuffer.Length / 100 > bufferSize then bufferSize <- bufferSize * 2
    dataBuffer.UploadProgress += (o, e) => (fun () -> if not OnUploadProgress = null then OnUploadProgress(this, e) )
    dataBuffer.CopyTo(reqStream, bufferSize)
    reqStream.Flush()

    let res = req.GetResponse() :?> HttpWebResponse
    let stream = res.GetResponseStream()
    if stream = null then failwith "Unable to retrieve stream from web response."

    let sr = new StreamReader(stream)
    let s = sr.ReadToEnd()
    sr.Close()
    s
 
