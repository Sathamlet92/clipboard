#include "clipboard_service.h"
#include <iostream>
#include <chrono>
#include <cstring>
#include <regex>
#include <thread>

namespace {
bool is_url_like(const std::string& input);
bool is_json_like(const std::string& input);
std::string detect_code_language(const std::string& text, LanguageDetector* detector);
}

ClipboardService::ClipboardService(std::shared_ptr<ClipboardDB> db)
    : db_(db)
{
    auto models_path = std::string(getenv("HOME")) + "/.clipboard-manager/models";
    
    try {
        embedding_service_ = std::make_unique<EmbeddingService>(models_path + "/ml/embedding-model.onnx");
        std::cout << "✅ Embedding service enabled" << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "⚠️  Embedding service disabled: " << e.what() << std::endl;
    }
    
    try {
        language_detector_ = std::make_unique<LanguageDetector>(models_path + "/language-detection/model.onnx");
        std::cout << "✅ Language detector enabled" << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "⚠️  Language detector disabled: " << e.what() << std::endl;
    }
    
    try {
        ocr_service_ = std::make_unique<OCRService>("/usr/share/tessdata");
        std::cout << "✅ OCR service enabled" << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "⚠️  OCR service disabled: " << e.what() << std::endl;
    }
}

void ClipboardService::set_items_updated_callback(std::function<void()> callback) {
    items_updated_callback_ = std::move(callback);
}

void ClipboardService::process_event(const ClipboardEvent& event) {
    // Check for duplicates - search entire database, including OCR text
    std::vector<uint8_t> content_to_check;
    
    if (!event.image_data.empty()) {
        content_to_check = event.image_data;
    } else if (!event.text_content.empty()) {
        content_to_check.assign(event.text_content.begin(), event.text_content.end());
    }
    
    if (!content_to_check.empty() && db_->content_exists(content_to_check)) {
        std::cout << "⏭️  Duplicate content ignored (already exists in database or as OCR text)" << std::endl;
        return;
    }
    
    ClipboardItem item;
    item.timestamp = event.timestamp > 0 ? event.timestamp : 
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count();
    item.source_app = "";
    
    if (!event.image_data.empty()) {
        // Image content
        item.type = ClipboardType::Image;
        item.content.assign(event.image_data.begin(), event.image_data.end());
        item.mime_type = "image/png";
        process_image(item);
    } else if (!event.text_content.empty()) {
        // Text content - store as blob - ALWAYS start as Text
        item.text_content = event.text_content;
        item.content.assign(event.text_content.begin(), event.text_content.end());
        item.mime_type = "text/plain";
        item.type = ClipboardType::Text;  // Always Text initially
        process_text(item);
    } else {
        std::cerr << "⚠️  Empty clipboard event" << std::endl;
        return;
    }
    
    // Generate embedding in background
    if (!item.get_text().empty() && embedding_service_) {
        item.embedding = embedding_service_->generate_embedding(item.get_text());
    }
    
    // Insert into database
    int64_t id = db_->insert(item);
    if (id > 0) {
        std::cout << "✅ Item saved: " << id << std::endl;
        
        // Detect code language in BACKGROUND (like C# does)
        if (item.type == ClipboardType::Text && language_detector_) {
            std::string text = item.text_content;
            auto db = db_;
            auto lang_detector = language_detector_.get();
            auto items_updated = items_updated_callback_;
            
            std::thread([id, text, db, lang_detector, items_updated]() {
                try {
                    std::string language = detect_code_language(text, lang_detector);
                    if (!language.empty()) {
                        // ML detected code - update item
                        auto fresh_item = db->get(id);
                        if (fresh_item) {
                            fresh_item->type = ClipboardType::Code;
                            fresh_item->code_language = language;
                            db->update(*fresh_item);
                            std::cout << "✅ Language detected for item " << id << ": " << language << std::endl;
                            if (items_updated) {
                                items_updated();
                            }
                        }
                    }
                } catch (const std::exception& e) {
                    std::cerr << "⚠️  Error detecting language: " << e.what() << std::endl;
                }
            }).detach();
        }
    } else {
        std::cerr << "❌ Failed to save item" << std::endl;
    }
}

