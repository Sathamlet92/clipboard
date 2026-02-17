#include "language_detector.h"
#include <algorithm>
#include <cctype>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iomanip>
#include <limits>
#include <sstream>
#include <unordered_set>
#include <nlohmann/json.hpp>

namespace fs = std::filesystem;
using json = nlohmann::json;

namespace {
std::vector<std::pair<std::string, std::string>> get_pairs(const std::vector<std::string>& chars) {
    std::vector<std::pair<std::string, std::string>> pairs;
    std::unordered_set<std::string> seen;
    for (size_t i = 0; i + 1 < chars.size(); i++) {
        std::string key = chars[i] + "\t" + chars[i + 1];
        if (seen.insert(key).second) {
            pairs.emplace_back(chars[i], chars[i + 1]);
        }
    }
    return pairs;
}

std::vector<std::string> merge_pair(const std::vector<std::string>& chars,
                                   const std::pair<std::string, std::string>& pair) {
    std::vector<std::string> result;
    size_t i = 0;
    while (i < chars.size()) {
        if (i + 1 < chars.size() && chars[i] == pair.first && chars[i + 1] == pair.second) {
            result.push_back(pair.first + pair.second);
            i += 2;
        } else {
            result.push_back(chars[i]);
            i++;
        }
    }
    return result;
}
}

LanguageDetector::LanguageDetector(const std::string& model_path) {
    env_ = std::make_unique<Ort::Env>(ORT_LOGGING_LEVEL_WARNING, "LanguageDetector");
    session_options_ = std::make_unique<Ort::SessionOptions>();
    session_options_->SetIntraOpNumThreads(2);

    fs::path model_file(model_path);
    fs::path model_dir = model_file.parent_path();

    session_ = std::make_unique<Ort::Session>(*env_, model_path.c_str(), *session_options_);

    load_vocab((model_dir / "vocab.json").string());
    load_merges((model_dir / "merges.txt").string());
    load_labels((model_dir / "labels.txt").string());

    std::cout << "✅ Language detector loaded with BPE tokenizer" << std::endl;
}

void LanguageDetector::load_vocab(const std::string& path) {
    std::ifstream file(path);
    if (!file.is_open()) {
        throw std::runtime_error("Cannot open vocab file: " + path);
    }

    json vocab_json = json::parse(file);
    for (auto& [key, value] : vocab_json.items()) {
        vocab_[key] = value.get<int>();
    }
}

void LanguageDetector::load_merges(const std::string& path) {
    std::ifstream file(path);
    if (!file.is_open()) {
        throw std::runtime_error("Cannot open merges file: " + path);
    }

    std::string line;
    bool first = true;
    int index = 0;
    while (std::getline(file, line)) {
        if (first && !line.empty() && line[0] == '#') {
            first = false;
            continue;
        }

        std::istringstream iss(line);
        std::string first_token;
        std::string second_token;
        if (iss >> first_token >> second_token) {
            merges_.emplace_back(first_token, second_token);
            std::string key = first_token + "\t" + second_token;
            merge_ranks_[key] = index++;
        }
    }
}

void LanguageDetector::load_labels(const std::string& path) {
    std::ifstream file(path);
    if (!file.is_open()) {
        throw std::runtime_error("Cannot open labels file: " + path);
    }

    std::string line;
    while (std::getline(file, line)) {
        if (!line.empty()) {
            labels_.push_back(line);
        }
    }
}

std::vector<std::string> LanguageDetector::pretokenize(const std::string& text) {
    std::vector<std::string> words;
    std::string current_word;

    for (unsigned char ch : text) {
        if (std::isspace(ch)) {
            if (!current_word.empty()) {
                words.push_back(current_word);
                current_word.clear();
            }
        } else if (std::ispunct(ch)) {
            if (!current_word.empty()) {
                words.push_back(current_word);
                current_word.clear();
            }
            words.push_back(std::string(1, static_cast<char>(ch)));
        } else {
            current_word += static_cast<char>(ch);
        }
    }

    if (!current_word.empty()) {
        words.push_back(current_word);
    }

    return words;
}

std::vector<std::string> LanguageDetector::bpe_encode(const std::string& word) {
    if (word.empty()) return {};

    std::vector<std::string> chars;
    chars.reserve(word.size());
    for (size_t i = 0; i < word.length(); i++) {
        if (i == 0) {
            chars.push_back("Ġ" + std::string(1, word[i]));
        } else {
            chars.push_back(std::string(1, word[i]));
        }
    }

    while (chars.size() > 1) {
        auto pairs = get_pairs(chars);
        if (pairs.empty()) break;

        int best_rank = std::numeric_limits<int>::max();
        std::pair<std::string, std::string> best_pair;
        bool found = false;

        for (const auto& pair : pairs) {
            std::string key = pair.first + "\t" + pair.second;
            auto it = merge_ranks_.find(key);
            if (it != merge_ranks_.end() && it->second < best_rank) {
                best_rank = it->second;
                best_pair = pair;
                found = true;
            }
        }

        if (!found) break;

        chars = merge_pair(chars, best_pair);
    }

    return chars;
}

