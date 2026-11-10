# 学习 LUT：导入 JSON 与版本对比

## 使用方式

1. 在 Unity 中选中已有的 `GaussianSplatRenderer` 对象。高斯资源必须包含目标层和原始 `hu` 数据。
2. Inspector 展开 **Learned LUT / JSON versions**，将 **Import into layer** 设为 `7`。
3. 点击 **Import LUT JSON...**，选择训练导出的 JSON。例如工作区的 `output/appearance/l7/关键结果/02_最终LUT.json`。无需 `.pt`。
4. **Displayed version** 中可切换所有已导入版本；选择 **Original / manual** 恢复导入前的手动显示设置。再次导入同一文件也会保留为独立版本，编号用于区分。
5. 保存场景或 prefab，导入的控制点和当前选择会一起保存，无需依赖原 JSON 的绝对路径。Play 模式中的临时导入遵循 Unity 的规则，不会自动写回场景。

如果希望在运行时直接操作：点击 **Add / select Game-view LUT panel**，保存场景后进入 Play。Game 窗口出现 **LUT comparison** 面板，可点击 **Import JSON...** 打开内置文件浏览器。在目录栏粘贴文件夹路径，点击 **Open folder**，再点击 JSON 文件。依次导入多个版本后，点击版本按钮即时切换。原始版本始终保留。

面板也可通过 Add Component → Gaussian Splatting → LUT Comparison Panel 添加，设置 Renderer Target 和 Target Layer。此面板面向桌面 Game 窗口及桌面构建，采用鼠标操作，不是 XR 世界空间交互面板，也不提供 WebGL 或 Android 系统文件选择器。自定义平台上传接口可以调用 `LUTComparisonPanel.ImportJson(json, versionName)`；该方法使用面板的 `targetLayer`。

## 文件约定

直接兼容当前训练导出的 JSON，无需修改训练输出：

```json
{
  "hu": [[-190, -110, 50]],
  "final_linear_rgb": [[[0.5, 0.2, 0.1], [0.7, 0.4, 0.2], [0.9, 0.8, 0.5]]]
}
```

示例仅说明格式。实际可有 16、32 或其他数量的控制点（2–1024）。`initial_linear_rgb` 等诊断字段不会被当作最终 LUT。每个 JSON 只允许一个组织行；目标层由界面明确指定。如果文件包含可选 `layer` 字段，必须与界面一致。文件上限 1 MiB，控制点 HU 必须严格递增，RGB 必须有限且位于 [0,1]。不合法的文件会显示错误并保留当前显示。

## 显示行为

- 只替换当前版本指定层的 RGB，不写回 PLY 或 GaussianSplatAsset，不修改几何或透明度设置。
- 使用 JSON 自己的 HU 控制点和原始高斯 HU，超范围截断到端点。不会使用全局手工 LUT 的 HU 范围。
- 控制点以 RGBAFloat 独立纹理存储，RGB 保存线性颜色，A 保存 HU 位置。直接在原始控制点之间线性插值，不转成 8-bit 的 256 点手工 LUT。
- 导入层跳过旧 SH 明暗、Layer / HU Mix、层亮度、HU macro/detail 调制和额外高光，避免把旧外观叠加到学习结果上。其他层仍执行原来的显示逻辑。当前仅有一个导入版本处于激活状态，切换到其他层的版本时，先前层恢复手工显示。
- 当前 Unity 渲染器在显示编码的 splat 缓冲中混合，再在合成阶段调用 `GammaToLinearSpace`。新 LUT 在线性空间插值后，进入该缓冲前只编码一次；没有对 JSON 再做 sRGB→线性解码。此集成沿用旧合成管线，不能承诺与 PyTorch 的线性混合逐像素一致，边界、透明叠加及项目曝光/色调映射也会影响画面。

## 验证

`package/Tests/Editor/LearnedLUTVersionTests.cs` 覆盖插值、端点、16/32 点、数据合法性和错误文件等情况。使用 Unity Test Runner 时，将 `org.nesnausk.gaussian-splatting` 加入项目 Packages/manifest.json 的 `testables` 列表，然后运行 EditMode 测试。

本机没有 Unity Editor，尚未执行 Unity 编译、GPU 渲染或 Game 面板交互验证。已使用 .NET、Unity 官方 3.2.2 包中的 Newtonsoft DLL 和最小 Color 测试替身运行上述 16 项用例，并读取本轮实际训练 JSON，验证连续导入两个版本及错误文件保留当前选择。新增界面源文件也经过最小 API 替身编译。实际 HU 映射 HLSL（含原有手工分支及新导入分支）通过 Windows D3DCompiler 的 cs_5_0 独立编译；旧编译器对带纹理分支的提前返回发出潜在未初始化警告。这些检查不代替 Unity 的完整 shader/C# 编译与画面验收。测试项目、下载的依赖和编译输出均位于系统临时目录，未放入训练结果目录。

Unity 内验收：导入两个不同 JSON，观察 L7 随版本切换、其他层及透明度设置不变；切回 Original / manual；输入错误文件确认显示不变；保存重开场景检查版本列表；分别切换 FidelityFX 和 DeviceRadixSort 检查显示。对同一 JSON 的不同版本比较应固定相机、曝光、背景及显示设置。

JSON 依赖使用 Unity 提供的 [Newtonsoft Json package](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)。
