module PhotoContainers

    type PhotoVisibility = 
    | Public            = 1
    | Friends           = 2
    | Family            = 3
    | FamilyAndFriends  = 4
    | Private           = 5

    type Photo(id, title, des, ispublic, isfriend, isfamily) = 
        member val Id : string= id
        member val Title : string = title
        member val Description : string = des
        member val Visibility = match (ispublic, isfriend, isfamily) with 
                                | (true, _, _)          -> PhotoVisibility.Public
                                | (false, true, false)  -> PhotoVisibility.Friends
                                | (false, false, true)  -> PhotoVisibility.Family
                                | (false, true, true)   -> PhotoVisibility.FamilyAndFriends
                                | (false, false, false) -> PhotoVisibility.Private

    type Video(id, title, des, ispublic, isfriend, isfamily) = 
        inherit Photo(id, title, des, ispublic, isfriend, isfamily) 

    type Album(id, title, des, primary, photos, videos, visibility) = 
        member val Id : string = id
        member val Title : string = title
        member val Description : string = des
        member val Primary = primary
        member val Visibility = visibility
        member val Photos : Photo array = photos
        member val Videos : Video array = videos