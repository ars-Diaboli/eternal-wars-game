using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// The editor's files (ADR-288): a new map (the wizard, a sample), Open, Save and Save as on the tab shown (the map's file, or the
/// template's on the Template tab, ADR-269), the template commands, the exports and Quit. The files themselves are the
/// <see cref="FileService"/>'s and the <see cref="TemplateFileService"/>'s.
/// </summary>
internal sealed class FileComposer(EditorParts parts, GenerateComposer generate)
{
    public void Register()
    {
        var files = parts.Files;
        var templates = parts.TemplateFiles;
        parts.Command("file.new", "New map…", OpenWizard, shortcut: new(Key.N, Ctrl: true), icon: UiIcon.New);
        parts.Command("file.open", "Open", () => parts.OnTab(templates.Open, () => files.Open()), UiIcon.Load, new(Key.O, Ctrl: true));
        parts.Command("file.save", "Save", () => parts.OnTab(() => templates.Save(), () => files.Save()), UiIcon.Save, new(Key.S, Ctrl: true));
        parts.Command("file.save-as", "Save as", () => parts.OnTab(() => templates.SaveAs(), () => files.SaveAs()), shortcut: new(Key.S, Ctrl: true, Shift: true), icon: UiIcon.SaveAs);
        parts.Command("file.export-report", "Export report (Markdown)…", ExportReport, UiIcon.ExportReport);
        parts.Command("file.export-picture", "Export map picture (PNG)…", ExportPicture, onMap: true, icon: UiIcon.ExportPicture);
        parts.Command("file.quit", "Quit", Quit, shortcut: new(Key.Q, Ctrl: true), icon: UiIcon.Quit);
        parts.Command("template.new", "New template", () => OpenTemplate(templates.New), UiIcon.New);
        parts.Command("template.open", "Open template…", () => OpenTemplate(templates.Open), UiIcon.Load);
        parts.Command("template.save", "Save template", () => templates.Save(), UiIcon.Save);
        parts.Command("template.save-as", "Save template as…", () => templates.SaveAs(), UiIcon.SaveAs);
    }

    /// <summary>The new-map wizard (ADR-259): it ends on a blank map or in the Generate tab, set to the chosen template.</summary>
    public void OpenWizard()
    {
        if (parts.IsBusy)
        {
            return;
        }

        var wizard = new NewMapWizard { Name = "NewMap" };
        parts.Overlays.AddChild(wizard);
        wizard.Bind(parts.Preferences.DefaultMapSize, generate.GeneratorTemplates(), request =>
        {
            if (request.Template is { } template)
            {
                parts.Workspace.ShowPage(EditorWorkspace.MapPage);
                parts.Generate.Preset(template, request.Size, request.Settings.Humans.Count, request.Settings.Humans);
                parts.Workspace.ShowTab(parts.Generate);
            }
            else
            {
                // The next steps for a first map (ADR-279): what the blank map lacks to be played.
                parts.Files.OpenNew(BlankScenario.Create(request.Settings, GameContent.All), () => parts.Say(
                    "A blank map with a hero for each player. Next: give each player a town (browser → Towns, the Owner in the tool's options); "
                    + "Problems lists what is left, many with a Fix; then Test (F5)."));
            }
        });
    }

    /// <summary>A sample map (ADR-259) opened as a new, unsaved map, through the map format like a file: from the empty state or Help → Sample maps.</summary>
    public void OpenSample(SampleMap sample) =>
        parts.Files.OpenNew(MapConverter.ToGameState(MapConverter.ToDocument(sample.Create(GameContent.All)), GameContent.All));

    /// <summary>
    /// Closes the editor, after the questions over the template's unsaved work and then the map's. A template autosave not offered yet is
    /// offered first (quitting properly would forget it); nothing happens while a question or a dialog is open.
    /// </summary>
    public void Quit()
    {
        if (parts.IsBusy)
        {
            return;
        }

        if (parts.TemplateFiles.HasRestoreOffer)
        {
            parts.Workspace.ShowPage(parts.TemplatePage);
            parts.TemplateFiles.OfferRestore();
            return;
        }

        parts.TemplateFiles.WhenSafe(() => parts.Files.Quit(parts.TemplateFiles.EndSession));
    }

    /// <summary>
    /// A template command from the menus: the Template tab shows, then <paramref name="open"/> runs (it may ask over unsaved work). A template
    /// autosave waiting to be offered is offered instead; the command is chosen again after that question.
    /// </summary>
    public void OpenTemplate(Action open)
    {
        parts.Workspace.ShowPage(parts.TemplatePage);
        if (parts.TemplateFiles.IsBusy)
        {
            parts.Say("Answer the question over the template autosave first");
            return;
        }

        open();
    }

    /// <summary>File → Export report (ADR-266): the problems of the map as it is now and its fairness numbers, as Markdown.</summary>
    private void ExportReport() => parts.Files.PickExport("Export the map report", " report", ".md", "Markdown", path =>
    {
        var state = parts.Editor.Scenario;
        string report;
        try
        {
            report = MapReport.Markdown(state, new MapAnalysis(state).Fairness(), ProblemsPanel.Validate(state));
        }
        catch (Exception e)
        {
            parts.Say($"Could not export the report: the map could not be analysed ({e.Message}).");
            return;
        }

        try
        {
            File.WriteAllText(path, report);
            parts.Say($"Exported the report to {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            parts.Say($"Could not export the report: {e.Message}");
        }
    });

    /// <summary>File → Export map picture (ADR-266): the level shown, top-down, as a PNG; the selection's marks and the captions are left out.</summary>
    private void ExportPicture() => parts.Files.PickExport("Export the map picture", "", ".png", "PNG pictures", path =>
    {
        var level = parts.Context.Level;
        MapPicture.Take(parts.Root, parts.Editor.Scenario.Map, level, parts.World!, [parts.SelectionView], image =>
        {
            if (image is null)
            {
                parts.Say("Could not export the map picture: nothing was drawn.");
                return;
            }

            var saved = image.SavePng(path);
            parts.Say(saved == Error.Ok
                ? $"Exported the map picture ({image.GetWidth()} × {image.GetHeight()}, level {level + 1}) to {path}"
                : $"Could not export the map picture: {saved}");
        });
    });
}