namespace {
bool is_url_like(const std::string& input) {
    std::string text = input;
    auto start = text.find_first_not_of(" \t\n\r");
    auto end = text.find_last_not_of(" \t\n\r");
    if (start == std::string::npos) {
        return false;
    }
    text = text.substr(start, end - start + 1);

    if (text.length() > 2048) {
        return false;
    }

    if (text.find('\n') != std::string::npos || text.find('\r') != std::string::npos) {
        return false;
    }

    static const std::regex url_regex(
        R"(^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$)",
        std::regex::icase);

    return std::regex_match(text, url_regex);
}

bool is_json_like(const std::string& input) {
    std::string text = input;
    auto start = text.find_first_not_of(" \t\n\r");
    auto end = text.find_last_not_of(" \t\n\r");
    if (start == std::string::npos) {
        return false;
    }
    text = text.substr(start, end - start + 1);

    if (text.length() < 2) {
        return false;
    }

    char first = text.front();
    char last = text.back();
    if (!((first == '{' && last == '}') || (first == '[' && last == ']'))) {
        return false;
    }

    bool in_string = false;
    bool escape = false;
    int brace = 0;
    int bracket = 0;
    bool has_colon = false;

    for (char ch : text) {
        if (escape) {
            escape = false;
            continue;
        }
        if (ch == '\\') {
            if (in_string) {
                escape = true;
            }
            continue;
        }
        if (ch == '"') {
            in_string = !in_string;
            continue;
        }
        if (in_string) {
            continue;
        }
        if (ch == '{') brace++;
        if (ch == '}') brace--;
        if (ch == '[') bracket++;
        if (ch == ']') bracket--;
        if (ch == ':') has_colon = true;
        if (brace < 0 || bracket < 0) {
            return false;
        }
    }

    if (brace != 0 || bracket != 0 || in_string) {
        return false;
    }

    if (first == '{' && !has_colon) {
        return false;
    }

    return true;
}

std::string detect_code_language(const std::string& text, LanguageDetector* detector) {
    if (detector) {
        std::string language = detector->detect_language(text);
        if (!language.empty()) {
            return language;
        }
    }

    if (is_json_like(text)) {
        return "JSON";
    }

    return "";
}
}

ClipboardType ClipboardService::classify_content(const std::string& text) {
    if (is_url_like(text)) {
        return ClipboardType::URL;
    }

    return ClipboardType::Text;
}

void ClipboardService::process_image(ClipboardItem& item) {
    // Run OCR
    if (ocr_service_) {
        item.ocr_text = ocr_service_->extract_text(item.content);
        
        if (!item.ocr_text.empty()) {
            // Check if OCR text is code
            std::string language = detect_code_language(item.ocr_text, language_detector_.get());
            if (!language.empty()) {
                item.code_language = language;
            }
        }
    }
}

void ClipboardService::process_text(ClipboardItem& item) {
    // Check for URL first (same pattern as .NET)
    if (is_url_like(item.text_content)) {
        item.type = ClipboardType::URL;
    }
}

std::optional<ClipboardItem> ClipboardService::get_item(int64_t id) {
    return db_->get(id);
}

std::vector<ClipboardItem> ClipboardService::get_recent_items(int limit) {
    std::cout << "🔧 Service: Getting recent items..." << std::endl;
    auto items = db_->get_recent(limit);
    std::cout << "✅ Service: Got " << items.size() << " items" << std::endl;
    return items;
}

void ClipboardService::delete_item(int64_t id) {
    std::cout << "🔧 ClipboardService: Borrando item " << id << std::endl;
    bool success = db_->delete_item(id);
    if (success) {
        std::cout << "✅ Item " << id << " borrado de BD" << std::endl;
    } else {
        std::cerr << "❌ Error borrando item " << id << std::endl;
    }
}

void ClipboardService::clear_all() {
    db_->delete_all();
}

void ClipboardService::copy_to_clipboard(const ClipboardItem& item) {
    try {
        if (item.type == ClipboardType::Image) {
            // For images, write to temp file and use wl-copy
            std::string temp_file = "/tmp/clipboard_temp_" + std::to_string(item.id) + ".png";
            FILE* f = fopen(temp_file.c_str(), "wb");
            if (f) {
                fwrite(item.content.data(), 1, item.content.size(), f);
                fclose(f);
                
                std::string cmd = "wl-copy < " + temp_file + " 2>/dev/null";
                int result = system(cmd.c_str());
                
                // Clean up temp file
                unlink(temp_file.c_str());
                
                if (result == 0) {
                    std::cout << "✅ Image copied to clipboard" << std::endl;
                } else {
                    std::cerr << "⚠️  Failed to copy image" << std::endl;
                }
            }
        } else {
            // For text, use wl-copy directly
            FILE* pipe = popen("wl-copy 2>/dev/null", "w");
            if (pipe) {
                std::string text;
                if (!item.content.empty()) {
                    text = std::string(item.content.begin(), item.content.end());
                }
                if (!text.empty()) {
                    fwrite(text.data(), 1, text.size(), pipe);
                    pclose(pipe);
                    std::cout << "✅ Text copied to clipboard" << std::endl;
                } else {
                    pclose(pipe);
                    std::cerr << "⚠️  Empty text content" << std::endl;
                }
            } else {
                std::cerr << "⚠️  Failed to open wl-copy" << std::endl;
            }
        }
    } catch (const std::exception& e) {
        std::cerr << "❌ Error in copy_to_clipboard: " << e.what() << std::endl;
    }
}
