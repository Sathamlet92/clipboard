#include "app/bootstrap.h"

#include <cstdlib>
#include <iostream>
#include <stdexcept>
#include <thread>

#include "database/clipboard_db.h"
#include "grpc/daemon_client.h"
#include "services/clipboard_service.h"
#include "ui/main_window.h"

AppBootstrap::AppBootstrap() = default;

int AppBootstrap::run(int argc, char* argv[]) {
    std::cout << "Clipboard Manager C++ (Wayland Native)" << std::endl;

    initialize_core();
    connect_activation();

    return app_->run(argc, argv);
}

void AppBootstrap::initialize_core() {
    std::cout << "🔧 Initializing GTK4..." << std::endl;
    app_ = Gtk::Application::create("com.clipboard.manager");
    std::cout << "✅ GTK4 initialized" << std::endl;

    std::cout << "🔧 Initializing database..." << std::endl;
    auto db_path = std::string(std::getenv("HOME")) + "/.clipboard-manager/clipboard.db";
    db_ = std::make_shared<ClipboardDB>(db_path);
    if (!db_->initialize()) {
        throw std::runtime_error("Failed to initialize database");
    }
    std::cout << "✅ Database initialized" << std::endl;

    std::cout << "🔧 Initializing services..." << std::endl;
    clipboard_service_ = std::make_shared<ClipboardService>(db_);
    std::cout << "✅ Services initialized" << std::endl;

    std::cout << "🔧 Setting up daemon client..." << std::endl;
    daemon_client_ = std::make_shared<DaemonClient>("unix:///tmp/clipboard-daemon.sock");
    std::cout << "✅ Daemon client configured" << std::endl;
}

void AppBootstrap::connect_activation() {
    std::cout << "🔧 Creating main window..." << std::endl;

    app_->signal_activate().connect([this]() {
        app_->hold();

        window_ = std::make_shared<MainWindow>(clipboard_service_);
        app_->add_window(*window_);
        window_->show();

        std::weak_ptr<MainWindow> weak_window = window_;
        clipboard_service_->set_items_updated_callback([weak_window]() {
            if (auto window = weak_window.lock()) {
                window->refresh_from_daemon();
            }
        });

        daemon_client_->set_callback([service = clipboard_service_, weak_window](const ClipboardEvent& event) {
            service->process_event(event);
            if (auto window = weak_window.lock()) {
                window->refresh_from_daemon();
            }
        });

        start_daemon_thread();
    });
}

void AppBootstrap::start_daemon_thread() {
    std::thread daemon_thread([client = daemon_client_]() {
        try {
            client->start();
        } catch (const std::exception& e) {
            std::cerr << "⚠️  Daemon error: " << e.what() << std::endl;
        }
    });
    daemon_thread.detach();
}
