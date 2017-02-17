using Newtonsoft.Json;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data.Items;
using Sitecore.Shell.Applications.ContentEditor.Gutters;
using System;
using System.Linq;
using System.Net;

namespace Sitecore.RP.Utility
{
    public class CheckIndexCreated : GutterRenderer
    {
        private int IsConflict { get; set; }
        private bool IsTimeZoneDifferent { get; set; }

        public SearchResultItem GetSkinnyItemsByRelatedIDs(Item item, string hostAddress, string indexName)
        {

            var searchIndex = ContentSearchManager.GetIndex(indexName);

            using (var context = searchIndex.CreateSearchContext())
            {
                var query = context.GetQueryable<SearchResultItem>();
                var lang = item.Language.Name.ToLower();
                query = query.Where(p => p.ItemId == item.ID && p.Language.Equals(lang));

                var results = query.GetResults();

                return results.Hits.Select(h => h.Document).FirstOrDefault();
            }
        }

        protected override GutterIconDescriptor GetIconDescriptor(Item item)
        {
            if (item.Database.Name != "master" || !(item.Paths.IsContentItem || item.Paths.IsMediaItem)) return null;
            IsConflict = 1;
            IsTimeZoneDifferent = false;
            var searchItem = DownloadIndexingDetails(item);

            if (string.IsNullOrEmpty(searchItem))
            {
                return null;
            }

            if (IsTimeZoneDifferent)
            {
                searchItem = " Indexes are in a different time zones." + " Sitecore time zone is " + TimeZone.CurrentTimeZone.StandardName + "." + searchItem;
            }

            string toolTip = " " + searchItem;
            var icon = "";

            switch (IsConflict)
            {
                case 1: // all indexes have same updated date
                    icon = "Applications/32x32/harddisk_network_ok.png";
                    toolTip = "All indexes are up to date.";
                    break;
                case 2: // differnce in updated date between indexes or between item and indexed item
                    icon = "Applications/32x32/harddisk_network_warning.png";
                    break;
                case 3: // some of indexes not created
                    icon = "Applications/32x32/harddisk_network_error.png";
                    break;
            }

            return new GutterIconDescriptor()
            {
                Icon = icon,
                Tooltip = toolTip
                //Click = $"item:index(id={item.ID})"
            };
        }

        private class ReturnJsonObject
        {
            public string returnbool { get; set; }
            public DateTime IndexDate { get; set; }

            public string ServerTimeZone { get; set; }
        }

        private string DownloadIndexingDetails(Item item)
        {
            string str = "";
            var sitecoreSettings =
                Context.ContentDatabase.GetItem("/sitecore/system/Modules/ItemIndexViewerSettings");
            // if setting through sitecore is not configured 
            if (sitecoreSettings?.Children == null || sitecoreSettings.Children.Count == 0)
            {
                var indexName = "sitecore_master_index";
                // this will return result for default sitecore index
                return GetIndexDetails(item, "", indexName);
            }

            var childItems = sitecoreSettings.Children;

            foreach (Item child in childItems)
            {
                if (!child.TemplateID.Equals(Data.ID.Parse("{BF8ED5EA-4ECB-4E66-96D1-D8225AA2B235}")))
                {
                    continue;
                }
                var hostAddress = child.Fields["HostAddress"].Value;
                var indexName = child.Fields["Index Name"].Value;

                if (string.IsNullOrEmpty(hostAddress) && string.IsNullOrEmpty(indexName))
                {
                    continue;
                }
                str = str + Environment.NewLine + GetIndexDetails(item, hostAddress, indexName);

            }
            return str;
        }

        private string firstIndexDateTime = "";
        private string GetIndexDetails(Item item, string hostAddress, string indexName)
        {
            var timezone = TimeZone.CurrentTimeZone;
            var itemzoneInfo = timezone.StandardName;
            var itemzoneforEachindex = "";
            if (!string.IsNullOrEmpty(hostAddress))
            {

                if (string.IsNullOrEmpty(indexName))
                {
                    IsConflict = 3;
                    return "Index name is not set for host (" + hostAddress + ").";
                }
                var strmessage = "Item is Index for host(" + hostAddress + ") ";

                var indexingMappingUrl =
                    string.Format(
                        "{0}/api/sitecore/RpSitecoreIndexe/GetSearchIndexJsonResult?itemid={1}&languagename={2}&searchindexname={3}",
                        hostAddress, item.ID.ToString(), item.Language.Name.ToLower(), indexName);


                DateTime strList = DateTime.MinValue;


                using (var webClient = new WebClient())
                {
                    var json = webClient.DownloadString(indexingMappingUrl);
                    if (json.Length > 0)
                    {
                        var desresult = JsonConvert.DeserializeObject<ReturnJsonObject>(json);
                        if (desresult.returnbool == "1")
                        {
                            strList = ServerTime(desresult.IndexDate);
                            itemzoneforEachindex = desresult.ServerTimeZone;
                            SetIconValuesMismatch(item, desresult.IndexDate);
                            if (!IsTimeZoneDifferent)
                                IsTimeZoneDifferent = itemzoneInfo != desresult.ServerTimeZone;
                        }
                        else
                        {
                            IsConflict = 3;
                            return "No Item is indexed for host (" + hostAddress + ") where index name is '" + indexName + "'." + AppendTimeZone(itemzoneforEachindex);
                        }
                    }
                }
                return strmessage + "where index name is '" + indexName + "'. Last updated date for an item in index is " + strList + "." + AppendTimeZone(itemzoneforEachindex); ;
            }

            var searchItem = GetSkinnyItemsByRelatedIDs(item, "", indexName);

            if (searchItem == null)
            {
                IsConflict = 3;
                return "No item is indexed for '" + indexName + "'.";
            }
            SetIconValuesMismatch(item, searchItem.Updated);

            var strmessage1 = "Item is indexed for '" + indexName + "'. Last updated date for an item in index is " +
                              ServerTime(searchItem.Updated) + "." + AppendTimeZone(TimeZoneInfo.Local.StandardName);

            return strmessage1;

        }

        private string AppendTimeZone(string timezone)
        {
            if (IsTimeZoneDifferent)
            {
                return " Time zone : " + timezone;
            }
            return "";

        }

        private void SetIconValuesMismatch(Item item, DateTime dateTime)
        {
            var strList1 = ServerTime(dateTime).ToString("yyyyMMddHHmm");

            Sitecore.Data.Fields.DateField updatedField = item.Fields[Sitecore.FieldIDs.Updated];
            var itemUpdateTime1 = ServerTime(updatedField.DateTime).ToString("yyyyMMddHHmm");
            if (string.IsNullOrEmpty(firstIndexDateTime))
            {
                firstIndexDateTime = strList1;
            }
            else if (itemUpdateTime1 != firstIndexDateTime || strList1 != firstIndexDateTime)
            {
                IsConflict = 2;
            }
        }

        private DateTime ServerTime(DateTime timeToConvert)
        {
            //return timeToConvert;
            return
                DateTime.SpecifyKind(
                    TimeZoneInfo.ConvertTimeFromUtc(timeToConvert.ToUniversalTime(), TimeZoneInfo.Local),
                    DateTimeKind.Unspecified);
        }
    }
}
