module FlickrParser

    open FSharp.Data
 
    // Sample calendar data used by the type provider below for structure
    [<Literal>]
    let setsSample =
        """<rsp stat="ok">
            <photosets cancreate="1" page="1" pages="1" perpage="13" total="13">
                <photoset id="72157651586794409a" primary="17090490017" secret="c5abd40132" server="7701" farm="8" photos="37" videos="0" needs_interstitial="0" visibility_can_see_set="1" count_views="0" count_comments="0" can_comment="1" date_create="1429222948" date_update="1430205245">
                    <title>Auto Sync</title>
                    <description>bla</description>
                </photoset>
                <photoset id="72157652014220202a" primary="16987619190" secret="ae4f81c174" server="8771" farm="9" photos="241" videos="5" needs_interstitial="0" visibility_can_see_set="1" count_views="0" count_comments="0" can_comment="1" date_create="1429335697" date_update="1429600116">
                    <title>2014-Cairngorms</title>
                    <description>bla</description>
                </photoset>
            </photosets>
        </rsp>"""
 
    [<Literal>]
    let setSample =
        """<rsp stat="ok">
              <photoset id="72157652014220202a" primary="16987619190" owner="131546405@N02" ownername="t.riddelsdell" page="1" per_page="500" perpage="500" pages="1" total="246" title="2014-Cairngorms">
                <photo id="16987619190a" secret="ae4f81c174" server="8771" farm="9" title="GoPro_20140216 125" isprimary="1" ispublic="0" isfriend="1" isfamily="1" />
                <photo id="16987622380a" secret="6e432dd9ff" server="7700" farm="8" title="GoPro_20140216 124" isprimary="0" ispublic="0" isfriend="1" isfamily="1" />
              </photoset>
            </rsp>"""
 
    type setsDataProv = XmlProvider<setsSample>
    type setDataProv = XmlProvider<setSample>
 
    let ParseSets str =
        let response = setsDataProv.Parse str
        response.Photosets.Photosets
 
    let ParseSet str =
        let response = setDataProv.Parse str
        response.Photoset.Photos