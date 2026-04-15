using MediatR;
using Microsoft.Data.SqlClient;
using Migration.Tool.Common;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Source.Model;
using Migration.Tool.Source.Services;

namespace Migration.Tool.Source.Handlers;

// ReSharper disable once UnusedMember.Global [implicit use]
public class MigrateAttachmentsCommandHandler(
    ModelFacade modelFacade,
    IAttachmentMigrator attachmentMigrator
) : IRequestHandler<MigrateAttachmentsCommand, CommandResult>
{
    // NACS: Only migrate attachments whose parent document was created on or after the cutoff date.
    // Set to null to disable filtering and migrate all attachments.
    private static readonly DateTime? CutoffDate = new(2025, 7, 22);

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

    public async Task<CommandResult> Handle(MigrateAttachmentsCommand request, CancellationToken cancellationToken)
    {
        var ksCmsAttachments = modelFacade.SelectAll<ICmsAttachment>();

        foreach (var ksCmsAttachment in ksCmsAttachments)
        {
            if (ksCmsAttachment.AttachmentIsUnsorted != true || ksCmsAttachment.AttachmentGroupGUID != null)
            {
                // those must be migrated with pages
                continue;
            }

            if (CutoffDate.HasValue && ksCmsAttachment.AttachmentDocumentID is { } docId)
            {
                var document = modelFacade.SelectById<ICmsDocument>(docId);
                if (document is null)
                {
                    continue;
                }

                // Check if parent document's content type is in scope
                var node = modelFacade.SelectById<ICmsTree>(document.DocumentNodeID);
                if (node is not null)
                {
                    var nodeClass = modelFacade.SelectById<ICmsClass>(node.NodeClassID);
                    if (nodeClass is not null && !TargetClassNames.Contains(nodeClass.ClassName))
                    {
                        continue;
                    }
                }

                // Check if parent document was created before cutoff
                if (document.DocumentCreatedWhen < CutoffDate.Value)
                {
                    continue;
                }
            }

            await attachmentMigrator.MigrateAttachment(ksCmsAttachment);
        }

        return new GenericCommandResult();
    }
}
