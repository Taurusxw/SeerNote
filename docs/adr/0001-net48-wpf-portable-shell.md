# ADR-0001：采用 .NET Framework 4.8 WPF 便携壳

## Status

Accepted — 2026-08-18

## Context

SeerNote 只支持 Windows 11，需要中文 IME、键盘、DPI、无障碍、精致界面和尽可能小的目录发布。本机有 Visual Studio Build Tools 和 .NET Framework 构建链，但没有 .NET SDK。

## Decision

使用 C#、WPF、`.NET Framework 4.8`、AnyCPU，无第三方 UI 库与 NuGet 包。发布为根目录 `SeerNote.exe`，依赖 Windows 11 自带的兼容运行时。

## Alternatives

- .NET 8 WPF self-contained：开发体验好，但捆绑运行时导致体积过大。
- Tauri：视觉开发快，但依赖 WebView2、内存更高，便携语义较弱。
- Rust/Win32：启动和体积最好，但中文输入、UIA、DPI 和 UI 交付风险最高。
- .NET Framework WinForms：小且成熟，但三栏编辑器主题、绑定和高 DPI 完成度不如 WPF。

## Consequences

- Windows 11 无需安装额外运行时，发布文件非常少。
- 使用标准 WPF 文本控件降低中文与辅助技术风险。
- 项目不跨平台，也不使用现代 .NET API；所有依赖必须来自框架或项目源码。
- 必须实际验证 WPF 冷启动、DPI、高对比和内存，而不能只凭框架推断。
