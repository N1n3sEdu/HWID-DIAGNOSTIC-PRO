# Arquitetura Técnica, Consultas do Windows e Limitações do Diagnóstico

## 1. Visão Geral da Arquitetura (MVVM + Clean Architecture)

O **HWID Diagnostic Pro** foi projetado em **.NET 10 LTS** com interface gráfica **WPF** para **Windows 10 e Windows 11 (x64)**, dividido em quatro projetos principais:

- **`HWIDDiagnosticPro.Core`**: Modelos de domínio, classificações oficiais (`NORMAL`, `ATENÇÃO`, `ERRO`, `CRÍTICO`, `INDISPONÍVEL`, `INCONCLUSIVO`), calculador de pontuação de integridade local (`0 a 100`), interpretador de erros documentados do Fortnite/Anticheat, analisador ético de possíveis restrições de acesso, catálogo da Central de Soluções e motor de mascaramento de privacidade (`PrivacyMasker`).
- **`HWIDDiagnosticPro.Infrastructure`**: Implementação das consultas reais de sistema utilizando WMI (`System.Management`), CIM (`root\CIMV2`, `root\WMI`, `root\Microsoft\Windows\Storage`, `root\CIMV2\Security\MicrosoftTpm`, `root\Microsoft\Windows\Defender`), Registro do Windows (`Microsoft.Win32.Registry`), `ServiceController`, `NetworkInterface`, `Ping`, `Dns`, `DriveInfo`, `X509Certificate` (Authenticode) e `EventLog`.
- **`HWIDDiagnosticPro.Reporting`**: Geração de relatórios técnicos nos formatos **PDF** (gerador nativo PDF 1.4 multipágina com codificação WinAnsi para português brasileiro), **HTML** (tema escuro responsivo), **JSON** e **TXT**, além do resumo executivo para suporte técnico com mascaramento automático.
- **`HWIDDiagnosticPro.App`**: Aplicação desktop WPF em padrão **MVVM** com injeção de dependências (`Microsoft.Extensions.DependencyInjection`), barra de progresso assíncrona real, cancelamento via `CancellationToken` e 16 módulos de navegação.

## 2. Consultas Oficiais do Windows Utilizadas

| Módulo | Fonte Oficial / API do Windows |
| :--- | :--- |
| **Placa-mãe** | `Win32_BaseBoard` (`Manufacturer`, `Product`, `Version`, `SerialNumber`) e `Win32_BIOS` (`SMBIOSMajorVersion`, `SMBIOSMinorVersion`) |
| **BIOS e UEFI** | `Win32_BIOS`, `Win32_ComputerSystemProduct` (`UUID`), `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State` (`UEFISecureBootEnabled`) |
| **Processador** | `Win32_Processor`, `MSAcpi_ThermalZoneTemperature` (`root\WMI`), `Win32_PerfFormattedData_Counters_ThermalZoneInformation` |
| **Memória RAM** | `Win32_PhysicalMemory` (`Capacity`, `ConfiguredClockSpeed`, `SMBIOSMemoryType`, `DeviceLocator`, `BankLabel`) |
| **SSD e HDD** | `Win32_DiskDrive`, `MSFT_PhysicalDisk` (`root\Microsoft\Windows\Storage`), `DriveInfo.GetDrives()` |
| **Placa de Vídeo** | `Win32_VideoController`, verificação de `d3d12.dll` / `d3d11.dll` em `System32` |
| **Rede e Internet** | `NetworkInterface.GetAllNetworkInterfaces()`, `Ping`, `Dns.GetHostAddressesAsync`, Registro (`Internet Settings`, `FirewallPolicy`), serviço `W32Time`, endpoint público `status.epicgames.com` |
| **Segurança do Windows** | `Win32_Tpm`, `MSFT_MpComputerStatus`, `DeviceGuard` (HVCI / VBS), `Win32_QuickFixEngineering`, `SystemStartOptions` (`TESTSIGNING` / `DEBUG`), UAC (`EnableLUA`), Pending Reboot (`CBS` / `WindowsUpdate`) |
| **Drivers e Serviços** | `Win32_PnPSignedDriver`, `Win32_Service`, `Win32_PnPEntity` (`ConfigManagerErrorCode <> 0`), Event Log `System` |
| **Anticheat** | Inspeção somente-leitura de serviços (`EasyAntiCheat`, `EasyAntiCheat_EOS`, `BEService`, `EpicOnlineServices`), `FileVersionInfo`, certificados Authenticode e logs públicos |
| **Fortnite** | `LauncherInstalled.dat`, manifestos `*.item` da Epic Games, verificação de binários e pacotes `.pak`, Visual C++ Redistributable (`VC\Runtimes\x64`), `FortniteGame.log` |
| **Integridade do Sistema** | `EventLog` (`System` e `Application`), `dism.exe /Online /Cleanup-Image /CheckHealth` e `sfc.exe /verifyonly` (sob confirmação explícita) |

## 3. Segurança, Ética e Limitações Documentadas

1. **Operação Estritamente Somente Leitura**: O software nunca modifica números de série, MAC, UUID ou chaves protegidas do sistema, não instala drivers de kernel próprios e não interfere em processos do Fortnite, Easy Anti-Cheat ou BattlEye.
2. **Sem Diagnósticos Especulativos de Banimento**: Nenhum software local possui acesso aos bancos de dados de sanções da Epic Games. Por isso, o programa jamais afirma que uma peça (placa-mãe, SSD, CPU, etc.) está banida sem confirmação oficial e não inventa porcentagens de probabilidade de banimento HWID.
3. **Elevação de Privilégio (`asInvoker`)**: O manifesto da aplicação solicita nível `asInvoker` para não exigir privilégios administrativos desnecessários na abertura. Consultas que dependem de elevação (como `SFC /verifyonly`, `DISM /CheckHealth` ou propriedades completas do `Win32_Tpm` em algumas políticas) exibem status claro ou fallback via Registro quando executadas sem elevação.
