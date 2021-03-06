using System;
using System.Linq;
using System.Threading.Tasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using Newtonsoft.Json.Linq;
using YesSql;
using Microsoft.Extensions.Logging;

namespace OrchardCore.Title
{
    public class Migrations : DataMigration
    {
        IContentDefinitionManager _contentDefinitionManager;
        private readonly ISession _session;
        private readonly ILogger<Migrations> _logger;

        public Migrations(
            IContentDefinitionManager contentDefinitionManager, 
            ISession session,
            ILogger<Migrations> logger)
        {
            _contentDefinitionManager = contentDefinitionManager;
            _session = session;
            _logger = logger;
        }

        public int Create()
        {
            _contentDefinitionManager.AlterPartDefinition("TitlePart", builder => builder
                .Attachable()
                .WithDescription("Provides a Title for your content item.")
                .WithDefaultPosition("0")
                );

            return 2;
        }
        
        public async Task<int> UpdateFrom1()
        {
            // This code can be removed in RC
            // We are patching all content item versions by moving the Title to DisplayText
            // This step doesn't need to be executed for a brand new site

            var lastDodcumentId = 0;

            for(;;)
            {
                var contentItemVersions = await _session.Query<ContentItem, ContentItemIndex>(x => x.DocumentId > lastDodcumentId).Take(10).ListAsync();
                
                if (!contentItemVersions.Any())
                {
                    // No more content item version to process
                    break;
                }

                foreach(var contentItemVersion in contentItemVersions)
                {
                    if (UpdateTitleAndBody(contentItemVersion.Content))
                    {
                        _session.Save(contentItemVersion);
                        _logger.LogInformation($"A content item version's Title was upgraded: '{contentItemVersion.ContentItemVersionId}'");
                    }

                    lastDodcumentId = contentItemVersion.Id;
                }

                await _session.CommitAsync();
            } 

            bool UpdateTitleAndBody(JToken content)
            {
                var changed = false;

                if (content.Type == JTokenType.Object)
                {
                    var title = content["TitlePart"] ? ["Title"]?.Value<string>();
                    
                    if (!String.IsNullOrWhiteSpace(title))
                    {
                        content["DisplayText"] = title;
                        changed = true;
                    }
                }

                foreach (var token in content)
                {
                    changed = UpdateTitleAndBody(token) || changed;
                }

                return changed;
            }

            return 2;
        }
    }
}