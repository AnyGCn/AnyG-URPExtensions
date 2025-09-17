### Resident Drawer

This library is implemented with reference to Unity 6's GPU Resident Drawer, developed as a low-end CPU version based on Unity 2022.

Currently, most open-source BRG implementations are not suitable for scene object rendering—they lack lighting logic and culling logic.

Rather than aiming to create a universal rendering pipeline to replace the existing SRP Batch rendering process, this library focuses on rendering a large number of static (or rarely modified) scene objects with lower CPU overhead. Therefore, in addition to the drawing pipeline logic, the library also emphasizes modules such as static rendering data structure assets and scene batch conversion, with the goal of establishing a practical, production-ready rendering workflow.