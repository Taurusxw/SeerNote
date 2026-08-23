# SeerNote 命令行接口

`SeerNote.Cli.exe` 是面向本地智能体和自动化脚本的稳定进程接口。它必须与 `SeerNote.exe` 位于同一目录，并共享该目录的 `data/notes.json`、备份、恢复顺序和排它锁。

## 快速开始

```powershell
./SeerNote.Cli.exe schema
./SeerNote.Cli.exe categories
./SeerNote.Cli.exe list --query "项目" --view all --limit 20
./SeerNote.Cli.exe get --id 01234567-89ab-cdef-0123-456789abcdef

"多行正文" | ./SeerNote.Cli.exe create --title "新 Note" --body-stdin
./SeerNote.Cli.exe update --id 01234567-89ab-cdef-0123-456789abcdef --favorite true
./SeerNote.Cli.exe delete --id 01234567-89ab-cdef-0123-456789abcdef
./SeerNote.Cli.exe restore --id 01234567-89ab-cdef-0123-456789abcdef
```

选项同时支持 `--name value` 和 `--name=value`。不带参数或执行 `help` 会显示人类可读帮助；智能体应先调用 `schema` 获取机器契约。

## 输出合同

除人类帮助外，成功命令只向 stdout 写一个 UTF-8 JSON 对象并返回 0：

```json
{
  "contract": "seernote.cli.v1",
  "ok": true,
  "command": "get",
  "data": {
    "note": {
      "schema": "seernote.note.v1",
      "id": "01234567-89ab-cdef-0123-456789abcdef",
      "title": "标题",
      "displayTitle": "标题",
      "body": "正文",
      "category": "工作",
      "favorite": false,
      "deleted": false,
      "createdUtc": "2026-08-22T00:00:00.0000000Z",
      "updatedUtc": "2026-08-22T00:00:00.0000000Z"
    }
  }
}
```

失败命令不向 stdout 写内容，只向 stderr 写一个 JSON 对象：

```json
{
  "contract": "seernote.cli.v1",
  "ok": false,
  "command": "get",
  "error": {
    "code": "note_not_found",
    "message": "No note exists with id ..."
  }
}
```

退出码：

| 值 | 含义 |
|---:|---|
| 0 | 成功 |
| 1 | 未预期的内部错误 |
| 2 | 命令或选项用法错误 |
| 3 | Note 不存在 |
| 4 | 桌面应用或另一命令正在占用工作区 |
| 5 | UUID、分类、视图、字段或状态校验失败 |
| 6 | 加载、保存或恢复失败 |

## 命令

| 命令 | 选项 | 行为 |
|---|---|---|
| `schema` | 无 | 返回契约、命令、变更命令、退出码和永久删除能力 |
| `categories` | 无 | 返回有序自定义分类 |
| `list` | `--query`、`--category`、`--view all\|favorites\|trash`、`--limit 1..1000` | 搜索并返回 Note 数组；默认 `all`、上限 100 |
| `get` | `--id UUID` | 返回活动或回收站中的单条 Note |
| `create` | `--title`、正文源、`--category`、`--favorite [true\|false]` | 创建 Note；标题或正文至少一个非空 |
| `update` | `--id` 加至少一个可修改字段 | 更新活动 Note；回收站 Note 必须先还原 |
| `delete` | `--id UUID` | 移入回收站并关闭其置顶小窗状态 |
| `restore` | `--id UUID` | 从回收站还原 |

正文源必须且最多选择一个：

- `--body TEXT`：短文本直接作为参数。
- `--body-file PATH`：按 UTF-8 读取文件；非法 UTF-8 返回校验错误。
- `--body-stdin`：读取标准输入直到 EOF，适合多行正文和智能体管道。

`--category` 只能使用 `categories` 返回的现有名称；空值表示未分类。CLI 不会隐式创建或重排分类。

## 并发与删除边界

所有数据命令，包括 `categories`、`list` 和 `get`，都会先取得 `data/.seernote.lock`。这是因为加载损坏主文件时可能从备份恢复并写回。如果桌面应用正在运行，应先让它完成保存并安全退出；CLI 会返回退出码 4，而不是绕过锁。

CLI 故意不提供永久删除。`delete` 始终可由 `restore` 撤销；永久删除与清空回收站继续保留在桌面端，并显示精确对象与确认提示。
