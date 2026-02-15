#!/bin/bash
# Script para crear la estructura de proyectos

# Volver a la raíz
cd ~/Documents/projects/clipboard-smart-manager

# Limpiar carpetas src existentes
rm -rf src/*

# Crear proyecto Avalonia App
dotnet new avalonia.mvvm -n ClipboardManager.App -o src/ClipboardManager.App

# Crear proyectos de librería
dotnet new classlib -n ClipboardManager.Core -o src/ClipboardManager.Core
dotnet new classlib -n ClipboardManager.Data -o src/ClipboardManager.Data
dotnet new classlib -n ClipboardManager.ML -o src/ClipboardManager.ML
dotnet new classlib -n ClipboardManager.Daemon.Client -o src/ClipboardManager.Daemon.Client

# Agregar proyectos a la solución
dotnet sln ClipboardManager.sln add src/ClipboardManager.App/ClipboardManager.App.csproj
dotnet sln ClipboardManager.sln add src/ClipboardManager.Core/ClipboardManager.Core.csproj
dotnet sln ClipboardManager.sln add src/ClipboardManager.Data/ClipboardManager.Data.csproj
dotnet sln ClipboardManager.sln add src/ClipboardManager.ML/ClipboardManager.ML.csproj
dotnet sln ClipboardManager.sln add src/ClipboardManager.Daemon.Client/ClipboardManager.Daemon.Client.csproj

# Agregar referencias entre proyectos
dotnet add src/ClipboardManager.App/ClipboardManager.App.csproj reference src/ClipboardManager.Core/ClipboardManager.Core.csproj
dotnet add src/ClipboardManager.App/ClipboardManager.App.csproj reference src/ClipboardManager.Daemon.Client/ClipboardManager.Daemon.Client.csproj

dotnet add src/ClipboardManager.Core/ClipboardManager.Core.csproj reference src/ClipboardManager.Data/ClipboardManager.Data.csproj
dotnet add src/ClipboardManager.Core/ClipboardManager.Core.csproj reference src/ClipboardManager.ML/ClipboardManager.ML.csproj

# Agregar paquetes NuGet necesarios
echo "Agregando paquetes NuGet..."

# ClipboardManager.Data
dotnet add src/ClipboardManager.Data/ClipboardManager.Data.csproj package Microsoft.Data.Sqlite
dotnet add src/ClipboardManager.Data/ClipboardManager.Data.csproj package Dapper

# ClipboardManager.ML
dotnet add src/ClipboardManager.ML/ClipboardManager.ML.csproj package Microsoft.ML.OnnxRuntime
dotnet add src/ClipboardManager.ML/ClipboardManager.ML.csproj package SixLabors.ImageSharp

# ClipboardManager.Daemon.Client
dotnet add src/ClipboardManager.Daemon.Client/ClipboardManager.Daemon.Client.csproj package Grpc.Net.Client
dotnet add src/ClipboardManager.Daemon.Client/ClipboardManager.Daemon.Client.csproj package Google.Protobuf
dotnet add src/ClipboardManager.Daemon.Client/ClipboardManager.Daemon.Client.csproj package Grpc.Tools

echo "✅ Proyectos creados y configurados correctamente"
