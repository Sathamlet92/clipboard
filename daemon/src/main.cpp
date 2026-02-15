#include "clipboard_monitor.h"
#include "grpc_server.h"
#include <iostream>
#include <csignal>
#include <atomic>
#include <thread>

std::atomic<bool> g_running(true);

void signal_handler(int signal) {
    std::cout << "\nReceived signal " << signal << ", shutting down..." << std::endl;
    g_running = false;
}

int main(int argc, char* argv[]) {
    std::cout << "Clipboard Manager Daemon v1.0.0" << std::endl;
    std::cout << "================================" << std::endl;
    
    // Setup signal handlers
    std::signal(SIGINT, signal_handler);
    std::signal(SIGTERM, signal_handler);
    
    // Parse command line arguments
    std::string server_address = "unix:///tmp/clipboard-daemon.sock";
    
    if (argc > 1) {
        server_address = argv[1];
    }
    
    std::cout << "Server address: " << server_address << std::endl;
    
    // Create clipboard monitor
    auto monitor = clipboard::CreateClipboardMonitor();
    
    if (!monitor->Initialize()) {
        std::cerr << "Failed to initialize clipboard monitor" << std::endl;
        return 1;
    }
    
    // Create gRPC server
    clipboard::GrpcServer grpc_server(server_address, monitor.get());
    
    // Setup clipboard change callback
    monitor->OnClipboardChanged = [&grpc_server](const clipboard::ClipboardData& data) {
        grpc_server.GetService()->OnClipboardChanged(data);
    };
    
    // Start monitor in separate thread
    std::thread monitor_thread([&monitor]() {
        monitor->Run();
    });
    
    // Run gRPC server in main thread
    std::thread grpc_thread([&grpc_server]() {
        grpc_server.Run();
    });
    
    // Wait for shutdown signal
    while (g_running) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
    
    // Cleanup
    std::cout << "Shutting down..." << std::endl;
    monitor->Stop();
    grpc_server.Shutdown();
    
    if (monitor_thread.joinable()) {
        monitor_thread.join();
    }
    
    if (grpc_thread.joinable()) {
        grpc_thread.join();
    }
    
    std::cout << "Daemon stopped" << std::endl;
    return 0;
}
