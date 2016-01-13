﻿using Hi.UrlRewrite.Caching;
using Hi.UrlRewrite.Entities.Actions;
using Hi.UrlRewrite.Entities.Conditions;
using Hi.UrlRewrite.Entities.Match;
using Hi.UrlRewrite.Entities.Rules;
using Hi.UrlRewrite.Templates.Folders;
using Hi.UrlRewrite.Templates.Inbound;
using Hi.UrlRewrite.Templates.Outbound;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Hi.UrlRewrite.Processing
{
    public class RulesEngine
    {

        private readonly Database db;

        public Database Database
        {
            get
            {
                return db;
            }
        }

        public RulesEngine(Database db)
        {
            this.db = db;
        }

        public List<InboundRule> GetInboundRules()
        {
            if (db == null)
            {
                return null;
            }
            var urlRewriteFolderItemId = new ID("{0DA6100D-E4FB-4ADD-AD4C-373F410A1952}");
            var redirectFolderItem = db.GetItem(urlRewriteFolderItemId); //GetRedirectFolderItems();

            if (redirectFolderItem == null)
            {
               return null;
            }
           
            var inboundRules = new List<InboundRule>();

                Log.Info(this, db, "Loading Inbound Rules from RedirectFolder: {0}", redirectFolderItem.Name);

                var folderDescendants = redirectFolderItem.Axes.GetDescendants()
                    .Where(x => x.TemplateID == new ID(new Guid(SimpleRedirectItem.TemplateId)) ||
                                x.TemplateID == new ID(new Guid(InboundRuleItem.TemplateId)));

                foreach (var descendantItem in folderDescendants)
                {
                    if (descendantItem.TemplateID == new ID(new Guid(SimpleRedirectItem.TemplateId)))
                    {
                        var simpleRedirectItem = new SimpleRedirectItem(descendantItem);

                        Log.Info(this, db, "Loading SimpleRedirect: {0}", simpleRedirectItem.Name);

                        var inboundRule = CreateInboundRuleFromSimpleRedirectItem(simpleRedirectItem, redirectFolderItem);

                        if (inboundRule != null && inboundRule.Enabled)
                        {
                            inboundRules.Add(inboundRule);
                        }
                    }
                    else if (descendantItem.TemplateID == new ID(new Guid(InboundRuleItem.TemplateId)))
                    {
                        var inboundRuleItem = new InboundRuleItem(descendantItem);

                        Log.Info(this, db, "Loading InboundRule: {0}", inboundRuleItem.Name);

                        var inboundRule = CreateInboundRuleFromInboundRuleItem(inboundRuleItem, redirectFolderItem);

                        if (inboundRule != null && inboundRule.Enabled)
                        {
                            inboundRules.Add(inboundRule);
                        }
                    }
                }

            return inboundRules;
        }

        public List<OutboundRule> GetOutboundRules()
        {
            if (db == null)
            {
                return null;
            }

            var redirectFolderItems = GetRedirectFolderItems();

            if (redirectFolderItems == null)
            {
                return null;
            }

            var outboundRules = new List<OutboundRule>();

            foreach (var redirectFolderItem in redirectFolderItems)
            {
                Log.Info(this, db, "Loading Outbound Rules from RedirectFolder: {0}", redirectFolderItem.Name);

                var folderDescendants = redirectFolderItem.Axes.GetDescendants()
                    .Where(x => x.TemplateID == new ID(new Guid(OutboundRuleItem.TemplateId)));

                foreach (var descendantItem in folderDescendants)
                {
                    if (descendantItem.TemplateID == new ID(new Guid(OutboundRuleItem.TemplateId)))
                    {
                        var outboundRuleItem = new OutboundRuleItem(descendantItem);

                        Log.Info(this, db, "Loading OutboundRule: {0}", outboundRuleItem.Name);

                        var outboundRule = CreateOutboundRuleFromOutboundRuleItem(outboundRuleItem, redirectFolderItem);

                        if (outboundRule != null && outboundRule.Enabled)
                        {
                            outboundRules.Add(outboundRule);
                        }
                    }
                }
            }

            return outboundRules;
        }

        private IEnumerable<Item> GetRedirectFolderItems()
        {
            Log.Info(this, db, "db.GetItem(RedirectFolderItem.TemplateId).GetReferrers(), TemplateId={0}", RedirectFolderItem.TemplateId);
            var redirectFolderItems = db.GetItem(RedirectFolderItem.TemplateId).GetReferrers();
            Log.Info(this, db, "redirectFolderItems count = {0}", redirectFolderItems.Length.ToString());
            return redirectFolderItems;
        }

        #region Serialization 
        internal InboundRule CreateInboundRuleFromSimpleRedirectItem(SimpleRedirectItem simpleRedirectItem, RedirectFolderItem redirectFolderItem)
        {
            var inboundRulePattern = string.Format("^{0}/?$", simpleRedirectItem.Path.Value);

            var siteNameRestriction = GetSiteNameRestriction(redirectFolderItem);

            var redirectTo = simpleRedirectItem.Target;
            string actionRewriteUrl;
            Guid? redirectItem;
            string redirectItemAnchor;

            GetRedirectUrlOrItemId(redirectTo, out actionRewriteUrl, out redirectItem, out redirectItemAnchor);

            Log.Debug(this, simpleRedirectItem.Database, "Creating Inbound Rule From Simple Redirect Item - {0} - id: {1} actionRewriteUrl: {2} redirectItem: {3}", simpleRedirectItem.Name, simpleRedirectItem.ID.Guid, actionRewriteUrl, redirectItem);

            var inboundRule = new InboundRule
            {
                Action = new Redirect
                {
                    AppendQueryString = true,
                    Name = "Redirect",
                    StatusCode = RedirectStatusCode.Permanent,
                    RewriteUrl = actionRewriteUrl,
                    RewriteItemId = redirectItem,
                    RewriteItemAnchor = redirectItemAnchor,
                    StopProcessingOfSubsequentRules = false,
                    HttpCacheability = HttpCacheability.NoCache
                },
                SiteNameRestriction = siteNameRestriction,
                Enabled = simpleRedirectItem.BaseEnabledItem.Enabled.Checked,
                IgnoreCase = true,
                ItemId = simpleRedirectItem.ID.Guid,
                ConditionLogicalGrouping = LogicalGrouping.MatchAll,
                Name = simpleRedirectItem.Name,
                Pattern = inboundRulePattern,
                MatchType = MatchType.MatchesThePattern,
                Using = Using.RegularExpressions
            };

            return inboundRule;
        }

        internal InboundRule CreateInboundRuleFromInboundRuleItem(InboundRuleItem inboundRuleItem, RedirectFolderItem redirectFolderItem)
        {
            var siteNameRestriction = GetSiteNameRestriction(redirectFolderItem);
            var inboundRule = inboundRuleItem.ToInboundRule(siteNameRestriction);

            return inboundRule;
        }

        internal OutboundRule CreateOutboundRuleFromOutboundRuleItem(OutboundRuleItem outboundRuleItem,
            RedirectFolderItem redirectFolderItem)
        {
            var outboundRule = outboundRuleItem.ToOutboundRule();

            return outboundRule;
        }

        internal static string GetSiteNameRestriction(RedirectFolderItem redirectFolder)
        {
            var site = redirectFolder.SiteNameRestriction.Value;

            return site;
        }

        internal static void GetRedirectUrlOrItemId(LinkField redirectTo, out string actionRewriteUrl, out Guid? redirectItemId, out string redirectItemAnchor)
        {
            actionRewriteUrl = null;
            redirectItemId = null;
            redirectItemAnchor = null;

            if (redirectTo.TargetItem != null)
            {
                redirectItemId = redirectTo.TargetItem.ID.Guid;
                redirectItemAnchor = redirectTo.Anchor;
            }
            else
            {
                actionRewriteUrl = redirectTo.Url;
            }
        }

        #endregion

        #region Caching

        private RulesCache GetRulesCache()
        {
            return RulesCacheManager.GetCache(db);
        }

        internal List<InboundRule> GetCachedInboundRules()
        {
            var inboundRules = GetInboundRules();

            if (inboundRules != null)
            {
                Log.Info(this, db, "Adding {0} rules to Cache [{1}]", inboundRules.Count(), db.Name);

                var cache = GetRulesCache();
                cache.SetInboundRules(inboundRules);
            }
            else
            {
                Log.Info(this, db, "Found no rules");
            }

            return inboundRules;
        }

        internal List<OutboundRule> GetCachedOutboundRules()
        {
            var outboundRules = GetOutboundRules();

            if (outboundRules != null)
            {
                Log.Info(this, db, "Adding {0} rules to Cache [{1}]", outboundRules.Count(), db.Name);

                var cache = GetRulesCache();
                cache.SetOutboundRules(outboundRules);
            }
            else
            {
                Log.Info(this, db, "Found no rules");
            }

            return outboundRules;
        }

        internal void RefreshRule(Item item, Item redirectFolderItem)
        {
            UpdateRulesCache(item, redirectFolderItem, AddRule);
        }

        internal void DeleteRule(Item item, Item redirectFolderItem)
        {
            UpdateRulesCache(item, redirectFolderItem, RemoveRule);
        }

        private void UpdateRulesCache(Item item, Item redirectFolderItem, Action<Item, Item, List<IBaseRule>> action)
        {
            var cache = GetRulesCache();
            IEnumerable<IBaseRule> baseRules = null;
            if (item.IsSimpleRedirectItem() || item.IsInboundRuleItem())
            {
                baseRules = cache.GetInboundRules();
                if (baseRules == null)
                {
                    baseRules = GetInboundRules();
                }
            }
            else if (item.IsOutboundRuleItem())
            {
                baseRules = cache.GetOutboundRules();
                if (baseRules == null)
                {
                    baseRules = GetOutboundRules();
                }
            }

            if (baseRules != null)
            {
                var rules = baseRules.ToList();

                action(item, redirectFolderItem, rules);

                Log.Debug(this, item.Database, "Updating Rules Cache - count: {0}", rules.Count());

                // update the cache
                if (item.IsSimpleRedirectItem() || item.IsInboundRuleItem())
                {
                    cache.SetInboundRules(rules.OfType<InboundRule>());
                }
                else if (item.IsOutboundRuleItem())
                {
                    cache.SetOutboundRules(rules.OfType<OutboundRule>());
                }

            }
        }

        private void AddRule(Item item, Item redirectFolderItem, List<IBaseRule> inboundRules)
        {
            IBaseRule newRule = null;

            Log.Debug(this, item.Database, "Adding Rule - item: [{0}]", item.Paths.FullPath);

            if (item.IsInboundRuleItem())
            {
                newRule = CreateInboundRuleFromInboundRuleItem(item, redirectFolderItem);
            }
            else if (item.IsSimpleRedirectItem())
            {
                newRule = CreateInboundRuleFromSimpleRedirectItem(item, redirectFolderItem);
            }
            else if (item.IsOutboundRuleItem())
            {
                newRule = CreateOutboundRuleFromOutboundRuleItem(item, redirectFolderItem);
            }

            if (newRule != null)
            {
                AddOrRemoveRule(item, redirectFolderItem, inboundRules, newRule);
            }
        }

        private void AddOrRemoveRule(Item item, Item redirectFolderItem, List<IBaseRule> rules, IBaseRule newRule)
        {
            if (newRule.Enabled)
            {
                var existingRule = rules.FirstOrDefault(e => e.ItemId == item.ID.Guid);
                if (existingRule != null)
                {

                    Log.Debug(this, item.Database, "Replacing Rule - item: [{0}]", item.Paths.FullPath);

                    var index = rules.FindIndex(e => e.ItemId == existingRule.ItemId);
                    rules.RemoveAt(index);
                    rules.Insert(index, newRule);
                }
                else
                {

                    Log.Debug(this, item.Database, "Adding Rule - item: [{0}]", item.Paths.FullPath);

                    rules.Add(newRule);
                }
            }
            else
            {
                RemoveRule(item, redirectFolderItem, rules);
            }
        }

        private void RemoveRule(Item item, Item redirectFolderItem, List<IBaseRule> inboundRules)
        {
            Log.Debug(this, item.Database, "Removing Rule - item: [{0}]", item.Paths.FullPath);

            var existingInboundRule = inboundRules.FirstOrDefault(e => e.ItemId == item.ID.Guid);
            if (existingInboundRule != null)
            {
                inboundRules.Remove(existingInboundRule);
            }
        }

        #endregion

    }
}
