#include "clipboard_service.h"
#include "../ml/embedding_service.h"
#include "../ml/language_detector.h"
#include "../ml/ocr_service.h"
#include <iostream>
#include <filesystem>
#include <chrono>
#include <cstring>
#include <regex>
#include <thread>

namespace {
bool is_url_like(const std::string& input);
bool is_json_like(const std::string& input);
std::string detect_code_language(const std::string& text, LanguageDetector* detector);
std::string build_embedding_text(const ClipboardItem& item);
}

ClipboardService::ClipboardService(std::shared_ptr<ClipboardDB> db)
    : db_(db)
{
    models_path_ = std::string(getenv("HOME")) + "/.clipboard-manager/models";
    std::cout << "✅ Clipboard service ready (ML/OCR lazy init enabled)" << std::endl;
}

ClipboardService::~ClipboardService() = default;

EmbeddingService* ClipboardService::get_embedding_service() {
    std::call_once(embedding_init_once_, [this]() {
        try {
            embedding_service_ = std::make_unique<EmbeddingService>(models_path_ + "/ml/embedding-model.onnx");
            std::cout << "✅ Embedding service enabled" << std::endl;
        } catch (const std::exception& e) {
            std::cerr << "⚠️  Embedding service disabled: " << e.what() << std::endl;
        }
    });
    return embedding_service_.get();
}

LanguageDetector* ClipboardService::get_language_detector() {
    std::call_once(language_init_once_, [this]() {
        try {
            language_detector_ = std::make_unique<LanguageDetector>(models_path_ + "/language-detection/model.onnx");
            std::cout << "✅ Language detector enabled" << std::endl;
        } catch (const std::exception& e) {
            std::cerr << "⚠️  Language detector disabled: " << e.what() << std::endl;
        }
    });
    return language_detector_.get();
}

OCRService* ClipboardService::get_ocr_service() {
    std::call_once(ocr_init_once_, [this]() {
        try {
            std::string user_tessdata = models_path_ + "/tessdata";
            std::string system_tessdata = "/usr/share/tessdata";
            std::string tessdata_path = std::filesystem::exists(user_tessdata)
                ? user_tessdata
                : system_tessdata;
            ocr_service_ = std::make_unique<OCRService>(tessdata_path);
            std::cout << "✅ OCR service enabled" << std::endl;
        } catch (const std::exception& e) {
            std::cerr << "⚠️  OCR service disabled: " << e.what() << std::endl;
        }
    });
    return ocr_service_.get();
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
    
    // Insert into database
    int64_t id = db_->insert(item);
    if (id > 0) {
        std::cout << "✅ Item saved: " << id << std::endl;
        
        // Detect code language in BACKGROUND (like C# does)
        auto* language_detector = get_language_detector();
        if (item.type == ClipboardType::Text && language_detector) {
            std::string text = item.text_content;
            auto db = db_;
            auto lang_detector = language_detector;
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

        // Generate embedding in BACKGROUND so copy events stay responsive
        if (auto* embedding_service = get_embedding_service()) {
            auto db = db_;
            auto embedder = embedding_service;
            auto items_updated = items_updated_callback_;
            ClipboardItem snapshot = item;

            std::thread([id, snapshot, db, embedder, items_updated]() {
                try {
                    auto fresh_item = db->get(id);
                    if (!fresh_item) {
                        return;
                    }

                    std::string embedding_text = build_embedding_text(*fresh_item);
                    if (embedding_text.empty()) {
                        embedding_text = build_embedding_text(snapshot);
                    }

                    if (embedding_text.empty()) {
                        return;
                    }

                    auto emb = embedder->generate_embedding(embedding_text);
                    if (emb.empty()) {
                        return;
                    }

                    fresh_item->embedding = std::move(emb);
                    db->update(*fresh_item);
                    if (items_updated) {
                        items_updated();
                    }
                } catch (const std::exception& e) {
                    std::cerr << "⚠️  Error generating embedding: " << e.what() << std::endl;
                }
            }).detach();
        }

        // OCR for images in BACKGROUND (can be expensive)
        if (item.type == ClipboardType::Image) {
            auto* ocr_service = get_ocr_service();
            auto* language_detector_bg = get_language_detector();
            auto* embedding_service_bg = get_embedding_service();
            if (ocr_service) {
                auto db = db_;
                auto ocr = ocr_service;
                auto lang_detector = language_detector_bg;
                auto embedder = embedding_service_bg;
                auto items_updated = items_updated_callback_;

                std::thread([id, db, ocr, lang_detector, embedder, items_updated]() {
                    try {
                        auto fresh_item = db->get(id);
                        if (!fresh_item || fresh_item->type != ClipboardType::Image) {
                            return;
                        }

                        std::string extracted = ocr->extract_text(fresh_item->content);
                        if (extracted.empty()) {
                            return;
                        }

                        fresh_item->ocr_text = extracted;
                        std::string language = detect_code_language(extracted, lang_detector);
                        if (!language.empty()) {
                            fresh_item->code_language = language;
                        }

                        if (embedder) {
                            std::string embedding_text = build_embedding_text(*fresh_item);
                            auto emb = embedder->generate_embedding(embedding_text);
                            if (!emb.empty()) {
                                fresh_item->embedding = std::move(emb);
                            }
                        }

                        db->update(*fresh_item);
                        if (items_updated) {
                            items_updated();
                        }
                    } catch (const std::exception& e) {
                        std::cerr << "⚠️  Error in background OCR: " << e.what() << std::endl;
                    }
                }).detach();
            }
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

std::string build_embedding_text(const ClipboardItem& item) {
    std::string text;

    if (!item.content.empty() && item.type != ClipboardType::Image) {
        text.assign(item.content.begin(), item.content.end());
    }

    if (!item.ocr_text.empty()) {
        if (!text.empty()) text += "\n";
        text += item.ocr_text;
    }

    if (!item.code_language.empty()) {
        if (!text.empty()) text += "\n";
        text += "language: " + item.code_language;
    }

    if (!item.content_type.empty()) {
        if (!text.empty()) text += "\n";
        text += "type: " + item.content_type;
    }

    return text;
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
    if (auto* ocr_service = get_ocr_service()) {
        item.ocr_text = ocr_service->extract_text(item.content);
        
        if (!item.ocr_text.empty()) {
            // Check if OCR text is code
            std::string language = detect_code_language(item.ocr_text, get_language_detector());
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
