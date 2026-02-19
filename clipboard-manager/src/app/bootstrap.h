#pragma once

#include <gtkmm.h>
#include <memory>

class ClipboardDB;
class ClipboardService;
class DaemonClient;
class MainWindow;

class AppBootstrap {
public:
    AppBootstrap();
    int run(int argc, char* argv[]);

private:
    void initialize_core();
    void connect_activation();
    void start_daemon_thread();

    Glib::RefPtr<Gtk::Application> app_;
    std::shared_ptr<ClipboardDB> db_;
    std::shared_ptr<ClipboardService> clipboard_service_;
    std::shared_ptr<DaemonClient> daemon_client_;
    std::shared_ptr<MainWindow> window_;
};
