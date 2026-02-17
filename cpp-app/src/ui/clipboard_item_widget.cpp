#include "clipboard_item_widget.h"
#include <iostream>
#include <regex>
#include <ctime>
#include <iomanip>
#include <sstream>
#include <gdkmm/pixbuf.h>
#include <gdkmm/texture.h>
#include <glib.h>
#include <glib.h>

// Helper function for basic syntax highlighting
static std::string apply_syntax_highlighting(const std::string& code, const std::string& language) {
    (void)language;
    std::string highlighted = Glib::Markup::escape_text(code);
    
    // Define color scheme
    const char* keyword_color = "#C678DD";     // Purple
    const char* string_color = "#98C379";      // Green
    const char* comment_color = "#5C6370";     // Gray
    const char* number_color = "#D19A66";      // Orange
    
    // Common keywords across languages
    std::vector<std::string> keywords = {
        "using", "namespace", "class", "static", "void", "public", "private", "protected",
        "return", "if", "else", "for", "while", "do", "switch", "case", "break", "continue",
        "new", "delete", "const", "var", "let", "function", "def", "import", "from",
        "try", "catch", "finally", "throw", "async", "await", "yield", "lambda",
        "true", "false", "null", "nullptr", "None", "True", "False", "this", "self",
        "int", "string", "bool", "float", "double", "char", "byte", "long", "short"
    };
    
    // Highlight strings (simple approach for single and double quotes)
    highlighted = std::regex_replace(highlighted, 
        std::regex("(&quot;[^&]*?&quot;)"),
        "<span foreground='" + std::string(string_color) + "'>$1</span>");
    
    // Highlight single-line comments
    highlighted = std::regex_replace(highlighted,
        std::regex("(//[^\n]*)"),
        "<span foreground='" + std::string(comment_color) + "' font_style='italic'>$1</span>");
    
    // Highlight numbers
    highlighted = std::regex_replace(highlighted,
        std::regex("\\b([0-9]+\\.?[0-9]*)\\b"),
        "<span foreground='" + std::string(number_color) + "'>$1</span>");
    
    // Highlight keywords
    for (const auto& keyword : keywords) {
        highlighted = std::regex_replace(highlighted,
            std::regex("\\b(" + keyword + ")\\b"),
            "<span foreground='" + std::string(keyword_color) + "' weight='bold'>$1</span>");
    }
    
    return highlighted;
}

