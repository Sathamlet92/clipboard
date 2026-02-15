using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClipboardManager.App.Models;
using ClipboardManager.App.ViewModels;

namespace ClipboardManager.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnItemPressed(object? sender, PointerPressedEventArgs e)
    {
        // Si el click fue en un botón, ignorar
        var source = e.Source as Avalonia.Visual;
        while (source != null)
        {
            if (source is Button)
            {
                return;
            }
            source = source.GetVisualParent();
        }
        
        // Obtener el item del DataContext del Border
        if (sender is Border border && border.DataContext is ClipboardItemViewModel item)
        {
            // Obtener el ViewModel
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.CopyItemAsync(item);
            }
        }
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SearchAsync();
        }
    }

    private async void OnCopyOcrTextClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ClipboardItemViewModel item)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.CopyOcrTextAsync(item);
            }
        }
    }

    private async void OnClearAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ClearAllAsync();
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ClipboardItemViewModel item)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.DeleteItemAsync(item);
            }
        }
    }
}