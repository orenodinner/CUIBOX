using System;
using System.Windows;
using System.Windows.Controls;

namespace TermRunner.Views;

public partial class MainWindow : Window
{
    private TerminalTabView? _terminalTabView;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_terminalTabView != null)
        {
            return;
        }

        _terminalTabView = new TerminalTabView();
        var tabItem = new TabItem
        {
            Header = "cmd.exe",
            Content = _terminalTabView
        };

        TerminalTabs.Items.Add(tabItem);
        TerminalTabs.SelectedItem = tabItem;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _terminalTabView?.Dispose();
        _terminalTabView = null;
    }
}
