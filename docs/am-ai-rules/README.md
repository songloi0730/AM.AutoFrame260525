# AutoMachine — AI Rules & Coding System
## Hướng dẫn cài đặt và sử dụng

Bộ file này giúp AI luôn viết code đúng chuẩn, nhất quán, phù hợp với
kiến trúc AutoMachine Framework — dù dùng Cursor, Cline, Copilot, hay Claude.

---

## 📁 Cấu trúc files

```
am-ai-rules/
│
├── .cursorrules                  ← Rules cho Cursor IDE / Cline / Roo
├── .editorconfig                 ← Code style enforcement trong Visual Studio
├── Directory.Build.props         ← MSBuild quality gates toàn solution
├── QUICK_REFERENCE.md            ← In ra, dán cạnh màn hình
│
├── copilot-instructions.md       ← GitHub Copilot custom instructions
│
├── agents/
│   └── AGENTS.md                 ← 7 specialized AI agents
│
├── prompts/
│   └── PROMPT_TEMPLATES.md       ← 11 prompt templates tái sử dụng
│
└── snippets/
    └── AutoMachine.snippet       ← Visual Studio code snippets (11 snippets)
```

---

## 🚀 Cài đặt — Làm một lần cho mỗi máy

### Bước 1: Copy vào Solution Root
```
SolutionRoot/
├── AutoMachine.slnx
├── .cursorrules          ← Copy từ am-ai-rules/
├── .editorconfig         ← Copy từ am-ai-rules/
├── Directory.Build.props ← Copy từ am-ai-rules/
├── .github/
│   └── copilot-instructions.md  ← Copy từ am-ai-rules/copilot-instructions.md
└── src/ tests/ docs/ ...
```

### Bước 2: Cài VS Code Snippets
```
1. Mở Visual Studio 2022
2. Menu: Tools → Code Snippets Manager (Ctrl+K, Ctrl+B)
3. Language: Visual C#
4. Click "Import..."
5. Chọn file: snippets/AutoMachine.snippet
6. OK → Done

Kiểm tra: gõ "am-" trong file .cs → IntelliSense hiện snippet list
```

### Bước 3: Cài Cursor Rules (nếu dùng Cursor IDE)
```
File .cursorrules đặt ở solution root → Cursor tự đọc
Không cần cấu hình thêm.
```

### Bước 4: Cài Copilot Instructions (nếu dùng GitHub Copilot)
```
File .github/copilot-instructions.md → Copilot tự đọc khi trong repo
Yêu cầu: GitHub Copilot Extension trong VS2022
```

### Bước 5: Cài Cline/Roo Rules (nếu dùng VS Code + Cline)
```
.cursorrules cũng được Cline và Roo đọc tự động
Đặt ở workspace root là đủ.
```

---

## 📖 Cách sử dụng hàng ngày

### 1. Tạo file mới → Dùng Snippet
```
Trong file .cs mới → gõ "am-fileheader" + Tab Tab → header tự điền
Tạo service mới   → gõ "am-service"    + Tab Tab → template đầy đủ
Tạo step mới      → gõ "am-step"       + Tab Tab → step template
```

### 2. Hỏi AI với context đúng → Dùng Agent
```
Mở AGENTS.md → copy đúng agent block → paste vào đầu chat với AI
Sau đó mô tả yêu cầu cụ thể → AI follow đúng rules của project
```

### 3. Hỏi AI tạo code cụ thể → Dùng Prompt Template
```
Mở PROMPT_TEMPLATES.md → copy template phù hợp
Điền [PLACEHOLDER] → paste vào AI → code chuẩn ngay lần đầu
```

### 4. Commit → Dùng Quick Reference Checklist
```
Mở QUICK_REFERENCE.md (hoặc in ra)
Tick từng ô trước khi git commit
```

---

## 🤖 AI Tools được hỗ trợ

| Tool | File đọc | Setup |
|------|---------|-------|
| **Cursor IDE** | `.cursorrules` | Tự động (đặt ở root) |
| **Cline (VS Code)** | `.cursorrules` | Tự động |
| **Roo Code** | `.cursorrules` | Tự động |
| **GitHub Copilot** | `.github/copilot-instructions.md` | Tự động |
| **Claude (chat)** | Copy `AGENTS.md` block vào chat | Thủ công |
| **ChatGPT** | Copy `.cursorrules` vào System Prompt | Thủ công |

---

## 🔄 Cập nhật rules khi có thay đổi

Khi project evolve (thêm hardware mới, pattern mới):
```
1. Cập nhật .cursorrules (thêm rule mới vào section phù hợp)
2. Cập nhật AGENTS.md (nếu agent cần biết thêm context)
3. Thêm snippet mới vào AutoMachine.snippet (nếu pattern hay)
4. Thêm prompt template vào PROMPT_TEMPLATES.md
5. Commit với message: "docs: update AI rules for {feature}"
```

---

## 📊 Coverage mục tiêu (nhắc nhở)

| Project | Coverage tối thiểu |
|---------|-------------------|
| AM.Services (AlarmService, ParameterService) | ≥ 90% |
| AM.Services (các service khác) | ≥ 80% |
| AM.WorkStation.* (Steps) | ≥ 80% |
| AM.Modules.* (ViewModels) | ≥ 70% |
| AM.Hardware.* (Simulators) | ≥ 50% |

Chạy coverage: `dotnet test --collect:"XPlat Code Coverage"`
