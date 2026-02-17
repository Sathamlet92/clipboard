#pragma once

#include <gtkmm.h>
#include <memory>
#include "../services/clipboard_service.h"
#include "../services/search_service.h"

class MainWindow : public Gtk::Window {
public:
    MainWindow(std::shared_ptr<ClipboardService> service);
    virtual ~MainWindow() = default;
    
    void refresh_from_daemon();

protected:
    // Signal handlers
    void on_search_changed();
    void on_search_activated();
    void on_item_clicked(int64_t item_id);
    void on_delete_clicked(int64_t item_id);
    void on_clear_all_clicked();
    
    // UI update
    void load_items();
    void update_item_list();
    
private:
    // Services
    std::shared_ptr<ClipboardService> clipboard_service_;
    std::shared_ptr<SearchService> search_service_;
    
    // UI Components
    Gtk::Box main_box_{Gtk::Orientation::VERTICAL};
    Gtk::Box search_box_{Gtk::Orientation::HORIZONTAL};
    Gtk::SearchEntry search_entry_;
    Gtk::Button search_button_{"🔍"};
    Gtk::Button clear_button_{"🗑️"};
    Gtk::ScrolledWindow scrolled_window_;
    Gtk::ListBox item_list_;
    Gtk::Box status_bar_{Gtk::Orientation::HORIZONTAL};
    Gtk::Label status_label_;
    
    // Data
    std::vector<ClipboardItem> items_;
    std::string current_search_;
};