std::vector<int> LanguageDetector::tokenize(const std::string& text) {
    const int BOS_TOKEN_ID = 0;
    const int EOS_TOKEN_ID = 2;
    const int UNK_TOKEN_ID = 3;

    std::vector<int> tokens;
    tokens.reserve(max_length_);
    tokens.push_back(BOS_TOKEN_ID);

    auto words = pretokenize(text);
    for (const auto& word : words) {
        if (tokens.size() >= static_cast<size_t>(max_length_ - 1)) break;

        auto word_tokens = bpe_encode(word);
        for (const auto& token_str : word_tokens) {
            if (tokens.size() >= static_cast<size_t>(max_length_ - 1)) break;

            auto it = vocab_.find(token_str);
            if (it != vocab_.end()) {
                tokens.push_back(it->second);
            } else {
                tokens.push_back(UNK_TOKEN_ID);
            }
        }
    }

    tokens.push_back(EOS_TOKEN_ID);
    return tokens;
}

bool LanguageDetector::is_code(const std::string& text) {
    return !detect_language(text).empty();
}

std::string LanguageDetector::detect_language(const std::string& text) {
    if (!session_ || text.empty() || labels_.empty() || vocab_.empty() || merges_.empty()) {
        return "";
    }

    try {
        std::string truncated = text.length() > 2000 ? text.substr(0, 2000) : text;
        auto tokens = tokenize(truncated);

        std::vector<int64_t> input_ids(max_length_, 0);
        std::vector<int64_t> attention_mask(max_length_, 0);

        for (size_t i = 0; i < std::min<size_t>(tokens.size(), max_length_); i++) {
            input_ids[i] = tokens[i];
            attention_mask[i] = 1;
        }

        std::vector<int64_t> input_shape = {1, max_length_};
        auto memory_info = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);

        Ort::Value input_ids_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, input_ids.data(), input_ids.size(),
            input_shape.data(), input_shape.size());

        Ort::Value attention_mask_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, attention_mask.data(), attention_mask.size(),
            input_shape.data(), input_shape.size());

        std::vector<int64_t> token_type_ids;
        Ort::Value token_type_ids_tensor{nullptr};

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
                token_type_ids.assign(max_length_, 0);
                token_type_ids_tensor = Ort::Value::CreateTensor<int64_t>(
                    memory_info, token_type_ids.data(), token_type_ids.size(),
                    input_shape.data(), input_shape.size());
                input_tensors.emplace_back(std::move(token_type_ids_tensor));
            } else {
                throw std::runtime_error("Unknown model input: " + name);
            }
        }

        auto out_name_ptr = session_->GetOutputNameAllocated(0, allocator);
        std::string output_name = out_name_ptr.get();
        const char* output_names[] = {output_name.c_str()};

        auto output_tensors = session_->Run(
            Ort::RunOptions{nullptr},
            input_names.data(), input_tensors.data(), input_tensors.size(),
            output_names, 1);

        float* logits = output_tensors[0].GetTensorMutableData<float>();
        auto type_info = output_tensors[0].GetTensorTypeAndShapeInfo();
        size_t logits_size = type_info.GetElementCount();

        size_t max_idx = 0;
        float max_val = logits[0];
        size_t limit = std::min(logits_size, labels_.size());
        for (size_t i = 1; i < limit; i++) {
            if (logits[i] > max_val) {
                max_val = logits[i];
                max_idx = i;
            }
        }

        // Log top-3 scores to debug threshold tuning
        std::vector<std::pair<float, size_t>> scored;
        scored.reserve(limit);
        for (size_t i = 0; i < limit; i++) {
            scored.emplace_back(logits[i], i);
        }
        std::partial_sort(scored.begin(), scored.begin() + std::min<size_t>(3, scored.size()), scored.end(),
            [](const auto& a, const auto& b) { return a.first > b.first; });

        std::ostringstream top;
        size_t top_n = std::min<size_t>(3, scored.size());
        for (size_t i = 0; i < top_n; i++) {
            if (i > 0) top << ", ";
            top << labels_[scored[i].second] << "=" << std::fixed << std::setprecision(2) << scored[i].first;
        }
        std::cout << "🔍 ML scores (top " << top_n << "): " << top.str()
                  << " | threshold=" << std::fixed << std::setprecision(2) << threshold_ << std::endl;

        if (max_val < threshold_) {
            return "";
        }

        return labels_[max_idx];
    } catch (const std::exception& e) {
        std::cerr << "❌ Error in detection: " << e.what() << std::endl;
        return "";
    }
}
