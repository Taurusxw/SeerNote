# 2026-08-24 Round 013: 共享右键菜单主题修复

## Status

completed

## Goal

让 Note 列表在 1–6 条直接菜单与 7 条起共享菜单两条路径中使用同一套应用主题，避免共享菜单退回系统浅灰外观。

## Background

共享命令树由 `EntryContextMenu : ContextMenu` 承载。WPF 隐式样式按精确控件类型查找，不会自动把 `ContextMenu` 的应用级隐式样式应用到派生类型，因此列表跨过 7 条阈值后出现主题断层。

## Scope

- 只调整共享 Note 菜单的样式解析边界。
- 不改变菜单命令、目标冻结、共享阈值、数据或发布结构。

## Implementation Steps

- `EntryContextMenu` 构造时显式引用应用资源中的 `ContextMenu` 样式。
- 展示测试增加共享菜单必须应用基类主题的回归断言。

## Key Decisions

- 继续复用中央 `MenuThemeResources`，不为派生菜单建立第二套视觉资源。
- 保留直接菜单与共享菜单现有的性能和目标生命周期策略。

## Change List

- `src/SeerNote/Presentation/EntryContextMenu.cs`
- `tests/SeerNote.Tests/PresentationTests.cs`
- `docs/CHANGELOG.md`

## Tests And Verification

- 新断言在修复前稳定失败，指出共享菜单未复用基类 `ContextMenu` 样式。
- 修复后 `build.ps1 -Task Test` 六组测试全部通过。
- 在 `artifacts/tests/` 隔离数据下启动 7 条 Note 的真实 WPF 窗口；右键根菜单、键盘高亮、分隔线、分类子菜单和当前分类勾选均实际渲染为石墨主题。
- 隔离实例已正常退出；未读取或改写项目真实 `data/`，未覆盖根目录发布程序。

## Documentation Updates

- `docs/CHANGELOG.md` 记录未发布修复。
- `docs/PRODUCT.md` 已有“右键菜单与子菜单使用一致语义配色”的现行合同，无需改写。
- 已消费的 `docs/HANDOFF.md` 保持冻结。

## Risks And Follow-Up

- 本轮未重跑 1.8.0 发布或人工高对比模式；修复只改变派生控件的样式解析，继续复用既有主题和系统高对比资源边界。

## Next Step

等待下一项用户目标；如需生成新的根目录便携程序，应作为独立构建或发布请求处理。
