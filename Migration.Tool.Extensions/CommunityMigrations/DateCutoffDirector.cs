using Microsoft.Data.SqlClient;
using Migration.Tool.Source;
using Migration.Tool.Source.Mappers.ContentItemMapperDirectives;
using Migration.Tool.Source.Model;

namespace Migration.Tool.Extensions.CommunityMigrations;

/// <summary>
/// Filters migration to only include content items of specific types
/// that were created on or after the cutoff date (July 22, 2025).
/// All other content types pass through unaffected.
/// </summary>
public class DateCutoffDirector(ModelFacade modelFacade) : ContentItemDirectorBase
{
    private static readonly DateTime CutoffDate = new(2025, 7, 22);

    private static readonly HashSet<string> TargetClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Convenience.CommitteeDocument",
        "Convenience.CoolNewProduct",
        "Convenience.MemberNews",
        "Convenience.NewsArticle",
        "Convenience.Page",
        "Convenience.Video",
        "Magazine.Article",
        "Magazine.Author",
        "Magazine.Issue"
    };

    public override void Direct(ContentItemSource source, IContentItemActionProvider options)
    {
        if (!TargetClassNames.Contains(source.SourceClassName))
        {
            return;
        }

        if (source.SourceNode is null)
        {
            options.Drop();
            return;
        }

        var documents = modelFacade.SelectWhere<ICmsDocument>(
            "DocumentNodeID = @nodeId",
            new SqlParameter("@nodeId", source.SourceNode.NodeID));

        if (!documents.Any(d => d.DocumentCreatedWhen >= CutoffDate))
        {
            options.Drop();
        }
    }
}
