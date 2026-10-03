# RolaPrint — C#

Dê dois cliques em **RolaPrint.exe**. O executável contém a interface e a captura em C#: não inicia PowerShell nem CMD e não precisa de arquivos de código ao lado para executar. Usa o .NET Framework 4.5 ou superior instalado no Windows.

## Usar

1. Abra o documento no início.
2. Selecione a região que rola, evitando barras de ferramentas e a barra de tarefas.
3. Clique em Iniciar captura. Não mova nem redimensione a janela selecionada.
4. Pressione Esc ou clique em Parar e ver resultado.
5. Confira a prévia e salve em PNG.

A captura usa automaticamente o modo Turbo, com rolagem adaptativa e pausas de 120 + 60 ms. Se o conteúdo ainda estiver em movimento, o aplicativo aguarda novas tentativas antes de avançar.

O painel Parar fica fora da seleção. Se não houver espaço, use Esc. A sessão não termina automaticamente ao chegar ao fim visual. Instabilidade, perda de foco e alinhamento incerto pausam a coleta. Ao atingir 40 megapixels ou 30.000 pixels de altura, a coleta pausa até você parar.

## Desenvolvimento

- NativeApp.cs: interface Windows Forms, seleção, parada manual e coleta em segundo plano.
- CaptureEngine.cs: captura, rolagem, alinhamento e escolha de emendas.
- Build.ps1: compila com o compilador C# do .NET Framework instalado no Windows.
- Test-Capture.ps1: testes de alinhamento, montagem, elementos fixos e interface compilada.

Feche o aplicativo antes de recompilar. Execute:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Test-Capture.ps1
```

O PowerShell é usado apenas como ferramenta de desenvolvimento e teste. O aplicativo compilado não depende dele. O antigo RolaPrint.ps1 foi preservado como protótipo anterior. Os iniciadores .vbs e .bat encaminham para o novo executável.

## Limitações

Vídeos e animações são identificados pela mudança entre duas capturas e ignorados no alinhamento, incluindo suas bordas; continuam visíveis no PNG como imagem estática. A escolha da emenda também evita essas regiões quando há espaço. Não existe bloqueio por porcentagem de movimento: o algoritmo procura detalhes estáticos mesmo com vídeos grandes. Se não houver detalhes estáticos suficientes, ainda pode aguardar: não é possível garantir alinhamento de uma região composta só por vídeo. Os testes incluem vídeos simulados pequenos e grandes com rolagem e sem rolagem.

O alinhamento precisa de conteúdo em comum entre imagens. Animações, grandes elementos fixos, áreas uniformes, rolagem horizontal e monitores com escalas diferentes podem impedir a coleta. Os testes automatizados usam imagens sintéticas; a migração ainda precisa de validação manual no Word e nos sites usados pelo usuário. A prévia ajusta a imagem à janela, sem zoom interativo. PDF e texto pesquisável não estão implementados.

Tudo é processado localmente. O cursor é restaurado ao finalizar; a posição de rolagem do documento muda. Diagnósticos ficam em %LOCALAPPDATA%\RolaPrint\RolaPrint.log.

## Linux

A prévia para Pop!_OS 24.04/COSMIC (amd64) está nas Releases: https://github.com/Brischiliari/RolaPrint/releases . Consulte linux/README.md para instalação e limitações. A captura real no COSMIC ainda precisa de validação.