ClipboardItemWidget::ClipboardItemWidget(const ClipboardItem& item)
    : Gtk::Box(Gtk::Orientation::VERTICAL)
    , item_(item)
{
    add_css_class("item-box");
    set_spacing(8);
    
    // ===== HEADER =====
    header_box_.set_spacing(10);
    
    // Type label with icon
    bool is_image_content = (item.type == ClipboardType::Image) || (item.content_type == "Image");
    std::string type_icon;
    std::string type_text;
    if (is_image_content) {
        type_icon = "🖼️";
        type_text = "image";
    } else if (item.type == ClipboardType::Text) {
        type_icon = "📝";
        type_text = "text";
    } else if (item.type == ClipboardType::Code) {
        type_icon = "💻";
        type_text = "code";
    } else if (item.type == ClipboardType::URL) {
        type_icon = "🔗";
        type_text = "url";
    } else {
        type_icon = "📄";
        type_text = "unknown";
    }
    
    type_label_.set_text(type_icon + " " + type_text);
    type_label_.add_css_class("type-badge");
    
    // Setup OCR notification badge (like iOS/Android notification dot)
    bool has_ocr_badge = false;
    if (!item.ocr_text.empty()) {
        if (!item.code_language.empty()) {
            has_ocr_badge = true;
        } else {
            std::string trimmed = item.ocr_text;
            trimmed.erase(0, trimmed.find_first_not_of(" \t\n\r"));
            trimmed.erase(trimmed.find_last_not_of(" \t\n\r") + 1);
            if (trimmed.length() >= 5) {
                has_ocr_badge = true;
            }
        }
    }
    if (has_ocr_badge) {
        ocr_badge_.set_text("OCR");
        ocr_badge_.add_css_class("ocr-notification");
        ocr_badge_.set_halign(Gtk::Align::END);
        ocr_badge_.set_valign(Gtk::Align::START);
    }
    
    // Setup language badge (only language, no "CODE" text)
    bool has_language_badge = false;
    if (!item.code_language.empty()) {
        // Single badge: Language name (e.g., "C#", "Python")
        language_badge_.set_text(item.code_language);
        language_badge_.add_css_class("language-notification");
        if (has_ocr_badge) {
            language_badge_.add_css_class("language-notification-ocr");
        }
        language_badge_.set_halign(Gtk::Align::END);
        language_badge_.set_valign(Gtk::Align::START);
        
        has_language_badge = true;
    }
    
    // Place badges to the right of the type label
    type_badge_box_.set_spacing(6);
    type_badge_box_.append(type_label_);
    if (has_ocr_badge) {
        ocr_badge_.set_margin_top(3);
        type_badge_box_.append(ocr_badge_);
    }
    if (has_language_badge) {
        language_badge_.set_margin_top(3);
        type_badge_box_.append(language_badge_);
    }
    
    // Time label
    std::time_t t = item.timestamp / 1000;
    std::tm* tm = std::localtime(&t);
    std::ostringstream oss;
    oss << std::put_time(tm, "%H:%M:%S");
    time_label_.set_text(oss.str());
    time_label_.set_hexpand(true);
    time_label_.set_halign(Gtk::Align::END);
    time_label_.add_css_class("time-label");
    
    // Delete button
    delete_button_.signal_clicked().connect(
        sigc::mem_fun(*this, &ClipboardItemWidget::on_delete));
    delete_button_.set_can_focus(false);
    
    header_box_.append(type_badge_box_);
    header_box_.append(time_label_);
    header_box_.append(delete_button_);
    
    append(header_box_);
    
    // ===== CONTENT =====
    content_box_.set_spacing(5);
    
    if (is_image_content) {
        // Show image
        if (!item.content.empty()) {
            try {
                auto loader = Gdk::PixbufLoader::create();
                loader->write(item.content.data(), item.content.size());
                loader->close();
                auto pixbuf = loader->get_pixbuf();
                
                if (pixbuf && pixbuf->get_width() > 0 && pixbuf->get_height() > 0) {
                    // ALWAYS scale to standard size for consistency
                    int target_width = 400;
                    int target_height = 200;
                    
                    // Calculate scale to fit within bounds while maintaining aspect ratio
                    double scale = std::min(
                        (double)target_width / pixbuf->get_width(),
                        (double)target_height / pixbuf->get_height()
                    );
                    
                    int new_width = std::max(1, (int)(pixbuf->get_width() * scale));
                    int new_height = std::max(1, (int)(pixbuf->get_height() * scale));
                    
                    pixbuf = pixbuf->scale_simple(new_width, new_height, Gdk::InterpType::BILINEAR);
                    
                    auto texture = Gdk::Texture::create_for_pixbuf(pixbuf);
                    image_picture_.set_paintable(texture);
                    image_picture_.set_can_shrink(false);
                    image_picture_.set_content_fit(Gtk::ContentFit::SCALE_DOWN);
                    image_picture_.set_size_request(target_width, target_height);
                    image_picture_.set_halign(Gtk::Align::START);
                    content_box_.append(image_picture_);
                } else {
                    content_label_.set_text("[Image data invalid]");
                    content_label_.add_css_class("error-label");
                    content_box_.append(content_label_);
                }
            } catch (const Glib::Error& e) {
                content_label_.set_text("[Error loading image: " + std::string(e.what()) + "]");
                content_label_.add_css_class("error-label");
                content_box_.append(content_label_);
            } catch (const std::exception& e) {
                content_label_.set_text("[Error loading image: " + std::string(e.what()) + "]");
                content_label_.add_css_class("error-label");
                content_box_.append(content_label_);
            } catch (...) {
                content_label_.set_text("[Image: " + std::to_string(item.content.size()) + " bytes - unknown error]");
                content_label_.add_css_class("error-label");
                content_box_.append(content_label_);
            }
        } else {
            content_label_.set_text("[Empty image data]");
            content_label_.add_css_class("error-label");
            content_box_.append(content_label_);
        }
        
        // Show OCR text preview
        if (!item.ocr_text.empty()) {
            if (!item.code_language.empty()) {
                std::string ocr_preview = item.ocr_text;
                if (ocr_preview.length() > 150) {
                    ocr_preview = ocr_preview.substr(0, 150) + "...";
                }
                ocr_label_.set_text(ocr_preview);
                ocr_label_.set_wrap(true);
                ocr_label_.set_xalign(0);
                ocr_label_.set_max_width_chars(60);
                ocr_label_.add_css_class("ocr-label");
                content_box_.append(ocr_label_);
            } else {
                std::string trimmed = item.ocr_text;
                trimmed.erase(0, trimmed.find_first_not_of(" \t\n\r"));
                trimmed.erase(trimmed.find_last_not_of(" \t\n\r") + 1);
                
                if (trimmed.length() >= 5) {
                    std::string ocr_preview = trimmed;
                    if (ocr_preview.length() > 150) {
                        ocr_preview = ocr_preview.substr(0, 150) + "...";
                    }
                    ocr_label_.set_text(ocr_preview);
                    ocr_label_.set_wrap(true);
                    ocr_label_.set_xalign(0);
                    ocr_label_.set_max_width_chars(60);
                    ocr_label_.add_css_class("ocr-label");
                    content_box_.append(ocr_label_);
                }
            }
        }
    } else {
        // Show text content
        std::string display_text;
        if (!item.content.empty()) {
            display_text = std::string(item.content.begin(), item.content.end());
        }
        
        if (display_text.length() > 300) {
            display_text = display_text.substr(0, 300) + "...";
        }
        
        // Check if URL
        bool is_url = item.type == ClipboardType::URL || 
                     (display_text.find("http://") == 0 || display_text.find("https://") == 0);
        
        // Check if code
        bool is_code = item.type == ClipboardType::Code;
        bool is_utf8 = g_utf8_validate(display_text.data(), display_text.size(), nullptr) != 0;
        
        if (!is_utf8) {
            content_label_.set_text("[Binary content]");
        } else if (is_url) {
            content_label_.set_markup("<span foreground='#4A90E2' underline='single'>" + 
                                     Glib::Markup::escape_text(display_text) + "</span>");
            content_label_.add_css_class("url-label");
        } else if (is_code) {
            // Apply syntax highlighting for code
            std::string highlighted = apply_syntax_highlighting(display_text, item.code_language);
            content_label_.set_markup(highlighted);
            content_label_.add_css_class("code-label");
        } else {
            content_label_.set_text(display_text);
        }
        
        content_label_.set_wrap(true);
        content_label_.set_xalign(0);
        content_label_.set_selectable(false);  // NOT selectable
        content_box_.append(content_label_);
    }
    
    append(content_box_);
    
    // ===== METADATA =====
    metadata_box_.set_spacing(10);
    
    if (!item.source_app.empty()) {
        source_label_.set_text("App: " + item.source_app);
        source_label_.add_css_class("metadata-label");
        metadata_box_.append(source_label_);
    }
    
    if (metadata_box_.get_first_child()) {
        append(metadata_box_);
    }
    
    // ===== ACTIONS =====
    actions_box_.set_spacing(5);
    bool has_actions = false;
    
    // URL button - only for URLs
    if (item.type == ClipboardType::URL) {
        url_button_.signal_clicked().connect(
            sigc::mem_fun(*this, &ClipboardItemWidget::on_open_url));
        url_button_.set_can_focus(false);
        actions_box_.append(url_button_);
        has_actions = true;
    }
    
    // Copy OCR button - ONLY for images with meaningful OCR text
    if (item.type == ClipboardType::Image && !item.ocr_text.empty()) {
        // Trim to check if it's actually empty (whitespace only)
        std::string trimmed = item.ocr_text;
        trimmed.erase(0, trimmed.find_first_not_of(" \t\n\r"));
        trimmed.erase(trimmed.find_last_not_of(" \t\n\r") + 1);
        
        // Only show button if OCR text has at least 5 characters
        // This filters out OCR noise like "N 4", "a", ".", etc.
        if (trimmed.length() >= 5) {
            copy_ocr_button_.signal_clicked().connect(
                sigc::mem_fun(*this, &ClipboardItemWidget::on_copy_ocr));
            copy_ocr_button_.set_can_focus(false);
            actions_box_.append(copy_ocr_button_);
            has_actions = true;
        }
    }
    
    if (has_actions) {
        append(actions_box_);
    }
    
    // Make text NOT selectable - only clickable
    content_label_.set_selectable(false);
    
    // Make the entire widget clickable
    // Use bubble phase so buttons can capture their clicks first
    auto gesture = Gtk::GestureClick::create();
    gesture->set_button(GDK_BUTTON_PRIMARY);
    gesture->set_propagation_phase(Gtk::PropagationPhase::BUBBLE);
    gesture->signal_released().connect([this](int, double, double) {
        // Use released instead of pressed for better button compatibility
        this->on_click();
    });
    add_controller(gesture);
    
    // Disable focus to prevent blue outline
    set_can_focus(false);
    set_focus_on_click(false);
}

