# AnyG-URPExtensions
Develop for Unity 2022 URP Extension Package.

# Feature
## Cached Mainlight Shadowmap
Reduce the update frequency of cascade shadow map, cut down most drawcall and triangles rendering by shadowmap.

## Shadalyze
Convenient android-glsl compiled code and analysis report inspector for develop. There are too many performance-affecting hidden rules, such as half problem. You can use this tool to edit your shader to reach highest performance.

## Batch Renderer Group Render System
Light-weight ECS Render System in Unity2022 (developed referring to gpu resident drawer in Unity6), You can use it by adding a component to every renderer in your scene, it can save much cpu time from the submit of render node in main thread and the submit of drawcall in render thread. And with Hiz Occlusion Culling, you can also have some gpu benefit.

## More Choice of Super resolution
MetalFX, SGSR and DLSS will be merged in this repo in near future.
