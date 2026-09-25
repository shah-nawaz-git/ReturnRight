using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;

namespace ReturnRight.Api.Features.Exports;

public record CaseFileImage(Domain.Evidence Evidence, Image Image);

public class CaseFileDocument(
    IssueCase issueCase,
    string caseTitle,
    DateTimeOffset generatedAtUtc,
    IReadOnlyList<CaseFileImage> images) : IDocument
{
    private const string Primary = "#315C6B";
    private const string Muted = "#667085";

    private const string Disclaimer =
        "This Case File is an organizational summary created from information provided by the user. "
        + "It does not certify legal entitlement to any outcome.";

    public DocumentMetadata GetMetadata() =>
        new() { Title = $"ReturnRight Case File — {caseTitle}" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10).FontColor("#101828"));
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("ReturnRight Case File")
                    .FontSize(18).SemiBold().FontColor(Primary);
                row.ConstantItem(180).AlignRight()
                    .Text($"Generated {generatedAtUtc.UtcDateTime:d MMMM yyyy, HH:mm} UTC")
                    .FontSize(9).FontColor(Muted);
            });
            column.Item().PaddingTop(4).Text(caseTitle).FontSize(13).SemiBold();
            column.Item().PaddingTop(8).PaddingBottom(4)
                .LineHorizontal(1f).LineColor(Primary);
        });
    }

    private void SectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingTop(14).Text(title)
            .FontSize(13).SemiBold().FontColor(Primary);
        column.Item().PaddingTop(2).PaddingBottom(4)
            .LineHorizontal(0.5f).LineColor(Primary);
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            ComposeCaseSummary(column);
            ComposePurchase(column);
            ComposeProblem(column);
            ComposeTimeline(column);
            ComposeInteractions(column);
            ComposeEvidenceIndex(column);
            if (issueCase.Status is CaseStatus.Resolved or CaseStatus.Closed)
            {
                ComposeFinalOutcome(column);
            }
            ComposeAppendix(column);
        });
    }

    private void ComposeCaseSummary(ColumnDescriptor column)
    {
        SectionTitle(column, "Case summary");
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn();
            });
            Row(table, "Problem", CaseLabels.Label(issueCase.ProblemType));
            var requested = CaseLabels.Label(issueCase.RequestedOutcomeType)
                + (issueCase.RequestedAmount is { } amount
                    ? $" — {amount:0.##} {issueCase.RequestedCurrency}"
                    : string.Empty);
            Row(table, "Requested outcome", requested);
            Row(table, "Current status", CaseLabels.Label(issueCase.Status));
            if (issueCase.FinalOutcomeType is { } final)
            {
                Row(table, "Final outcome", CaseLabels.Label(final)
                    + (issueCase.FinalOutcomeOn is { } on ? $" on {on:d MMM yyyy}" : string.Empty));
            }
        });
    }

    private void ComposePurchase(ColumnDescriptor column)
    {
        SectionTitle(column, "Purchase");
        var purchase = issueCase.Purchase;
        if (purchase is null)
        {
            column.Item().Text("No purchase details recorded.").FontColor(Muted);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn();
            });
            Row(table, "Merchant", purchase.MerchantName);
            if (purchase.PurchaseDate is { } date)
            {
                Row(table, "Purchase date", $"{date:d MMM yyyy}");
            }
            if (!string.IsNullOrWhiteSpace(purchase.OrderNumber))
            {
                Row(table, "Order / reference", purchase.OrderNumber);
            }
            if (purchase.TotalAmount is { } total)
            {
                Row(table, "Purchase total", $"{total:0.##} {purchase.Currency}");
            }
        });

        var affectedIds = issueCase.AffectedItems.Select(a => a.PurchaseItemId).ToHashSet();
        var items = purchase.Items
            .Where(i => affectedIds.Count == 0 || affectedIds.Contains(i.Id))
            .OrderBy(i => i.SortOrder)
            .ToList();
        if (items.Count > 0)
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });
                Row(table, "Affected items", string.Join("\n", items.Select(item =>
                    $"{item.ProductName} — qty {item.Quantity}"
                    + (item.UnitPrice is { } price
                        ? $" at {price:0.##} {purchase.Currency}"
                        : string.Empty))));
            });
        }
    }

    private void ComposeProblem(ColumnDescriptor column)
    {
        SectionTitle(column, "Problem");
        if (issueCase.ProblemDiscoveredOn is { } discovered)
        {
            column.Item().PaddingBottom(2)
                .Text($"Discovered on {discovered:d MMM yyyy}").FontColor(Muted);
        }
        column.Item().Text(issueCase.Description).LineHeight(1.4f);
    }

    private void ComposeTimeline(ColumnDescriptor column)
    {
        SectionTitle(column, "Resolution timeline");
        var events = issueCase.TimelineEvents
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt).ToList();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(90);
                columns.RelativeColumn();
            });
            if (events.Count == 0)
            {
                table.Cell().ColumnSpan(2).Text("No events recorded.").FontColor(Muted);
            }
            foreach (var timelineEvent in events)
            {
                table.Cell().Text($"{timelineEvent.OccurredAt:d MMM yyyy}").FontColor(Muted);
                table.Cell().Text(timelineEvent.Summary);
            }
        });
    }

    private void ComposeInteractions(ColumnDescriptor column)
    {
        SectionTitle(column, "Seller interactions");
        var interactions = issueCase.Interactions.OrderBy(i => i.OccurredAt).ToList();
        if (interactions.Count == 0)
        {
            column.Item().Text("No seller interactions recorded.").FontColor(Muted);
            return;
        }
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(95);
                columns.ConstantColumn(120);
                columns.RelativeColumn();
            });
            foreach (var interaction in interactions)
            {
                table.Cell().Text($"{interaction.OccurredAt:d MMM yyyy HH:mm}").FontColor(Muted);
                table.Cell().Text(CaseLabels.Label(interaction.InteractionType));
                table.Cell().Text(interaction.Note);
            }
        });
    }

    private void ComposeEvidenceIndex(ColumnDescriptor column)
    {
        SectionTitle(column, "Evidence index");
        var evidence = issueCase.Evidence.OrderBy(e => e.CreatedAt).ToList();
        if (evidence.Count == 0)
        {
            column.Item().Text("No evidence recorded.").FontColor(Muted);
            return;
        }
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(25);
                columns.ConstantColumn(95);
                columns.ConstantColumn(70);
                columns.RelativeColumn();
                columns.RelativeColumn();
            });
            var number = 0;
            foreach (var item in evidence)
            {
                number++;
                table.Cell().Text($"{number}").FontColor(Muted);
                table.Cell().Text(CaseLabels.Label(item.EvidenceType));
                table.Cell().Text($"{item.CreatedAt:d MMM yyyy}").FontColor(Muted);
                table.Cell().Text(item.Document.OriginalFileName);
                table.Cell().Text(item.Description ?? "—").FontColor(Muted);
            }
        });
    }

    private void ComposeFinalOutcome(ColumnDescriptor column)
    {
        SectionTitle(column, "Final outcome");
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn();
            });
            Row(table, "Outcome",
                issueCase.FinalOutcomeType is { } final ? CaseLabels.Label(final) : "—");
            if (issueCase.FinalAmount is { } amount)
            {
                Row(table, "Amount", $"{amount:0.##} {issueCase.FinalCurrency}");
            }
            if (issueCase.FinalOutcomeOn is { } on)
            {
                Row(table, "Completed on", $"{on:d MMM yyyy}");
            }
            if (!string.IsNullOrWhiteSpace(issueCase.FinalNote))
            {
                Row(table, "Note", issueCase.FinalNote);
            }
        });
    }

    private void ComposeAppendix(ColumnDescriptor column)
    {
        if (images.Count == 0)
        {
            return;
        }
        SectionTitle(column, "Appendix: image evidence");
        var number = 0;
        foreach (var image in images)
        {
            number++;
            column.Item().PaddingTop(6).Text(
                $"{number}. {CaseLabels.Label(image.Evidence.EvidenceType)} — "
                + image.Evidence.Document.OriginalFileName)
                .FontColor(Muted).FontSize(9);
            column.Item().PaddingTop(4).MaxHeight(240)
                .Image(image.Image).FitArea();
        }
    }

    private void ComposeFooter(IContainer container)
    {
        container.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted))
            .Column(column =>
            {
                column.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor("#D0D5DD");
                column.Item().PaddingTop(4).Text(Disclaimer);
                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
    }

    private static void Row(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(2).Text(label).FontColor(Muted);
        table.Cell().PaddingVertical(2).Text(value);
    }
}
