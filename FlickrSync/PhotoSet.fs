module PhotoContainers

    type PhotoVisibility = 
    | Private
    | Public
    | Family
    | Friends
    | FamilyAndFriends

    type Photo(id, title, des, ispublic, isfriend, isfamily) = 
        member val Id = id
        member val Title = title
        member val Description = des
        member val Visibility = match (ispublic, isfriend, isfamily) with 
                                | (true, _, _)          -> Public
                                | (false, true, false)  -> Friends
                                | (false, false, true)  -> Family
                                | (false, true, true)   -> FamilyAndFriends
                                | (false, false, false) -> Private

    type PhotoSet(id, title, des, primary, photos, videos, visibility_can_see_set) = 
        member val Id = id
        member val Title = title
        member val Description = des
        member val Primary = primary
        member val Visibility = visibility_can_see_set
        member val Photos = photos
        member val Videos = videos