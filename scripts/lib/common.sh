#!/bin/bash

set -euo pipefail

log_info() {
    echo "ℹ️  $*"
}

log_ok() {
    echo "✅ $*"
}

log_warn() {
    echo "⚠️  $*"
}

log_err() {
    echo "❌ $*"
}

project_root_dir() {
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
    echo "$(cd "$script_dir/.." && pwd)"
}

require_arch_linux() {
    if ! command -v pacman >/dev/null 2>&1; then
        log_err "Este script es solo para Arch Linux"
        exit 1
    fi
}

detect_clipboard_binary() {
    local project_root="$1"
    local project_build_bin="$project_root/clipboard-manager/build/clipboard-manager"

    if [ -x "$HOME/.local/bin/clipboard-manager" ]; then
        echo "$HOME/.local/bin/clipboard-manager"
    elif [ -x "/usr/local/bin/clipboard-manager" ]; then
        echo "/usr/local/bin/clipboard-manager"
    elif [ -x "$project_build_bin" ]; then
        echo "$project_build_bin"
    else
        echo "clipboard-manager"
    fi
}

detect_active_keybinding_file() {
    local hypr_dir="$1"
    local pointer="$hypr_dir/conf/keybinding.conf"
    local default_file="$hypr_dir/conf/keybindings/default.conf"
    local active_file="$default_file"

    if [ -f "$pointer" ]; then
        local src_line src_path
        src_line=$(grep -E '^source\s*=\s*' "$pointer" | head -n1 || true)
        if [ -n "$src_line" ]; then
            src_path=$(echo "$src_line" | sed -E 's/^source\s*=\s*//')
            src_path=${src_path/#\~/$HOME}
            if [ -f "$src_path" ]; then
                active_file="$src_path"
            fi
        fi
    fi

    echo "$active_file"
}

create_hypr_backup_dir() {
    echo "$HOME/.clipboard-manager-backups/$(date +%Y%m%d_%H%M%S)"
}

backup_hypr_file() {
    local hypr_dir="$1"
    local backup_dir="$2"
    local file="$3"

    if [ -f "$file" ]; then
        local rel target
        rel="${file#$hypr_dir/}"
        target="$backup_dir/$rel"
        mkdir -p "$(dirname "$target")"
        cp "$file" "$target"
        log_ok "Backup: $rel"
    fi
}
