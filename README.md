# HWID Diagnostic Pro

**Software Profissional de Diagnóstico Avançado de Hardware, Rede, Segurança do Windows, Anticheat e Fortnite para Windows 10 e Windows 11 (x64)**

![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-3B82F6?style=flat-square)
![WPF MVVM](https://img.shields.io/badge/UI-WPF%20MVVM-10B981?style=flat-square)
![Plataforma](https://img.shields.io/badge/Windows-10%20%2F%2011%20(x64)-2563EB?style=flat-square)
![Modo](https://img.shields.io/badge/Operação-100%25%20Somente%20Leitura-059669?style=flat-square)

---

## 1. Sobre o Projeto

O **HWID Diagnostic Pro** é uma ferramenta desktop desenvolvida em **C# (.NET 10 LTS)** com interface gráfica **WPF** e arquitetura **MVVM**, projetada para realizar diagnósticos técnicos completos em computadores com **Windows 10 e Windows 11 (x64)** — especialmente para usuários que enfrentam desconexões, incompatibilidades, expulsão de partidas e erros relacionados aos sistemas anticheat (**Easy Anti-Cheat**, **Easy Anti-Cheat EOS** e **BattlEye**) em jogos como **Fortnite**.

### Compromisso Ético e Segurança
- **Exclusivamente Diagnóstico (Somente Leitura)**: Não modifica identificadores de hardware (HWID, Serial, MAC, UUID), não altera chaves protegidas do Registro, não instala drivers próprios e não desativa mecanismos de segurança do Windows.
- **Não-Intrusivo**: Não injeta código, não lê memória protegida de processos em execução e não interfere na comunicação dos sistemas anticheat ou do jogo.
- **Privacidade por Padrão**: Opera localmente, sem telemetria automática, sem solicitar credenciais ou tokens da Epic Games, e **mascara automaticamente** números de série, UUID, endereços IP, endereços MAC, nomes de usuário em caminhos de arquivos, e-mails e tokens em todos os relatórios exportados.

---

## 2. Módulos do Sistema (Menu Lateral)

A interface em português brasileiro (tema escuro `#0B0F19`, `#111827`, cards `#1A2233` e destaque `#3B82F6`, resolução mínima `1280 × 720`) conta com **16 módulos dedicados**:

1. **Dashboard**: Painel com os 15 indicadores principais do computador, 5 botões de ação rápida e o **Indicador de Integridade Técnica Local (0 a 100)** (com aviso explícito de que a pontuação reflete exclusivamente verificações locais e **não** representa probabilidade de banimento).
2. **Diagnóstico completo**: Visão consolidada de todas as verificações técnicas classificadas em `NORMAL`, `ATENÇÃO`, `ERRO`, `CRÍTICO`, `INDISPONÍVEL` e `INCONCLUSIVO`.
3. **Hardware**: Informações reais via WMI/CIM de Processador (fabricante, modelo, arquitetura, frequência, núcleos, threads, temperatura ACPI) e Memória RAM (fabricante, capacidade, frequência configurada, quantidade de módulos, tipo DDR e slots utilizados).
4. **BIOS e placa-mãe**: Fabricante, modelo, versão, número de série parcialmente mascarado, disponibilidade SMBIOS, versão/data da BIOS, modo UEFI ou Legacy, Secure Boot e UUID parcialmente mascarado.
5. **SSD e HDD**: Fabricante, modelo, capacidade, interface (`NVMe`, `SATA` ou `USB`), número de série parcialmente mascarado, saúde do dispositivo, status SMART, espaço utilizado e espaço disponível.
6. **Placa de vídeo**: Fabricante, modelo, versão e data do driver WHQL, memória dedicada (VRAM), compatibilidade com DirectX e status PnP do dispositivo.
7. **Rede e internet**: Adaptadores ativos, IP local e MAC parcialmente mascarados, gateway padrão, DNS, DHCP, velocidade nominal, proxy, detecção de adaptadores de VPN (sem classificar VPN como trapaça), Firewall do Windows, latência e perda de pacotes (ICMP Ping real), resolução DNS, conectividade com serviços públicos da Epic Games e sincronia de relógio (`W32Time`).
8. **Segurança do Windows**: TPM 2.0, Secure Boot, Microsoft Defender, Firewall, Integridade da Memória (HVCI), Isolamento de Núcleo (VBS), Windows Update, Assinatura Digital de Drivers (`TestSigning`), UAC, Configurações de Inicialização, Proteções disponíveis (DEP/SmartScreen), Compilação do Windows e Necessidade de Reinicialização.
9. **Drivers e serviços**: Inventário de drivers instalados (`Win32_PnPSignedDriver`), assinaturas digitais, serviços do Windows e de terceiros, dispositivos com códigos de erro no Gerenciador de Dispositivos e falhas registradas no Visualizador de Eventos.
10. **Anticheat**: Verificação dedicada para **Easy Anti-Cheat**, **Easy Anti-Cheat EOS**, **BattlEye** e **Epic Games Launcher** (existência da instalação, serviços registrados, versão local, assinatura digital Authenticode, logs públicos e recomendações de reparo oficial).
11. **Fortnite** *(inclui Interpretador de Erros e Análise de Restrições de Acesso)*:
    - Localização automática via `LauncherInstalled.dat` e manifestos da Epic Games, verificação dos executáveis principais e pacotes `.pak`, requisitos mínimos, DirectX, espaço livre, dependências do Visual C++ 2015–2022, logs do usuário (`FortniteGame.log`) e 6 atalhos oficiais.
    - **Interpretador de Mensagens de Erro**: Campo para inserir manualmente mensagens recebidas na partida e obter causas documentadas e recomendações.
    - **Análise de Restrições de Acesso**: Questionário técnico que correlaciona sintomas informados com o diagnóstico local nos 6 resultados oficiais (`Problema local identificado`, `Possível problema de conectividade`, `Possível falha no anticheat`, `Sanção informada pelo usuário`, `Possível restrição de acesso não confirmada`, `Diagnóstico inconclusivo`).
12. **Integridade do sistema**: Verificação de arquivos nucleares, Component Store (CBS), erros de disco/NTFS, eventos críticos (`Kernel-Power`, `BugCheck`, `WHEA-Logger`) e execução sob confirmação explícita de `DISM /CheckHealth` e `SFC /verifyonly`.
13. **Logs e eventos**: Visualizador integrado dos eventos relevantes do Windows (`System` e `Application`) e dos registros locais da sessão.
14. **Relatórios**: Exportação completa e mascarada nos formatos **PDF**, **HTML**, **JSON** e **TXT**, além do botão **Copiar Relatório Resumido para Suporte Técnico**.
15. **Central de soluções**: Catálogo de procedimentos oficiais organizados por problema encontrado, contendo descrição, causa provável, prioridade, passo a passo, riscos envolvidos e link para documentação oficial.
16. **Configurações**: Controle de mascaramento, tempo limite de consultas, verificação online de status público e diretório de relatórios salvo localmente em JSON (`%LocalAppData%\HWIDDiagnosticPro\settings.json`).

---

## 3. Estrutura do Projeto

```text
HWIDDiagnosticPro/
├── HWIDDiagnosticPro.sln
├── Directory.Build.props
├── src/
│   ├── HWIDDiagnosticPro.App/              # Interface Gráfica WPF (.NET 10 Windows x64, MVVM, Temas)
│   ├── HWIDDiagnosticPro.Core/             # Modelos, Interfaces, Calculadores, Interpretadores e PrivacyMasker
│   ├── HWIDDiagnosticPro.Infrastructure/   # Consultas reais WMI, CIM, Registro, Rede, Segurança, Anticheat e Fortnite
│   └── HWIDDiagnosticPro.Reporting/        # Exportação de relatórios mascarados em PDF, HTML, JSON e TXT
├── tests/
│   ├── UnitTests/                          # Testes unitários (xUnit) de mascaramento, pontuação e análise
│   └── IntegrationTests/                   # Testes de integração fim a fim de diagnóstico e exportação
├── assets/                                 # Ícones e recursos visuais
├── docs/                                   # Documentação técnica de arquitetura e limitações
├── scripts/                                # Scripts PowerShell (.ps1), Bash (.sh) e Inno Setup (.iss)
├── build/                                  # Saída de compilação Release, executável autossuficiente e pacote portátil
├── README.md
└── LICENSE
```

---

## 4. Requisitos e Instruções de Compilação

### Pré-requisitos para Desenvolvimento
- **.NET 10 SDK (LTS)**
- **Windows 10 ou Windows 11 (x64)** para execução completa das APIs nativas de desktop e WMI/CIM.
- Visual Studio 2022/2026 ou Visual Studio Code.

> **Nota para o Usuário Final**: A versão publicada é **autossuficiente (`self-contained`)** para Windows x64 — o usuário final **não** precisa instalar .NET, Python ou qualquer ferramenta de desenvolvimento para executar o `HWIDDiagnosticPro.exe`.

### Compilando Manualmente via CLI

```bash
# 1. Restaurar dependências
dotnet restore HWIDDiagnosticPro.sln

# 2. Compilar toda a solução
dotnet build HWIDDiagnosticPro.sln -c Release

# 3. Executar os testes automatizados
dotnet test HWIDDiagnosticPro.sln -c Release

# 4. Publicar executável autossuficiente (Single-File) para Windows x64
dotnet publish src/HWIDDiagnosticPro.App/HWIDDiagnosticPro.App.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o build/win-x64-self-contained
```

### Utilizando os Scripts Automatizados (`scripts/`)

- **No Windows (PowerShell)**:
  ```powershell
  .\scripts\build-and-publish.ps1
  ```
- **Em Ambientes Linux / CI Multiplataforma (Bash)**:
  ```bash
  chmod +x ./scripts/build-and-publish.sh
  ./scripts/build-and-publish.sh
  ```

---

## 5. Licença

Distribuído sob a licença **MIT**. Consulte o arquivo [LICENSE](LICENSE) para mais informações.