# Sources and acknowledgements

Copy It is an independent Cities: Skylines II code mod inspired by the selection workflow of Move It.

- Move It repository examined: https://github.com/yenyang/CS2-MoveIt (main downloaded 2026-09-14, MIT). No Move It binary dependency is included. Reference source stays in the development research folder and is not redistributed in the Copy It package.
- UI template, generated TypeScript declarations and build helpers originate from the installed Cities: Skylines II official modding toolchain. Game libraries remain external references and are not redistributed.
- UI runtime React is supplied by the game. Webpack's generated third-party license file accompanies the UI module.
- Copy It's overlapping-squares icon was authored for this mod as SVG.

Official guidance consulted directly in the browser on 2026-09-14:

https://cs2.paradoxwikis.com/Modding_Toolchain

https://cs2.paradoxwikis.com/UI_Modding

The Toolchain page carries a verification badge for 1.1.12 f1; the UI page for 1.5.7 f1. Their architectural guidance was checked against the locally installed toolchain and Game.dll, rather than copying obsolete version numbers.

0.5.0: SlopeMath is reused from the local Sloped It project by the same author; no Sloped It binary dependency. Surface creation was checked against installed GenerateAreasSystem, AreaToolSystem, Area.Node, and Area.SearchSystem. Official wiki refresh on 2026-09-14 returned HTTP 401; previously consulted guidance above and the installed game APIs were used.

Copy It 0.5.4 adapts override prevention and LOD refresh from https://github.com/yenyang/Anarchy, Systems/OverridePrevention/PreventOverrideSystem.cs and PreventCullingSystem.cs. Optional integration uses its public AnarchyBridge.TryAddAnarchyComponent API.

MIT License

Copyright (c) 2024 yenyang

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
