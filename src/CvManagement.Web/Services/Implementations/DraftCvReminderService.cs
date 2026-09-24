using System.Net;
using CvManagement.Web.Data;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CvManagement.Web.Services.Implementations;

public class DraftCvReminderOptions
{
    public const string SectionName = "Reminders:DraftCv";

    public bool Enabled { get; set; } = true;
    /// <summary>How often the job wakes up.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
    /// <summary>A draft becomes due for a reminder once it has been unpublished this long.</summary>
    public TimeSpan DraftAge { get; set; } = TimeSpan.FromDays(3);
    /// <summary>Minimum gap between two reminders about the same draft.</summary>
    public TimeSpan RepeatAfter { get; set; } = TimeSpan.FromDays(7);
    /// <summary>Upper bound of CVs handled per run, so one run can never balloon.</summary>
    public int BatchSize { get; set; } = 200;
    /// <summary>Absolute app URL used to build links in the email (e.g. https://cv.example.com); links are omitted when empty.</summary>
    public string? BaseUrl { get; set; }
}

/// <summary>
/// Background deadline reminder: periodically emails candidates whose CV has sat in Draft past
/// <see cref="DraftCvReminderOptions.DraftAge"/>, nudging them to fill the remaining fields and publish
/// (only published CVs are visible to Recruiters). One email per candidate per run, listing all their
/// due drafts; CVs hidden by lost eligibility are skipped, since the candidate can't open them anyway.
/// </summary>
public class DraftCvReminderService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<DraftCvReminderOptions> options,
    ILogger<DraftCvReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.Enabled)
            {
                try
                {
                    var sent = await RunOnceAsync(stoppingToken);
                    if (sent > 0) logger.LogInformation("Draft CV reminders sent to {Count} candidate(s).", sent);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed run (DB or SMTP hiccup) must not kill the host -- try again next interval.
                    logger.LogError(ex, "Draft CV reminder run failed.");
                }
            }

            try
            {
                await Task.Delay(options.CurrentValue.Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One pass: fixed number of queries regardless of how many CVs are due. Returns emails sent.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var settings = options.CurrentValue;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var accessEvaluator = scope.ServiceProvider.GetRequiredService<IPositionAccessEvaluator>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IAppEmailSender>();

        var now = DateTime.UtcNow;
        var draftCutoff = now - settings.DraftAge;
        var repeatCutoff = now - settings.RepeatAfter;

        var due = await db.Cvs.AsNoTracking()
            .Where(c => c.Status == CvStatus.Draft
                && c.CreatedAt <= draftCutoff
                && (c.LastReminderSentAt == null || c.LastReminderSentAt <= repeatCutoff)
                && c.User.EmailConfirmed
                && c.User.Email != null
                && (c.User.LockoutEnd == null || c.User.LockoutEnd < DateTimeOffset.UtcNow))
            .OrderBy(c => c.CreatedAt)
            .Take(settings.BatchSize)
            .Select(c => new { c.Id, c.UserId, c.PositionId, PositionTitle = c.Position.Title, c.User.Email, c.User.DisplayName })
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        var eligible = await accessEvaluator.FilterEligibleAsync(due.Select(c => (c.UserId, c.PositionId)).ToList(), ct);
        var visible = due.Where(c => eligible.Contains((c.UserId, c.PositionId))).ToList();

        var remindedCvIds = new List<int>();
        var emailsSent = 0;
        foreach (var candidate in visible.GroupBy(c => c.UserId))
        {
            var first = candidate.First();
            var items = string.Concat(candidate.Select(cv =>
            {
                var title = WebUtility.HtmlEncode(cv.PositionTitle);
                return string.IsNullOrWhiteSpace(settings.BaseUrl)
                    ? $"<li>{title}</li>"
                    : $"<li><a href=\"{settings.BaseUrl.TrimEnd('/')}/Cv/Details/{cv.Id}\">{title}</a></li>";
            }));
            var greeting = string.IsNullOrWhiteSpace(first.DisplayName) ? "Hi" : $"Hi {WebUtility.HtmlEncode(first.DisplayName)}";

            try
            {
                await emailSender.SendAsync(first.Email!, "Your draft CVs are waiting to be published",
                    $"<p>{greeting},</p><p>These CVs are still drafts, so Recruiters can't see them yet. " +
                    $"Fill in the fields highlighted in red and press <strong>Publish</strong>:</p><ul>{items}</ul>", ct);
                remindedCvIds.AddRange(candidate.Select(cv => cv.Id));
                emailsSent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not marked as reminded, so this candidate is retried on the next run.
                logger.LogWarning(ex, "Could not send draft reminder to user {UserId}.", candidate.Key);
            }
        }

        if (remindedCvIds.Count > 0)
        {
            await db.Cvs.Where(c => remindedCvIds.Contains(c.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastReminderSentAt, now), ct);
        }
        return emailsSent;
    }
}
