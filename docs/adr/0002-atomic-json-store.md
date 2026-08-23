# ADR-0002：采用原子 JSON 权威存储

## Status

Accepted — 2026-08-18

## Context

数据预计为几千条以内纯文本，必须随目录复制、容易备份、无外部服务，并尽可能避免发布 DLL。普通 JSON 全量覆盖在断电时不安全；SQLite 事务成熟但会增加依赖和发布复杂度。

## Decision

使用一个版本化 `data/notes.json` 保存完整状态。写入采用同卷临时文件、`Flush(true)`、回读验证和 `ReplaceFile`；同时维护 `.bak`、每日备份与恢复目录。单实例保证只有一个写入者。

## Alternatives

- SQLite DELETE journal：数据完整性最强，但 .NET Framework 需要额外托管与原生组件。
- JSONL：追加简单，但更新、删除、压缩和 schema 一致性需要自建日志协议。
- 每条 Markdown：人类可读，但会产生文件命名、批量状态和大量小文件问题。

## Consequences

- 发布无数据库 DLL，完整目录可直接迁移。
- 每次保存是 O(n) 序列化；在 5,000 条纯文本边界内必须实测。
- 原子协议和恢复顺序成为核心数据完整性代码，必须有故障路径测试。
- 若未来数据规模或查询需求超出预算，应以新 ADR 迁移到 SQLite，而不是在 JSON 上叠加日志系统。
