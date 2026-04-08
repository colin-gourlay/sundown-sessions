// <copyright file="ShowNotesFrontmatterBuilder.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.CreateFrontmatter
{
    using System.Globalization;
    using System.Text;

    public static class ShowNotesFrontmatterBuilder
    {
        public static string Build(CreateShowNotesFrontmatterCommand command)
        {
            var sb = new StringBuilder();

            var formattedDate = FormatBroadcastDate(command.BroadcastDate);
            var slug = ToSlug(command.FeaturedGuest);
            var isoDate = command.BroadcastDate.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);

            sb.AppendLine("---");
            sb.AppendLine(CultureInfo.InvariantCulture, $"title: 'Show #{command.ShowNumber}: Broadcast {formattedDate}'");
            sb.AppendLine(CultureInfo.InvariantCulture, $"slug: 'featuring-{slug}'");
            sb.AppendLine(CultureInfo.InvariantCulture, $"description: 'featuring {EscapeYamlSingleQuoted(command.FeaturedGuest)}'");
            sb.AppendLine("summary: 'THE SUNDOWN SESSIONS returns with...");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"          - {command.FeaturedGuest}");
            sb.AppendLine();
            sb.AppendLine("          - and much, much more...");
            sb.AppendLine("'");
            sb.AppendLine("keywords:");
            foreach (var keyword in command.Keywords)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $" - '{EscapeYamlSingleQuoted(keyword)}'");
            }

            sb.AppendLine("toc: false");
            sb.AppendLine(CultureInfo.InvariantCulture, $"featured_image: '{command.ShowNumber}-show-logo.jpeg'");
            sb.AppendLine("read_more_copy: Show notes...");
            sb.AppendLine("show_reading_time: true");
            sb.AppendLine(CultureInfo.InvariantCulture, $"date: {isoDate}");
            sb.AppendLine("draft: true");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< fold \"Listen On Demand\" >}}}}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< include_content \"/shows/{command.ShowNumber}/listen-again\" >}}}}");
            sb.AppendLine("{{< /fold >}}");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< fold \"Playlist\" >}}}}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< include_content \"/shows/{command.ShowNumber}/playlist\" >}}}}");
            sb.AppendLine("{{< /fold >}}");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< fold \"Featured band: {command.FeaturedGuest}\" >}}}}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< include_content \"/shows/{command.ShowNumber}/featured-guest\" >}}}}");
            sb.AppendLine("{{< /fold >}}");
            sb.AppendLine();
            sb.AppendLine("{{< fold \"Show discussion points\" >}}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< include_content \"/shows/{command.ShowNumber}/discussion-points\" >}}}}");
            sb.AppendLine("{{< /fold >}}");
            sb.AppendLine();
            sb.AppendLine("{{< fold \"Track info\" >}}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{{{{< include_content \"/shows/{command.ShowNumber}/track-info\" >}}}}");
            sb.AppendLine("{{< /fold >}}");

            return sb.ToString();
        }

        public static string FormatBroadcastDate(DateTimeOffset date)
        {
            var day = date.Day;
            var suffix = GetOrdinalSuffix(day);
            return date.ToString($"d'{suffix}' MMMM yyyy", CultureInfo.InvariantCulture);
        }

        public static string ToSlug(string value)
        {
            var lower = value.ToLowerInvariant();
            var cleaned = new global::System.Text.StringBuilder(lower.Length);
            var lastWasHyphen = false;

            foreach (var c in lower)
            {
                if (char.IsLetterOrDigit(c))
                {
                    cleaned.Append(c);
                    lastWasHyphen = false;
                }
                else if ((char.IsWhiteSpace(c) || c == '-') && !lastWasHyphen)
                {
                    cleaned.Append('-');
                    lastWasHyphen = true;
                }
            }

            return cleaned.ToString().Trim('-');
        }

        public static string EscapeYamlSingleQuoted(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal);
        }

        private static string GetOrdinalSuffix(int day)
        {
            if (day is 11 or 12 or 13)
            {
                return "th";
            }

            return (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        }
    }
}
