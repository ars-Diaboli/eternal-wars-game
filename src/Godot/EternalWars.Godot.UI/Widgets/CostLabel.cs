using EternalWars.Core.Domain.Economy;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// A cost with what the player lacks marked (ADR-294): each resource's icon and amount, the amounts the treasury cannot cover in the refusal
/// colour, and a tooltip that names the shortfall ("Lacking: 1500 gold, 2 ore"). "free" when nothing is due. <see cref="Show"/> refills it, so
/// a section keeps one and updates it when the resources change.
/// </summary>
public partial class CostLabel : HBoxContainer
{
    public CostLabel()
    {
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeConstantOverride("separation", UiSpacing.Cell);
    }

    /// <summary>A label showing <paramref name="cost"/> against <paramref name="treasury"/> (null: nothing marked).</summary>
    public CostLabel(ResourceSet cost, ResourceSet? treasury, string suffix = "", int size = ResourceIcons.TextSize)
        : this()
    {
        Show(cost, treasury, suffix, size);
    }

    /// <summary>What the treasury lacks for the cost shown; empty when it covers it (or no treasury was given).</summary>
    public ResourceSet Lacking { get; private set; } = ResourceSet.Empty;

    /// <summary>Shows <paramref name="cost"/>, marking what <paramref name="treasury"/> lacks, then <paramref name="suffix"/> (" each").</summary>
    public void Show(ResourceSet cost, ResourceSet? treasury, string suffix = "", int size = ResourceIcons.TextSize)
    {
        this.ClearChildren();
        Lacking = treasury is { } have ? Shortfall(cost, have) : ResourceSet.Empty;
        var any = false;
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            if (cost[resource] == 0)
            {
                continue;
            }

            any = true;
            if (ResourceIcons.Get(resource, size) is { } texture)
            {
                AddChild(new TextureRect
                {
                    Texture = texture,
                    MouseFilter = MouseFilterEnum.Ignore,
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    CustomMinimumSize = new Vector2(size, size),
                });
            }

            var amount = ResourceIcons.AmountLabel(resource, cost[resource]);
            if (ResourceIcons.Get(resource, size) is null)
            {
                amount.Text += $" {resource.ToString().ToLowerInvariant()}";
            }

            if (Lacking[resource] > 0)
            {
                amount.AddThemeColorOverride("font_color", UiTheme.TextRefusal);
            }

            AddChild(amount);
        }

        if (!any || suffix.Length > 0)
        {
            AddChild(new Label { Text = any ? suffix.TrimStart() : "free" + suffix, MouseFilter = MouseFilterEnum.Ignore });
        }

        TooltipText = Lacking.IsEmpty ? "" : $"Lacking: {ResourceIcons.Describe(Lacking)}";
    }

    /// <summary>What <paramref name="have"/> lacks to pay <paramref name="cost"/>, resource by resource.</summary>
    public static ResourceSet Shortfall(ResourceSet cost, ResourceSet have)
    {
        var lacking = ResourceSet.Empty;
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            if (cost[resource] > have[resource])
            {
                lacking += ResourceSet.Of(resource, cost[resource] - have[resource]);
            }
        }

        return lacking;
    }
}
