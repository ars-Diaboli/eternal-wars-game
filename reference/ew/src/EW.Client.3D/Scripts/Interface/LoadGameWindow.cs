using System.Collections.Generic;
using EW.Scripts;
using Godot;

namespace EW.Scripts.Interface;

public partial class LoadGameWindow : Control
{
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler(string savePath);

    [Export] public ItemList SaveList { get; set; }
    [Export] public Button LoadButton { get; set; }
    [Export] public Button BackButton { get; set; }

    private readonly List<string> _savePaths = new();
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        ResolveReferences();

        if (SaveList != null)
        {
            SaveList.ItemSelected += OnSaveSelected;
        }

        if (LoadButton != null)
        {
            LoadButton.Pressed += OnLoadPressed;
        }

        if (BackButton != null)
        {
            BackButton.Pressed += OnBackPressed;
        }

        Visible = false;
        RefreshSaveList();
    }

    public override void _ExitTree()
    {
        if (SaveList != null)
        {
            SaveList.ItemSelected -= OnSaveSelected;
        }

        if (LoadButton != null)
        {
            LoadButton.Pressed -= OnLoadPressed;
        }

        if (BackButton != null)
        {
            BackButton.Pressed -= OnBackPressed;
        }
    }

    public void OpenWindow()
    {
        RefreshSaveList();
        Visible = true;
    }

    public void CloseWindow()
    {
        Visible = false;
    }

    public void RefreshSaveList()
    {
        ResolveReferences();
        _savePaths.Clear();
        _selectedIndex = -1;

        if (SaveList == null)
        {
            return;
        }

        SaveList.Clear();

        foreach (string savePath in SaveGameStore.GetSavedGamePaths())
        {
            _savePaths.Add(savePath);
            SaveList.AddItem(GetSaveDisplayName(savePath));
        }

        if (_savePaths.Count == 0)
        {
            SaveList.AddItem("No saved games available");
        }

        UpdateLoadButtonState();
    }

    private void OnSaveSelected(long index)
    {
        _selectedIndex = (int)index;
        UpdateLoadButtonState();
    }

    private void OnLoadPressed()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _savePaths.Count)
        {
            return;
        }

        string savePath = _savePaths[_selectedIndex];
        GD.Print($"Load game requested: {savePath}");
        EmitSignal(SignalName.LoadRequested, savePath);
    }

    private void OnBackPressed()
    {
        CloseWindow();
        EmitSignal(SignalName.BackRequested);
    }

    private void UpdateLoadButtonState()
    {
        if (LoadButton != null)
        {
            LoadButton.Disabled = _selectedIndex < 0 || _selectedIndex >= _savePaths.Count;
        }
    }

    private void ResolveReferences()
    {
        SaveList ??= GetNodeOrNull<ItemList>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SaveList");
        LoadButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/BottomBar/LoadGameButton");
        BackButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/BottomBar/BackButton");
    }

    private static string GetSaveDisplayName(string savePath)
    {
        int separatorIndex = savePath.LastIndexOf('/');
        return separatorIndex < 0 ? savePath : savePath[(separatorIndex + 1)..];
    }
}
