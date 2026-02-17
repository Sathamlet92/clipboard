#include <gtkmm.h>
#include <iostream>
#include <thread>
#include "ui/main_window.h"
#include "database/clipboard_db.h"
#include "services/clipboard_service.h"
#include "grpc/daemon_client.h"

int main(int argc, char* argv[]) {
    std::cout << "Clipboard Manager C++ (Wayland Native)" << std::endl;
    
    try {
        std::cout << "🔧 Initializing GTK4..." << std::endl;
        auto app = Gtk::Application::create("com.clipboard.manager");
        std::cout << "✅ GTK4 initialized" << std::endl;
        
        std::cout << "🔧 Initializing database..." << std::endl;
        auto db_path = std::string(getenv("HOME")) + "/.clipboard-manager/clipboard.db";
        auto db = std::make_shared<ClipboardDB>(db_path);
        if (!db->initialize()) {
            std::cerr << "❌ Failed to initialize database" << std::endl;
            return 1;
        }
        std::cout << "✅ Database initialized" << std::endl;
        
        std::cout << "🔧 Initializing services..." << std::endl;
        auto clipboard_service = std::make_shared<ClipboardService>(db);
        std::cout << "✅ Services initialized" << std::endl;
        
        std::cout << "🔧 Setting up daemon client..." << std::endl;
        auto daemon_client = std::make_shared<DaemonClient>("unix:///tmp/clipboard-daemon.sock");
        std::cout << "✅ Daemon client configured" << std::endl;
        
        std::cout << "🔧 Creating main window..." << std::endl;
        std::shared_ptr<MainWindow> window_ptr;
        
        app->signal_activate().connect([&]() {
            // Hold the application to prevent it from exiting when window is hidden
            app->hold();
            
            window_ptr = std::make_shared<MainWindow>(clipboard_service);
            app->add_window(*window_ptr);
            window_ptr->show();
            
            // Set callback after window is created - use weak_ptr to avoid dangling pointer
            std::weak_ptr<MainWindow> weak_window = window_ptr;
            clipboard_service->set_items_updated_callback([weak_window]() {
                if (auto window = weak_window.lock()) {
                    window->refresh_from_daemon();
                }
            });
            daemon_client->set_callback([clipboard_service, weak_window](const ClipboardEvent& event) {
                clipboard_service->process_event(event);
                if (auto window = weak_window.lock()) {
                    window->refresh_from_daemon();
                }
            });
            
            // Start daemon connection
            std::thread daemon_thread([daemon_client]() {
                try {
                    daemon_client->start();
                } catch (const std::exception& e) {
                    std::cerr << "⚠️  Daemon error: " << e.what() << std::endl;
                }
            });
            daemon_thread.detach();
        });
        
        return app->run(argc, argv);
        
    } catch (const std::exception& e) {
        std::cerr << "💥 FATAL ERROR: " << e.what() << std::endl;
        return 1;
    } catch (...) {
        std::cerr << "💥 FATAL ERROR: Unknown exception" << std::endl;
        return 1;
    }
}
