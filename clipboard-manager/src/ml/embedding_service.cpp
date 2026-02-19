#include "embedding_service.h"
#include <iostream>
#include <algorithm>
#include <cctype>
#include <numeric>
#include <filesystem>
#include <fstream>
#include <limits>
#include <nlohmann/json.hpp>

using json = nlohmann::json;
namespace fs = std::filesystem;

EmbeddingService::EmbeddingService(const std::string& model_path) {
    env_ = std::make_unique<Ort::Env>(ORT_LOGGING_LEVEL_WARNING, "EmbeddingService");
    session_options_ = std::make_unique<Ort::SessionOptions>();
    session_options_->SetIntraOpNumThreads(4);
    session_options_->SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_ALL);
    
    session_ = std::make_unique<Ort::Session>(*env_, model_path.c_str(), *session_options_);
    load_tokenizer(model_path);
    
    std::cout << "✅ Embedding model loaded" << std::endl;
}

void EmbeddingService::load_tokenizer(const std::string& model_path) {
    fs::path model_file(model_path);
    fs::path tokenizer_file = model_file.parent_path() / "tokenizer.json";

    std::ifstream file(tokenizer_file);
    if (!file.is_open()) {
        throw std::runtime_error("Cannot open tokenizer file: " + tokenizer_file.string());
    }

    json j = json::parse(file);

    auto model = j.value("model", json::object());
    if (model.value("type", std::string()) != "Unigram") {
        throw std::runtime_error("Unsupported tokenizer type for embeddings (expected Unigram)");
    }

    if (model.contains("unk_id") && !model["unk_id"].is_null()) {
        unk_id_ = model["unk_id"].get<int64_t>();
    }

    if (j.contains("truncation") && j["truncation"].is_object()) {
        max_length_ = j["truncation"].value("max_length", max_length_);
    }
    if (max_length_ < 8) {
        max_length_ = 128;
    }

    if (j.contains("padding") && j["padding"].is_object()) {
        pad_id_ = j["padding"].value("pad_id", pad_id_);
    }

    if (j.contains("post_processor") && j["post_processor"].is_object()) {
        auto pp = j["post_processor"];
        if (pp.contains("special_tokens") && pp["special_tokens"].is_object()) {
            auto special = pp["special_tokens"];
            if (special.contains("<s>") && special["<s>"].contains("ids") && !special["<s>"]["ids"].empty()) {
                bos_id_ = special["<s>"]["ids"][0].get<int64_t>();
            }
            if (special.contains("</s>") && special["</s>"].contains("ids") && !special["</s>"]["ids"].empty()) {
                eos_id_ = special["</s>"]["ids"][0].get<int64_t>();
            }
        }
    }

    auto vocab = model.value("vocab", json::array());
    unigram_vocab_.clear();
    unigram_vocab_.reserve(vocab.size());
    max_piece_bytes_ = 0;

    for (size_t i = 0; i < vocab.size(); ++i) {
        const auto& row = vocab[i];
        if (!row.is_array() || row.size() < 2 || !row[0].is_string()) {
            continue;
        }

        std::string piece = row[0].get<std::string>();
        float score = 0.0f;
        if (row[1].is_number_float() || row[1].is_number_integer()) {
            score = row[1].get<float>();
        }

        unigram_vocab_[piece] = UnigramEntry{static_cast<int64_t>(i), score};
        max_piece_bytes_ = std::max(max_piece_bytes_, piece.size());
    }

    if (unigram_vocab_.empty()) {
        throw std::runtime_error("Tokenizer vocab is empty");
    }
}

std::vector<float> EmbeddingService::generate_embedding(const std::string& text) {
    if (!session_) {
        return {};
    }
    
    try {
        // Tokenize
        auto tokens = tokenize(text);
        
        // Prepare inputs
        std::vector<int64_t> input_shape = {1, static_cast<int64_t>(tokens.size())};
        auto memory_info = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
        Ort::Value input_ids_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, tokens.data(), tokens.size(), input_shape.data(), input_shape.size());

        std::vector<int64_t> attention_mask(tokens.size(), 0);
        for (size_t i = 0; i < tokens.size(); i++) {
            attention_mask[i] = tokens[i] == pad_id_ ? 0 : 1;
        }
        Ort::Value attention_mask_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, attention_mask.data(), attention_mask.size(),
            input_shape.data(), input_shape.size());

        std::vector<int64_t> token_type_ids(tokens.size(), 0);
        Ort::Value token_type_ids_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, token_type_ids.data(), token_type_ids.size(),
            input_shape.data(), input_shape.size());

        Ort::AllocatorWithDefaultOptions allocator;
        size_t input_count = session_->GetInputCount();
        std::vector<std::string> input_names_storage;
        std::vector<const char*> input_names;
        std::vector<Ort::Value> input_tensors;
        input_names_storage.reserve(input_count);
        input_names.reserve(input_count);
        input_tensors.reserve(input_count);

        for (size_t i = 0; i < input_count; i++) {
            auto name_ptr = session_->GetInputNameAllocated(i, allocator);
            std::string name = name_ptr.get();
            input_names_storage.push_back(name);
            input_names.push_back(input_names_storage.back().c_str());

            if (name == "input_ids") {
                input_tensors.emplace_back(std::move(input_ids_tensor));
            } else if (name == "attention_mask") {
                input_tensors.emplace_back(std::move(attention_mask_tensor));
            } else if (name == "token_type_ids") {
                input_tensors.emplace_back(std::move(token_type_ids_tensor));
            } else {
                throw std::runtime_error("Unknown model input: " + name);
            }
        }

        auto out_name_ptr = session_->GetOutputNameAllocated(0, allocator);
        std::string output_name = out_name_ptr.get();
        const char* output_names[] = {output_name.c_str()};

        auto output_tensors = session_->Run(
            Ort::RunOptions{nullptr}, input_names.data(), input_tensors.data(), input_tensors.size(),
            output_names, 1);
        
        // Get output
        float* output_data = output_tensors[0].GetTensorMutableData<float>();
        auto output_shape = output_tensors[0].GetTensorTypeAndShapeInfo().GetShape();
        
        size_t seq_len = output_shape[1];
        size_t hidden_size = output_shape[2];
        
        std::vector<float> token_embeddings(output_data, output_data + (seq_len * hidden_size));
        
        // Mean pooling
        return mean_pooling(token_embeddings, seq_len, hidden_size);
        
    } catch (const std::exception& e) {
        std::cerr << "❌ Embedding generation failed: " << e.what() << std::endl;
        return {};
    }
}

