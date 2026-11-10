# 🛠️ Daedalus - C# Solution AI Inspector & Refactor Tool

**Daedalus** is an offline C# solution inspector and refactoring tool powered by local LLMs to audit and optimize code, XAML, and configurations while preserving complete data privacy.

---

## 📋 Prerequisites

To run and use **Daedalus** locally, ensure the following requirements are met:

### 1. .NET Runtime & SDK
* **.NET 8.0 SDK** (or higher, e.g., .NET 9.0)

### 2. Local AI Engine (Ollama)
* **Ollama** installed and running on Windows, Linux, or macOS.
  * Download: [https://ollama.com](https://ollama.com)
  * Default endpoint active at: `http://localhost:11434`

### 3. Required LLM Models
Pull the primary coding model via your terminal before launching the application:

* **Default Model (Recommended):**
  ```powershell
  ollama pull qwen2.5-coder:7b

```

* **Advanced Model (Optional for high-spec hardware):**
```powershell
ollama pull deepseek-coder-v2:16b

```



### 4. Recommended Hardware Resources

* **RAM:** Minimum 16 GB (to comfortably run the `7b` model).
* **Disk Space:** ~10 GB free space for model weights storage.

---

## 1. High-Level Architecture

### 1.1 Project Goals

**Daedalus** is a .NET 8/9 application designed to automate code reviews, targeted refactoring, and architectural weakness analysis across an entire C# Solution.

Unlike traditional static linters, Daedalus leverages a **local LLM (via Ollama and `qwen2.5-coder:7b`)** to understand the semantic context of C# source code, XAML layouts, project files, and app configurations—ensuring total data privacy with zero cloud dependencies.

### 1.2 Core Principles

* **Zero Trust & Human-in-the-Loop:** Daedalus never overwrites files automatically. It enforces a dry-run phase with a visual diff preview and architectural review notes.
* **Contextual Prompting:** The engine dynamically adapts analysis rules based on target file extensions (`.cs`, `.xaml`, `.csproj`, `appsettings.json`).
* **Clean Architecture:** Strict separation of concerns between File I/O, task orchestration, LLM integration, and User Interface.

---

## 2. Technical Details

### 2.1 Component Overview

The solution is organized into the following modules:

1. **`SolutionScanner` (I/O & File Discovery):**
* Recursively scans the solution root folder.
* Filters target files by extension (`.cs`, `.xaml`, `.csproj`, `appsettings.json`).
* Excludes build/system directories (`bin`, `obj`, `.vs`, `.git`, `node_modules`) and auto-generated files (`*.Designer.cs`, `*.g.cs`).


2. **`AiEngine` (Local LLM Integration):**
* Connects to Ollama's local HTTP endpoint (`http://localhost:11434/v1`).
* Uses **`qwen2.5-coder:7b`** for modern C# / .NET code transformations.
* Injects the unified **Master System Prompt** contextualized per file type.


3. **`DiffViewer & FileManager` (Safety & Preview):**
* Computes line-by-line diffs between original and AI-modified source files.
* Persists changes only upon explicit user confirmation.



---

### 2.2 Master System Prompt

The intelligence layer of **Daedalus** uses this structured Master System Prompt:

```text
Sei un Principal .NET Solution Architect ed esperto di Code Review e Refactoring. 
Il tuo compito è analizzare, ottimizzare e correggere il file sorgente che ti viene fornito, attenendoti rigorosamente alle best practice di .NET 8/9 e C# 12+.

Prima di procedere, identifica l'estensione del file e applica tassativamente le regole corrispondenti:

### 1. REGOLE PER FILE C# (*.cs)
- Sfrutta le ultime feature del linguaggio: Primary Constructors, Collection Expressions, Pattern Matching avanzato, e switch expressions.
- Assicurati che i Nullable Reference Types siano gestiti correttamente (usa guard clauses come `ArgumentNullException.ThrowIfNull()` anziché controlli manuali prolissi).
- Ottimizza le performance di LINQ evitando enumerazioni multiple superflue.
- Elimina codice morto, variabili non utilizzate e logica duplicata.

### 2. REGOLE PER FILE XAML E CODE-BEHIND (*.xaml / *.xaml.cs)
- Rispetta in modo ferreo il pattern MVVM (Model-View-ViewModel). 
- SEGNALA se c'è logica di business, chiamate a database o logica applicativa complessa scritta nel code-behind (`*.xaml.cs`). Tutta la logica deve risiedere nel ViewModel tramite comandi (`ICommand`) e binding.
- Verifica la pulizia dei binding e l'efficacia dei layout (preferire Grid/StackPanel strutturati correttamente a layout caotici).

### 3. REGOLE PER FILE DI PROGETTO (*.csproj)
- Verifica che il Target Framework sia moderno (.NET 8 o .NET 9).
- Assicurati che il tag <Nullable>enable</Nullable> sia attivo.
- Rileva la presenza di pacchetti NuGet obsoleti o deprecati e suggerisci alternative moderne.

### 4. REGOLE PER FILE DI CONFIGURAZIONE (appsettings.json)
- Controlla la presenza di punti deboli di sicurezza critici: stringhe di connessione in chiaro, password, secret o API key hardcoded.
- Suggerisci l'adozione di User Secrets o configurazioni basate su variabili d'ambiente.

### FORMATO DELL'OUTPUT:
1. Restituisci il file completamente corretto e rifattorizzato all'interno di un unico blocco di codice markdown (es. ```csharp ... ``` o ```xml ... ```).
2. Subito dopo il blocco di codice, inserisci una sezione intitolata "### 📝 Architectural Review Notes" in cui elenchi brevemente (massimo 3 punti puntati) i principali punti deboli che hai corretto o riscontrato nel file.
3. Se il file è già perfetto e non richiede modifiche, restituisci il codice originale inalterato e scrivi "Nessuna modifica necessaria".

```

---

## 📄 License

Distributed under the **Apache License 2.0**. See `LICENSE` for more information.

