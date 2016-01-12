﻿using Hi.UrlRewrite.Templates.Folders;
using Hi.UrlRewrite.Templates.Inbound;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Events;
using Sitecore.SecurityModel;
using System;
using System.Linq;

namespace Hi.UrlRewrite.Processing
{
    public class UrlRewriteItemEventHandler
    {

        #region ItemSaved

        public void OnItemSaved(object sender, EventArgs args)
        {
            var item = Event.ExtractParameter(args, 0) as Item;
            var itemChanges = Event.ExtractParameter(args, 1) as ItemChanges;

            if (item == null)
            {
                return;
            }

            RunItemSaved(item, itemChanges);
        }

        public void OnItemSavedRemote(object sender, EventArgs args)
        {
            var itemSavedRemoteEventArg = args as ItemSavedRemoteEventArgs;
            if (itemSavedRemoteEventArg == null)
            {
                return;
            }

            var itemId = itemSavedRemoteEventArg.Item.ID;
            var db = itemSavedRemoteEventArg.Item.Database;
            Item item;

            using (new SecurityDisabler())
            {
                item = db.GetItem(itemId);
            }

            RunItemSaved(item, itemSavedRemoteEventArg.Changes);
        }

        private void RunItemSaved(Item item, ItemChanges itemChanges)
        {
            var db = item.Database;
            var rulesEngine = new RulesEngine(db);

            try
            {
                using (new SecurityDisabler())
                {
                    var redirectFolderItem = item.Axes.GetAncestors()
                        .FirstOrDefault(a => a.TemplateID.Equals(new ID(RedirectFolderItem.TemplateId)));

                    if (redirectFolderItem == null) return;

                    if (item.IsRedirectFolderItem())
                    {
                        Log.Info(this, db, "Refreshing Redirect Folder [{0}] after save event", item.Paths.FullPath);

                        rulesEngine.GetCachedInboundRules();
                    }
                    else if (item.IsOutboundRuleItem())
                    {
                        Log.Info(this, db, "Refreshing Outbound Rule [{0}] after save event", item.Paths.FullPath);

                        rulesEngine.RefreshRule(item, redirectFolderItem);
                    }
                    else if (item.IsSimpleRedirectItem())
                    {
                        Log.Info(this, db, "Refreshing Simple Redirect [{0}] after save event", item.Paths.FullPath);

                        rulesEngine.RefreshRule(item, redirectFolderItem);
                    }
                    else if (item.IsInboundRuleItem())
                    {
                        Log.Info(this, db, "Refreshing Inbound Rule [{0}] after save event", item.Paths.FullPath);

                        rulesEngine.RefreshRule(item, redirectFolderItem);
                    }
                    else if (item.IsRedirectType() && item.IsInboundRuleItemChild() && db.Name.Equals("web", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var inboundRuleItem = item.Parent;
                        var inboundRule = new InboundRuleItem(inboundRuleItem);

                        inboundRule.BeginEdit();
                        inboundRule.Action.InnerField.SetValue(item.ID.ToString(), false);
                        inboundRule.EndEdit();
                    }
                    else if (item.IsInboundRuleItemChild())
                    {
                        Log.Info(this, db, "Refreshing Inbound Rule [{0}] after save event", item.Parent.Paths.FullPath);

                        rulesEngine.RefreshRule(item.Parent, redirectFolderItem);
                    }
                    else if (item.IsOutboundRuleItemChild())
                    {
                        Log.Info(this, db, "Refreshing Outbound Rule [{0}] after save event", item.Parent.Paths.FullPath);

                        rulesEngine.RefreshRule(item.Parent, redirectFolderItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(this, ex, db, "Exception occured when saving item after save - Item ID: {0} Item Path: {1}", item.ID, item.Paths.FullPath);
            }

        }

        #endregion

        #region ItemDeleted

        public void OnItemDeleted(object sender, EventArgs args)
        {
            var item = Event.ExtractParameter(args, 0) as Item;
            if (item == null)
            {
                return;
            }

            RunItemDeleted(item);
        }

        public void OnItemDeletedRemote(object sender, EventArgs args)
        {
            var itemDeletedRemoteEventArg = args as ItemDeletedRemoteEventArgs;
            if (itemDeletedRemoteEventArg == null)
            {
                return;
            }

            RunItemDeleted(itemDeletedRemoteEventArg.Item);
        }

        private void RunItemDeleted(Item item)
        {

            var rulesEngine = new RulesEngine(item.Database);

            try
            {

                using (new SecurityDisabler())
                {

                    if (item.IsInboundRuleItem() || item.IsSimpleRedirectItem() || item.IsInboundRuleItemChild())
                    {
                        Log.Info(this, item.Database, "Refreshing inbound rules cache after delete event",
                            item.Paths.FullPath);

                        rulesEngine.GetCachedInboundRules();
                    }
                    else if (item.IsOutboundRuleItem() || item.IsOutboundRuleItemChild())
                    {
                        Log.Info(this, item.Database, "Refreshing outbound rules cache after delete event",
                            item.Parent.Paths.FullPath);

                        rulesEngine.GetCachedOutboundRules();
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Error(this, ex, item.Database, "Exception occured which deleting item after publish Item ID: {0} Item Path: {1}", item.ID, item.Paths.FullPath);
            }
        }

        #endregion

    }
}
