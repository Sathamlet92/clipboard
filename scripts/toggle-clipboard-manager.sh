#!/bin/bash
# Script para mostrar/ocultar Clipboard Manager en Hyprland
# Uso: ./toggle-clipboard-manager.sh

WINDOW_CLASS="com.clipboard.manager"

# Verificar si la ventana está visible
if hyprctl clients -j | jq -e ".[] | select(.class == \"$WINDOW_CLASS\") | select(.hidden == false)" > /dev/null 2>&1; then
    # Ventana visible - ocultarla
    hyprctl dispatch closewindow "$WINDOW_CLASS"
else
    # Ventana oculta o no existe - mostrarla
    if pgrep -f "clipboard-manager" > /dev/null; then
        # Proceso existe, intentar mostrar ventana
        # Como GTK/Wayland no permite "raise" fácilmente, matamos y reiniciamos
        pkill -f "clipboard-manager"
        sleep 0.2
    fi
    # Iniciar la aplicación
    /usr/local/bin/clipboard-manager &
fi
