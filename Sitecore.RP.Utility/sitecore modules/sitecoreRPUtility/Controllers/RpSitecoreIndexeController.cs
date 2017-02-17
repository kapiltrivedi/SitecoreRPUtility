using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Mvc.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Sitecore.RP.Utility.sitecore_modules.sitecoreRPUtility.Controllers
{
    public class RpSitecoreIndexeController : SitecoreController
    {
        public JsonResult GetSearchIndexJsonResult(string itemId, string languageName, string searchIndexName)
        {
            if (itemId == null) throw new ArgumentNullException(nameof(itemId));
            if (languageName == null) throw new ArgumentNullException(nameof(languageName));
            if (searchIndexName == null) throw new ArgumentNullException(nameof(searchIndexName));
            var returnMessage1 = "UnSuccessful";
            var searchResultItem = GetSearchResultItem(itemId, languageName, searchIndexName);
            var newmodel = new
            {
                returnMessage = returnMessage1,
                returnbool = 0,
                IndexDate = DateTime.MinValue
            };
            if (searchResultItem == null || searchResultItem.Count <= 0)
                return Json(newmodel, JsonRequestBehavior.AllowGet);
            SearchResultItem resultItem = searchResultItem.FirstOrDefault();

            var timezoneInfo = TimeZone.CurrentTimeZone;

            var newmodel1 = new { returnMessage = "Successful", returnbool = 1, IndexDate = resultItem.Updated.ToUniversalTime(), ServerTimeZone = timezoneInfo.StandardName };
            return Json(newmodel1, JsonRequestBehavior.AllowGet);
        }

        public ISearchIndex GetSearchIndex(string searchIndexName)
        {
            string indexName = searchIndexName;
            ISearchIndex index = ContentSearchManager.GetIndex(indexName);
            return index;
        }

        public List<SearchResultItem> GetSearchResultItem(string itemId, string languageName, string searchIndexName)
        {
            var ItemIdGuid = Sitecore.Data.ID.Parse(itemId);
            var searchIndex = GetSearchIndex(searchIndexName);
            using (var context = searchIndex.CreateSearchContext())
            {
                var query = context.GetQueryable<SearchResultItem>();
                var lang = languageName.ToLower();
                query = query.Where(p => p.ItemId == ItemIdGuid && p.Language.Equals(lang));

                var results = query.GetResults();

                return results.Hits.Select(h => h.Document).ToList();
            }
        }
    }
}