std::vector<int64_t> EmbeddingService::tokenize(const std::string& text) {
    std::vector<int64_t> tokens;
    tokens.reserve(static_cast<size_t>(max_length_));
    tokens.push_back(bos_id_);

    for (const auto& word : whitespace_split(text)) {
        if (tokens.size() >= static_cast<size_t>(max_length_ - 1)) {
            break;
        }

        std::string metaspace_piece = "▁" + word;
        auto encoded = unigram_encode_piece(metaspace_piece);
        for (int64_t token_id : encoded) {
            if (tokens.size() >= static_cast<size_t>(max_length_ - 1)) {
                break;
            }
            tokens.push_back(token_id);
        }
    }

    tokens.push_back(eos_id_);

    if (tokens.size() > static_cast<size_t>(max_length_)) {
        tokens.resize(static_cast<size_t>(max_length_));
        tokens.back() = eos_id_;
    } else {
        while (tokens.size() < static_cast<size_t>(max_length_)) {
            tokens.push_back(pad_id_);
        }
    }

    return tokens;
}

std::vector<std::string> EmbeddingService::whitespace_split(const std::string& text) const {
    std::vector<std::string> result;
    std::string current;

    for (unsigned char ch : text) {
        if (std::isspace(ch)) {
            if (!current.empty()) {
                result.push_back(current);
                current.clear();
            }
        } else {
            current.push_back(static_cast<char>(ch));
        }
    }

    if (!current.empty()) {
        result.push_back(current);
    }

    if (result.empty()) {
        result.push_back("");
    }
    return result;
}

size_t EmbeddingService::next_utf8_char_len(const std::string& text, size_t offset) const {
    if (offset >= text.size()) return 0;

    unsigned char c = static_cast<unsigned char>(text[offset]);
    if ((c & 0x80) == 0x00) return 1;
    if ((c & 0xE0) == 0xC0) return (offset + 2 <= text.size()) ? 2 : 1;
    if ((c & 0xF0) == 0xE0) return (offset + 3 <= text.size()) ? 3 : 1;
    if ((c & 0xF8) == 0xF0) return (offset + 4 <= text.size()) ? 4 : 1;
    return 1;
}

std::vector<int64_t> EmbeddingService::unigram_encode_piece(const std::string& piece) const {
    if (piece.empty()) return {unk_id_};

    const size_t n = piece.size();
    const float neg_inf = -std::numeric_limits<float>::infinity();

    std::vector<float> best(n + 1, neg_inf);
    std::vector<size_t> prev(n + 1, n + 1);
    std::vector<int64_t> prev_id(n + 1, unk_id_);
    best[0] = 0.0f;

    for (size_t i = 0; i < n; ++i) {
        if (!std::isfinite(best[i])) {
            continue;
        }

        size_t max_len = std::min(max_piece_bytes_, n - i);
        bool found_piece = false;
        for (size_t len = 1; len <= max_len; ++len) {
            std::string candidate = piece.substr(i, len);
            auto it = unigram_vocab_.find(candidate);
            if (it == unigram_vocab_.end()) {
                continue;
            }

            found_piece = true;
            size_t j = i + len;
            float score = best[i] + it->second.score;
            if (score > best[j]) {
                best[j] = score;
                prev[j] = i;
                prev_id[j] = it->second.id;
            }
        }

        if (!found_piece) {
            size_t len = next_utf8_char_len(piece, i);
            if (len == 0) len = 1;
            size_t j = std::min(n, i + len);
            float score = best[i] - 20.0f;
            if (score > best[j]) {
                best[j] = score;
                prev[j] = i;
                prev_id[j] = unk_id_;
            }
        }
    }

    if (!std::isfinite(best[n])) {
        return {unk_id_};
    }

    std::vector<int64_t> ids;
    for (size_t pos = n; pos > 0 && prev[pos] <= n; pos = prev[pos]) {
        ids.push_back(prev_id[pos]);
        if (prev[pos] == pos) break;
    }

    std::reverse(ids.begin(), ids.end());
    if (ids.empty()) {
        ids.push_back(unk_id_);
    }
    return ids;
}

std::vector<float> EmbeddingService::mean_pooling(
    const std::vector<float>& token_embeddings, size_t seq_len, size_t hidden_size) {
    
    std::vector<float> result(hidden_size, 0.0f);
    
    for (size_t i = 0; i < seq_len; ++i) {
        for (size_t j = 0; j < hidden_size; ++j) {
            result[j] += token_embeddings[i * hidden_size + j];
        }
    }
    
    for (float& val : result) {
        val /= seq_len;
    }
    
    return result;
}
