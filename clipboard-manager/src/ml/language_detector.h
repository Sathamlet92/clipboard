#pragma once

#include <string>
#include <vector>
#include <map>
#include <unordered_map>
#include <onnxruntime_cxx_api.h>
#include <memory>

class LanguageDetector {
public:
    explicit LanguageDetector(const std::string& model_path);
    
    bool is_code(const std::string& text);
    std::string detect_language(const std::string& text);
    
private:
    std::unique_ptr<Ort::Env> env_;
    std::unique_ptr<Ort::Session> session_;
    std::unique_ptr<Ort::SessionOptions> session_options_;
    std::vector<std::string> labels_;
    std::map<std::string, int> vocab_;
    std::vector<std::pair<std::string, std::string>> merges_;
    std::unordered_map<std::string, int> merge_ranks_;
    
    float threshold_ = 5.11f;
    int max_length_ = 512;
    
    // Tokenization helpers
    std::vector<int> tokenize(const std::string& text);
    std::vector<std::string> pretokenize(const std::string& text);
    std::vector<std::string> bpe_encode(const std::string& word);
    void load_vocab(const std::string& path);
    void load_merges(const std::string& path);
    void load_labels(const std::string& path);
};
