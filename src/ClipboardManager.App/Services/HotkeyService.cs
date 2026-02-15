using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;

namespace ClipboardManager.App.Services;

public class HotkeyService : IDisposable
{
    private readonly Window _window;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    public HotkeyService(Window window)
    {
        _window = window;
    }

    public bool RegisterHotkey(Key key, KeyModifiers modifiers)
    {
        if (_isRegistered)
        {
            UnregisterHotkey();
        }

        try
        {
            // En Linux, usamos el sistema de hotkeys de Avalonia
            // Para hotkeys globales reales, necesitaríamos integración con X11/Wayland
            _window.KeyDown += OnKeyDown;
            _isRegistered = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+Shift+V
        if (e.Key == Key.V && 
            e.KeyModifiers.HasFlag(KeyModifiers.Control) && 
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        // Esc para cerrar
        else if (e.Key == Key.Escape)
        {
            _window.Hide();
            e.Handled = true;
        }
        // Ctrl+F para buscar
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Focus en search box
            e.Handled = true;
        }
    }

    public void UnregisterHotkey()
    {
        if (_isRegistered)
        {
            _window.KeyDown -= OnKeyDown;
            _isRegistered = false;
        }
    }

    public void Dispose()
    {
        UnregisterHotkey();
    }
}
