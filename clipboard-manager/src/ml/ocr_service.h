#pragma once

#include <string>
#include <vector>
#include <tesseract/baseapi.h>
#include <leptonica/allheaders.h>

class OCRService {
public:
    explicit OCRService(const std::string& tessdata_path);
    ~OCRService();
    
    std::string extract_text(const std::vector<uint8_t>& image_data);
    
private:
    tesseract::TessBaseAPI* api_;
};
