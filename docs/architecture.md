echo "# Architecture

The engine is divided into the following modules:

- **Core** - basic utilities, logging, time management.
- **Renderer** — Vulkan/OpenGL/DX11/DX12 backends.
- **Audio** - integration with SoLoud.
- **Window** — own window system (without GLFW/SDL).

All modules are independent and can be used separately." > docs/architecture.
md