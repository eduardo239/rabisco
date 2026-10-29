# Rabisco ✏️

Notas adesivas nativas para Windows 11, feitas com **WinUI 3 + Windows App SDK (C# / .NET 10)** e **SQLite**.

## Funcionalidades (v0.1)

- Cada nota é uma janela leve e independente, com 6 cores (amarelo, verde, rosa, roxo, azul, cinza).
- **Fixar no topo** (ícone de alfinete): a nota fica sempre visível sobre as outras janelas.
- Salvamento automático no SQLite (texto, cor, posição, tamanho e fixação).
- As notas abertas reabrem no mesmo lugar ao iniciar o app (inclusive depois de desligar o PC).
- Lista de notas com **Mica**, busca que ignora acentos e maiúsculas, e menu de contexto (abrir/excluir).
- Tema claro/escuro acompanhando o Windows.
- Atalhos globais: **Ctrl+Alt+N** (nova nota de qualquer lugar) e **Ctrl+Alt+R** (mostrar a lista).
- **Ctrl+N** cria uma nota dentro do app.
- Instância única: abrir o Rabisco de novo só traz a lista para frente.

## Requisitos

- Windows 10 19041+ (recomendado Windows 11, para Mica e cantos arredondados)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Opcional: Visual Studio 2022/2026 com a carga de trabalho **"Desenvolvimento de aplicativos WinUI"**

> O WinUI 3 só compila no Windows. Se o código estiver no WSL, copie ou clone o projeto
> para uma pasta do Windows (ex.: `C:\dev\rabisco`) antes de compilar.

## Rodando

```powershell
dotnet run --project src\Rabisco
```

Ou abra `Rabisco.sln` no Visual Studio, escolha a plataforma `x64` (ou `ARM64`) e pressione F5.

Por padrão o `dotnet run` compila para `win-x64`. Em PCs ARM use `dotnet run --project src\Rabisco -r win-arm64`.

Publicar um executável:

```powershell
dotnet publish src\Rabisco -c Release -r win-x64
```

## Onde ficam os dados

`%LOCALAPPDATA%\Rabisco\rabisco.db` (SQLite, modo WAL). A versão do esquema fica em `PRAGMA user_version`.

## Estrutura

```
src/Rabisco/
├── Program.cs              # Main com instância única (AppInstance)
├── App.xaml(.cs)           # Inicialização, ciclo de vida, estilos globais
├── MainWindow.xaml(.cs)    # Lista de notas (Mica, busca, atalhos globais)
├── NoteWindow.xaml(.cs)    # Janela de uma nota adesiva
├── Models/                 # Note, NoteColor
├── Data/NoteRepository.cs  # Acesso ao SQLite (Microsoft.Data.Sqlite)
├── Services/NoteManager.cs # Coleção de notas + janelas abertas + persistência
├── Theming/                # Paleta das notas e cores da barra de título
└── Interop/                # P/Invoke Win32 (RegisterHotKey, subclass de janela)
```

## Comportamento das janelas

- Fechar uma nota (X) só a esconde; ela continua na lista. Notas vazias são descartadas ao fechar.
- Fechar a lista com notas abertas só a esconde. O app encerra quando não sobra nenhuma janela visível.
- **Sair do Rabisco** (menu ⋯ da lista) encerra tudo e reabre as mesmas notas na próxima vez.

## Próximos passos sugeridos

- Ícone na bandeja do sistema (ex.: pacote `H.NotifyIcon.WinUI`) para manter o app em segundo plano
- Formatação de texto (negrito, listas) com `RichEditBox`
- Configuração dos atalhos globais e opção de iniciar com o Windows
- Ícone do app e empacotamento MSIX para distribuição
- Sincronização opcional na nuvem
