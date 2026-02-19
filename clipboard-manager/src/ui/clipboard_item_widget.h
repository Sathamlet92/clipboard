#pragma once

#include <gtkmm.h>
#include "../database/clipboard_db.h"

class ClipboardItemWidget : public Gtk::Box {
public:
    explicit ClipboardItemWidget(const ClipboardItem& item);
    virtual ~ClipboardItemWidget() = default;
    
    sigc::signal<void()>& signal_clicked() { return signal_clicked_; }
    sigc::signal<void()>& signal_delete() { return signal_delete_; }
    
protected:
    void on_click();
    void on_delete();
    void on_open_url();
    void on_copy_ocr();
    
private:
    ClipboardItem item_;
    
    // UI Components
    Gtk::Box header_box_{Gtk::Orientation::HORIZONTAL};
    Gtk::Box type_badge_box_{Gtk::Orientation::HORIZONTAL};
    Gtk::Label type_label_;
    Gtk::Label ocr_badge_;  // Small notification badge for OCR
    Gtk::Label code_badge_;  // Badge for CODE
    Gtk::Label language_badge_;  // Badge for programming language
    Gtk::Label time_label_;
    Gtk::Button delete_button_{"🗑️"};
    
    // Content area
    Gtk::Box content_box_{Gtk::Orientation::VERTICAL};
    Gtk::Label content_label_;
    Gtk::Picture image_picture_;
    
    // Metadata
    Gtk::Box metadata_box_{Gtk::Orientation::HORIZONTAL};
    Gtk::Label source_label_;
    Gtk::Label ocr_label_;
    
    // Actions
    Gtk::Box actions_box_{Gtk::Orientation::HORIZONTAL};
    Gtk::Button url_button_{"🌐 Abrir"};
    Gtk::Button copy_ocr_button_{"📝 Copiar texto"};
    
    // Signals
    sigc::signal<void()> signal_clicked_;
    sigc::signal<void()> signal_delete_;
};
