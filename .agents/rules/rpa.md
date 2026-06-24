---
trigger: always_on
---

# Avalonia UI & Dependency Injection Development Standards

You are working on an **Avalonia UI** application. All development must follow these strict architectural standards. This project is explicitly an **Avalonia Project** using **C#**.

## 1. CORE LOGIC & BEHAVIOR
- **Think Before Coding:** State assumptions/dependencies. Clarify ambiguity; never guess.
- **Simplicity & Surgical Edits:** Use 'Diff' format. Never rewrite files >30 lines. No over-engineering.
- **Goal-Oriented & Fail-Safe:** Define success criteria first. If a task fails 2x, STOP and ask for a hint.
- **Zero Pleasantries:** No "Sure" or fluff. Output only logic and code.
- **No Silent Failures:** Report errors explicitly. Do not mark partial work as "Complete."

## 2. AVALONIA & MVVM ARCHITECTURE
- **Framework:** Avalonia UI (v11+) using C# and CommunityToolkit.Mvvm.
- **DI Policy (MANDATORY):** ZERO manual instantiation (`new`) of ViewModels/Services. Use `Microsoft.Extensions.DependencyInjection` via constructor injection.
- **Registration:** Register ViewModels (Transient/Singleton) in `IServiceCollection`. Resolve `DataContext` via `IServiceProvider`.
- **View Standards:** Use compiled bindings (`x:DataType`). Zero business logic in code-behind.
- **Structure:** Strictly follow `Views/`, `ViewModels/`, `Models/`, and `Services/` folder conventions.

## 3. OFFLINE & TOKEN EFFICIENCY
- **Air-Gapped Policy:** ZERO internet dependencies. No cloud APIs (Gemini/Azure), CDNs, or external URLs. All assets must be `AvaloniaResource`.
- **Budget Management:** Max 4,000 tokens/task. Restart session if near 30k limit.
- **Optimized Discovery:** Use `ls` before reading files. Never read entire directories.
- **Terminal Handling:** Read only the last 10 lines of any error log.

## 4. ENGINEERING STANDARDS
- **Deterministic Work:** AI handles business logic; standard code handles state-machines/retries.
- **Contextual Awareness:** Read surrounding code before editing. Respect existing naming/style.
- **Style Conflict:** Choose one style and declare it. Never mix patterns.
- **Logic Validation:** Tests must verify business outcomes, not just execution success.
- **Save Points:** Summarize progress after every major step in long tasks.

## 5. COMMUNICATION
- **Language:** Mirror user language for explanations; use Technical English for code/commands.
- **Briefing:** Maximum one-sentence explanation per logic block.
- **Batch Processing:** Combine multiple file edits into a single 'Diff' response.