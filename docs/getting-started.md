# Getting Started

## Requirements
- C++17
- Ninja 1.13+
- Compiler: Clang, MSVC or GCC

## Assembly
\`\`\`bash
git clone https://github.com/your-nick/GraphicsFuzzumEngine.git
cd GraphicsFuzzumEngine
./build.sh
\`\`\`

## Minimal startup
\`\`\`cpp
#include \"Engine.h\"

int main() {
    Engine engine;
return engine.run();
}
\`\`\`