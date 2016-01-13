﻿using Hi.UrlRewrite.Caching;
using Hi.UrlRewrite.Entities.Rules;
using Hi.UrlRewrite.Processing.Results;
using Sitecore.Data;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.SecurityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace Hi.UrlRewrite.Processing
{
    public class InboundRewriteProcessor : HttpRequestProcessor
    {

        public override void Process(HttpRequestArgs args)
        {

            var db = Sitecore.Data.Database.GetDatabase("web"); // Sitecore.Context.Database;
            Log.Info(this, db, "InboundRewriteProcessor Process");
            try
            {

                if (args.Context == null || db == null) return;

                var httpContext = new HttpContextWrapper(args.Context);
                var requestUri = httpContext.Request.Url;

                if (requestUri == null || Configuration.IgnoreUrlPrefixes.Length > 0 && Configuration.IgnoreUrlPrefixes.Any(prefix => requestUri.PathAndQuery.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return;
                }
                
                var urlRewriter = new InboundRewriter(httpContext.Request.ServerVariables, httpContext.Request.Headers);

                Log.Info(this, db, "InboundRewriteProcessor Process ProcessUri {0}", requestUri);

                var requestResult = ProcessUri(requestUri, db, urlRewriter);

                if (requestResult == null || !requestResult.MatchedAtLeastOneRule) return;

                httpContext.Items["urlrewrite:db"] = db.Name;
                httpContext.Items["urlrewrite:result"] = requestResult;

                var urlRewriterItem = Sitecore.Context.Database.GetItem(new ID(Constants.UrlRewriter_ItemId));
                if (urlRewriterItem != null)
                {
                    Sitecore.Context.Item = urlRewriterItem;
                }
                else
                {
                    Log.Warn(this, db, "Unable to find UrlRewriter item {0}.", Constants.UrlRewriter_ItemId);
                }
            }
            catch (ThreadAbortException)
            {
                // swallow this exception because we may have called Response.End
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException) return;

                Log.Error(this, ex, db, "Exception occured");
            }
        }

        internal ProcessInboundRulesResult ProcessUri(Uri requestUri, Database db, InboundRewriter urlRewriter)
        {
            Log.Info(this, db, "ProcessUri");
            var inboundRules = GetInboundRules(db);

            if (inboundRules == null)
            {
                return null;
            }

            return urlRewriter.ProcessRequestUrl(requestUri, inboundRules);
        }

        private List<InboundRule> GetInboundRules(Database db)
        {
            Log.Info(this, db, "Start GetInboundRules.");
            var cache = RulesCacheManager.GetCache(db);
            var inboundRules = cache.GetInboundRules();
            Log.Info(this, db, "Stop GetInboundRules.");
            if (inboundRules != null)
            {
                Log.Info(this, db, "inboundRules!=null");
                Log.Info(this, db, "inboundRules.Any()=={0}", inboundRules.Any());
                if (inboundRules.Any())
                {
                    Log.Info(this, db, "inboundRules.Count()=={0}", inboundRules.Count);
                }
                return inboundRules;
            }
            else
            {
                Log.Info(this, db, "inboundRules==null");
            }

            Log.Info(this, db, "Initializing Inbound Rules.");

            using (new SecurityDisabler())
            {
                var rulesEngine = new RulesEngine(db);
                inboundRules = rulesEngine.GetCachedInboundRules();
            }

            return inboundRules;
        }
    }
}
