# AGENTS.md

This file captures durable, shareable repository memory for future contributors and agents.

## Stable Facts

- Repository name: `AnyG-URPExtensions`
- Engine: Unity `2022.3.62f1`
- Render pipeline: URP `14.0.12`
- Layout: package-based Unity project with embedded packages under `Packages/`

## Main Modules

- `Packages/com.unity.render-pipelines.universal`
  - Forked URP package that contains rendering changes and extension code.
- `Packages/BrgRenderSystem`
  - BRG-based renderer and related documentation.
- `Packages/Shadalyze`
  - Shader analysis tooling and references.

## Project Intent

- Keep URP as the base renderer.
- Add production-focused rendering extensions for performance experiments and mobile/desktop feature work.
- Treat upstream URP merges carefully because the package in this repo is modified.

## Useful Entry Points

- Root overview: `README.md`
- BRG documentation: `Packages/BrgRenderSystem/README.md`
- Shadalyze documentation: `Packages/Shadalyze/README.md`

## Practical Notes

- Open the project with Unity `2022.3.62f1`.
- Let Unity import embedded packages under `Packages/`.
- Review module-local documentation before enabling a feature or changing rendering behavior.
- After changing `.cs` files, use the Unity MCP diagnostic tools to check editor state and C# compilation errors before reporting success.
- After changing `.shader`, `.hlsl`, `.compute`, or Shader Graph assets, inspect Unity Console logs via MCP because shader compiler errors are reported there.
- Keep Unity MCP use diagnostic-first; do not use scene, asset, or prefab mutation tools unless the user explicitly asks for that workflow.
- Prefer updating this file only when a fact is stable enough to be reused across sessions.
