# AnyG-URPExtensions

Unity 2022.3 URP extension repository based on the official `com.unity.render-pipelines.universal` package.

The goal of this repo is to keep URP as the base renderer while adding production-focused rendering extensions for performance experiments and mobile/desktop feature work.

## What is included

- Cached Main Light Shadow Map
  - Reduces the update frequency of cascade shadow maps to save draw calls and triangle submissions.
- HZB Occlusion Culling
  - Hierarchical Z buffer based occlusion workflow for scene culling.
- Batch Renderer Group Render System
  - A lightweight ECS-oriented rendering pipeline for large numbers of static or rarely changed renderers.
- Shadalyze
  - Android GLSL shader performance analysis tooling powered by Mali Offline Compiler.
- Super Resolution extensions
  - Snapdragon Game Super Resolution 1: a color-input-only upscaler.
  - Snapdragon Game Super Resolution 2: a TAAU-based improvement path built on top of SGSR work.

## Environment

- Unity `2022.3.62f1`
- URP `14.0.12`
- Package-based project layout under `Packages/com.unity.render-pipelines.universal`

## Repository Layout

- `Packages/com.unity.render-pipelines.universal`
  - Forked URP package with the rendering changes and extension code.
- `Packages/BrgRenderSystem`
  - BRG-based renderer and related documentation.
- `Packages/Shadalyze`
  - Shader analysis tooling and references.

## Getting Started

1. Open the project in Unity `2022.3.62f1`.
2. Let Unity import the embedded packages under `Packages/`.
3. Review the module-specific documentation before enabling a feature:
   - [BRG Render System](Packages/BrgRenderSystem/README.md)
   - [Shadalyze](Packages/Shadalyze/README.md)

## Notes

- The URP package in this repository is modified from the official package, so merge/upstream work should be done carefully.
- Several extensions are feature-specific and may require additional scene setup, renderer features, or platform support.
- If you only want one module, start from the package-local README instead of the root overview.
- Shared repository memory lives in [AGENTS.md](AGENTS.md).
