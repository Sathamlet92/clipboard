#include "main_window.h"
#include "clipboard_item_widget.h"
#include <iostream>

MainWindow::MainWindow(std::shared_ptr<ClipboardService> service)
    : clipboard_service_(service)
{
    std::cout << "🔧 MainWindow: Initializing search service..." << std::endl;
    try {
        search_service_ = std::make_shared<SearchService>(service->get_db());
        std::cout << "✅ MainWindow: Search service initialized" << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "⚠️  MainWindow: Search service failed: " << e.what() << std::endl;
    }
    
    std::cout << "🔧 MainWindow: Setting window properties..." << std::endl;
    set_title("Clipboard Manager");
    set_default_size(600, 500);
    set_decorated(false);
    std::cout << "✅ MainWindow: Window properties set" << std::endl;
    
    std::cout << "🔧 MainWindow: Loading CSS..." << std::endl;
    // CSS styling para tema oscuro estilo cliphist
    auto css_provider = Gtk::CssProvider::create();
    css_provider->load_from_data(R"(
        /* Force override GTK theme focus styles */
        * {
            outline: none;
            outline-width: 0;
            outline-style: none;
            outline-color: transparent;
            -gtk-outline-radius: 0;
        }
        
        *:focus {
            outline: none;
            outline-width: 0;
            box-shadow: none;
            border-color: inherit;
        }
        
        window {
            background-color: #1A2B2B;
            border-radius: 12px;
            border: 2px solid #4A9B9B;
        }
        
        .search-box {
            background-color: #1A2B2B;
            padding: 10px;
        }
        
        entry {
            background-color: #1E3333;
            color: #E0E0E0;
            border: 1px solid #2D4D4D;
            border-radius: 4px;
            padding: 8px;
        }
        
        entry:focus {
            border-color: #4A9B9B;
            outline: none;
            box-shadow: none;
        }
        
        button {
            background-color: #2D4D4D;
            color: #FFFFFF;
            border: 1px solid #3D5D5D;
            border-radius: 4px;
            padding: 8px;
            font-weight: 500;
        }
        
        button:hover {
            background-color: #3D5D5D;
        }
        
        button:focus {
            outline: none;
            box-shadow: none;
        }
        
        .item-box {
            background-color: #1E3333;
            border: 1px solid #2D4D4D;
            border-radius: 6px;
            margin: 5px;
            padding: 10px;
        }
        
        .item-box:hover {
            background-color: #234040;
            cursor: pointer;
        }
        
        .item-box:focus {
            outline: none;
            box-shadow: none;
        }
        
        .status-bar {
            background-color: #4A9B9B;
            padding: 8px;
            color: white;
        }
        
        label {
            color: #E0E0E0;
        }
        
        .type-badge {
            color: white;
            font-weight: bold;
        }
        
        .ocr-notification {
            background-color: #E74C3C;
            color: white;
            font-size: 9px;
            font-weight: 700;
            min-width: 26px;
            min-height: 14px;
            border-radius: 7px;
            padding: 1px 4px;
            margin-top: 0px;
            margin-right: 0px;
        }
        
        .language-notification {
            background-color: #E74C3C;
            color: white;
            font-size: 9px;
            font-weight: 700;
            min-width: 24px;
            min-height: 14px;
            border-radius: 7px;
            padding: 1px 5px;
            margin-top: 0px;
            margin-right: 0px;
        }

        .language-notification-ocr {
            margin-left: 6px;
        }
        
        .time-label {
            color: #A0A0A0;
            font-size: 11px;
        }
        
        .metadata-label {
            color: #A0A0A0;
            font-size: 11px;
        }
        
        .ocr-label {
            color: #4EC9B0;
            font-size: 11px;
            background-color: #1E3A1E;
            padding: 5px;
            border-radius: 3px;
        }
        
        .code-label {
            font-family: 'JetBrains Mono', 'Fira Code', 'Courier New', monospace;
            font-size: 11px;
            background-color: #1E1E1E;
            padding: 8px;
            border-radius: 4px;
            border-left: 3px solid #61AFEF;
        }
        
        .url-label {
            color: #4A90E2;
        }
        
        .error-label {
            color: #F48771;
            font-style: italic;
        }
        
        /* Force no focus on scrollable areas */
        scrolledwindow, scrolledwindow:focus {
            outline: none;
            box-shadow: none;
        }
        
        /* Force no focus on boxes */
        box, box:focus {
            outline: none;
            box-shadow: none;
        }
    )");
    
    std::cout << "🔧 MainWindow: Applying CSS..." << std::endl;
    auto display = Gdk::Display::get_default();
    // Use USER priority to override theme settings
    Gtk::StyleContext::add_provider_for_display(
        display, css_provider, GTK_STYLE_PROVIDER_PRIORITY_USER);
    std::cout << "✅ MainWindow: CSS applied" << std::endl;
    
    std::cout << "🔧 MainWindow: Setting up UI components..." << std::endl;
    // Setup search box
    search_box_.set_spacing(5);
    search_box_.add_css_class("search-box");
    search_entry_.set_placeholder_text("Buscar en historial... (Enter o 🔍)");
    search_entry_.set_hexpand(true);
    search_entry_.signal_changed().connect(
        sigc::mem_fun(*this, &MainWindow::on_search_changed));
    search_entry_.signal_activate().connect(
        sigc::mem_fun(*this, &MainWindow::on_search_activated));
    
    search_button_.signal_clicked().connect(
        sigc::mem_fun(*this, &MainWindow::on_search_activated));
    clear_button_.signal_clicked().connect(
        sigc::mem_fun(*this, &MainWindow::on_clear_all_clicked));
    
    search_box_.append(search_entry_);
    search_box_.append(search_button_);
    search_box_.append(clear_button_);
    
    // Setup item list
    scrolled_window_.set_vexpand(true);
    scrolled_window_.set_child(item_list_);
    scrolled_window_.set_can_focus(false);
    item_list_.set_can_focus(false);
    
    // Setup status bar
    status_bar_.add_css_class("status-bar");
    status_label_.set_text("0 items");
    status_bar_.append(status_label_);
    
    // Layout
    main_box_.append(search_box_);
    main_box_.append(scrolled_window_);
    main_box_.append(status_bar_);
    
    main_box_.set_can_focus(false);
    search_box_.set_can_focus(false);
    status_bar_.set_can_focus(false);
    
    set_child(main_box_);
    
    // Load initial items
    load_items();
    
    // Handle Escape key to hide window
    auto key_controller = Gtk::EventControllerKey::create();
    key_controller->signal_key_pressed().connect(
        [this](guint keyval, guint, Gdk::ModifierType) {
            if (keyval == GDK_KEY_Escape) {
                hide();
                return true;
            }
            return false;
        }, false);
    add_controller(key_controller);
}

