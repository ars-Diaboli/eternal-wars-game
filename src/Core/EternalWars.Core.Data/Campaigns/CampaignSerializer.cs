using System.Text.Json;
using EternalWars.Core.Domain.Campaigns;

namespace EternalWars.Core.Data.Campaigns;

/// <summary>
/// A campaign file (<c>*.ewcampaign.json</c>, ADR-241): its name, description and maps in order, each naming a map file in the campaign file's
/// folder. Version 1; a newer file is refused.
/// </summary>
public sealed record CampaignDocument(string Name, string Description, IReadOnlyList<CampaignMap> Maps)
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
}

/// <summary>Reads and writes campaign files (ADR-241).</summary>
public static class CampaignSerializer
{
    public const string FileExtension = ".ewcampaign.json";

    public static string Serialize(Campaign campaign) =>
        JsonSerializer.Serialize(new CampaignDocument(campaign.Name, campaign.Description, campaign.Maps), JsonDefaults.Options);

    /// <summary>The campaign in <paramref name="json"/>; <see cref="FormatException"/> when it is no campaign, is newer than this game or breaks a rule.</summary>
    public static Campaign Deserialize(string json)
    {
        CampaignDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CampaignDocument>(json, JsonDefaults.Options);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Not a campaign file: {e.Message}", e);
        }

        if (document is null)
        {
            throw new FormatException("The campaign file is empty.");
        }

        if (document.FormatVersion > CampaignDocument.CurrentFormatVersion)
        {
            throw new FormatException($"The campaign file is version {document.FormatVersion}; this game reads up to {CampaignDocument.CurrentFormatVersion}.");
        }

        var campaign = new Campaign(document.Name ?? "", document.Description ?? "", document.Maps ?? []);
        if (Problems(campaign) is [var first, ..])
        {
            throw new FormatException(first);
        }

        return campaign;
    }

    /// <summary>What is wrong with the campaign: no name, no maps, a map without a file or with a negative count of heroes to carry.</summary>
    public static IReadOnlyList<string> Problems(Campaign campaign)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(campaign.Name))
        {
            problems.Add("The campaign needs a name.");
        }

        if (campaign.Maps.Count == 0)
        {
            problems.Add("The campaign needs at least one map.");
        }

        for (var i = 0; i < campaign.Maps.Count; i++)
        {
            var map = campaign.Maps[i];
            if (string.IsNullOrWhiteSpace(map.Map))
            {
                problems.Add($"Map {i + 1} names no map file.");
            }

            if (map.CarryStrongest < 0)
            {
                problems.Add($"Map {i + 1} carries a negative number of heroes.");
            }
        }

        return problems;
    }

    public static void Save(Campaign campaign, string path) => File.WriteAllText(path, Serialize(campaign));

    public static Campaign Load(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>
    /// The campaigns in <paramref name="directory"/> (<c>*.ewcampaign.json</c>, by file name), none when it does not exist; a file that does not
    /// load is skipped, with a <paramref name="warn"/> saying why.
    /// </summary>
    public static List<(string Path, Campaign Campaign)> LoadFolder(string directory, Action<string> warn)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<(string, Campaign)>();
        foreach (var file in Directory.GetFiles(directory, "*" + FileExtension).Order(StringComparer.Ordinal))
        {
            try
            {
                found.Add((file, Load(file)));
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                warn($"Skipping campaign {file}: {e.Message}");
            }
        }

        return found;
    }
}