void ClipboardItemWidget::on_click() {
    signal_clicked_.emit();
}


void ClipboardItemWidget::on_delete() {
    signal_delete_.emit();
}

void ClipboardItemWidget::on_open_url() {
    std::string text;
    if (!item_.content.empty()) {
        text = std::string(item_.content.begin(), item_.content.end());
    }
    
    std::regex url_regex(R"(https?://[^\s]+)");
    std::smatch match;
    
    if (std::regex_search(text, match, url_regex)) {
        std::string url = match.str();
        std::string cmd = "xdg-open '" + url + "' 2>/dev/null &";
        system(cmd.c_str());
    }
}

void ClipboardItemWidget::on_copy_ocr() {
    if (!item_.ocr_text.empty()) {
        FILE* pipe = popen("wl-copy 2>/dev/null", "w");
        if (pipe) {
            fwrite(item_.ocr_text.data(), 1, item_.ocr_text.size(), pipe);
            int result = pclose(pipe);
            if (result == 0) {
                std::cout << "✅ OCR text copied to clipboard" << std::endl;
            } else {
                std::cerr << "⚠️  Failed to copy OCR text" << std::endl;
            }
        } else {
            std::cerr << "⚠️  Failed to open wl-copy for OCR text" << std::endl;
        }
    }
}