void MainWindow::on_search_changed() {
    // No hacer búsqueda automática, solo al presionar Enter o botón
}

void MainWindow::on_search_activated() {
    current_search_ = search_entry_.get_text();
    load_items();
}

void MainWindow::on_item_clicked(int64_t item_id) {
    try {
        // Get item from database
        auto item_opt = clipboard_service_->get_item(item_id);
        if (!item_opt) {
            std::cerr << "⚠️  Item not found: " << item_id << std::endl;
            return;
        }
        
        // Copy to clipboard
        clipboard_service_->copy_to_clipboard(*item_opt);
        
        // Keep window visible (user manages with ESC or Super+Q in Hyprland)
        std::cout << "✅ Item copied to clipboard" << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "❌ Error copying item: " << e.what() << std::endl;
    }
}

void MainWindow::on_delete_clicked(int64_t item_id) {
    std::cout << "🗑️  Borrando item ID: " << item_id << std::endl;
    clipboard_service_->delete_item(item_id);
    std::cout << "✅ Item borrado, recargando..." << std::endl;
    load_items();
}

void MainWindow::on_clear_all_clicked() {
    clipboard_service_->clear_all();
    load_items();
}

void MainWindow::load_items() {
    std::cout << "🔧 MainWindow: Loading items..." << std::endl;
    if (current_search_.empty()) {
        items_ = clipboard_service_->get_recent_items(20);
    } else if (search_service_) {
        items_ = search_service_->search(current_search_, 20);
    } else {
        items_ = clipboard_service_->get_recent_items(20);
    }
    
    std::cout << "✅ MainWindow: Loaded " << items_.size() << " items" << std::endl;
    update_item_list();
    status_label_.set_text(std::to_string(items_.size()) + " items");
}

void MainWindow::update_item_list() {
    std::cout << "🔧 MainWindow: Updating item list with " << items_.size() << " items..." << std::endl;
    
    // Clear existing items
    while (auto child = item_list_.get_first_child()) {
        item_list_.remove(*child);
    }
    
    std::cout << "🔧 MainWindow: Creating widgets..." << std::endl;
    // Add new items
    for (size_t i = 0; i < items_.size(); ++i) {
        const auto& item = items_[i];
        std::cout << "🔧 MainWindow: Creating widget " << (i+1) << "/" << items_.size() << std::endl;
        
        try {
            auto widget = Gtk::make_managed<ClipboardItemWidget>(item);
            
            // Connect delete signal
            widget->signal_delete().connect([this, id = item.id]() {
                on_delete_clicked(id);
            });
            
            // Connect to widget's own click signal (widget will emit it)
            // Use id capture only; avoid capturing the widget pointer to prevent dangling pointer crashes
            widget->signal_clicked().connect([this, id = item.id]() {
                on_item_clicked(id);
            });
            // Widget already attaches its internal click controller in its constructor
            
            item_list_.append(*widget);
            std::cout << "✅ MainWindow: Widget " << (i+1) << " added" << std::endl;
        } catch (const std::exception& e) {
            std::cerr << "❌ MainWindow: Failed to create widget: " << e.what() << std::endl;
        }
    }
    
    std::cout << "✅ MainWindow: Item list updated" << std::endl;
}

void MainWindow::refresh_from_daemon() {
    // Called from daemon thread, use idle to update UI in main thread
    Glib::signal_idle().connect_once([this]() {
        load_items();
    });
}
