module AlbumComparison

open PhotoContainers
open System.Text.RegularExpressions

(* Strategy for matching album names:
    - Check if both names are of the form "[0-9]+ +- +.*"
    - If matches (date,strname) then match date from left digit to right, allowing differing precision
    - If date matches or no date portion then 
      - check if flickr name contains local name and visa versa
      - remove any matched portion and check the length of unmatched part is less than matched part
*)

let (|MatchAlbumNameNoDate|_|) (flickrName : string) (localName : string) = 
    let fn = flickrName.ToLower()
    let ln = localName.ToLower()
    let mchs =  if fn.Contains(ln) then
                    fn.Length - fn.Replace(ln,"").Length > 0
                elif ln.Contains(fn) then
                    ln.Length - ln.Replace(fn,"").Length > 0
                else
                    false
    if mchs then Some flickrName else None

let (|MatchAlbumDate|_|) (flickrDate: string) (localDate : string) = 
    let (long,short) =  if flickrDate.Length > localDate.Length then (flickrDate,localDate)
                        else (localDate,flickrDate)
    if  [for i in 0..long.Length-1 -> short.Length<i+1 || short.[i] = long.[i]]
        |> List.fold (&&) true
    then 
        Some flickrDate
    else 
        None

let (|MatchAlbumName|_|) (localName : string) (flickrName : string) = 
    let mf = Regex("([0-9]+)-(.*)").Match(flickrName.Replace(" ",""))
    let ml = Regex("([0-9]+)-(.*)").Match(localName.Replace(" ",""))
    if mf.Success && ml.Success then
        let fd = mf.Groups.[1].Value 
        let ld = ml.Groups.[1].Value
        let fn = mf.Groups.[2].Value 
        let ln = ml.Groups.[2].Value 
        match fd with
        | MatchAlbumDate ld x -> match fn with 
                                 | MatchAlbumNameNoDate ln y -> Some flickrName
                                 | _ -> None
        | _ -> None
    else 
        match flickrName with 
        | MatchAlbumNameNoDate localName y -> Some flickrName
        | _ -> None
        
let (|Missing|Matching|) (albumArr: Album array) (album: Album)= 
    albumArr
    |> Array.filter (fun a -> match a.Title with
                              | MatchAlbumName album.Title x -> true
                              | _ -> false )
    |> fun rem -> match rem with 
                  | [| |] -> Missing
                  | items -> Matching

let MissingAlbums (srcAlbums : Album array) (tgtAlbums : Album array) : Album array = 
    let (|Missing_|Matching_|) = (|Missing|Matching|) tgtAlbums

    srcAlbums
    |> Array.filter (fun album -> match album with 
                                  | Matching_ -> false
                                  | Missing_ -> true )
                                  
let Partition (srcAlbums : Album array) (tgtAlbums : Album array) : (Album array * Album array) = 
    let (|Missing_|Matching_|) = (|Missing|Matching|) tgtAlbums

    srcAlbums
    |> Array.partition (fun album -> match album with 
                                     | Matching_ -> true
                                     | Missing_ -> false )
