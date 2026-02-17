#include <iostream>
#include "app/bootstrap.h"

int main(int argc, char* argv[]) {
    try {
        AppBootstrap bootstrap;
        return bootstrap.run(argc, argv);
    } catch (const std::exception& e) {
        std::cerr << "💥 FATAL ERROR: " << e.what() << std::endl;
        return 1;
    } catch (...) {
        std::cerr << "💥 FATAL ERROR: Unknown exception" << std::endl;
        return 1;
    }
}
