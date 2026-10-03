# RolaPrint para Pop!_OS — preparação

O executável Windows não funciona diretamente no Linux. A versão Linux ainda não foi implementada nem validada.

## Dependências a substituir

- Windows Forms e System.Drawing: interface Avalonia e armazenamento de pixels portátil.
- user32.dll: aquisição de frames e controle de rolagem próprios do ambiente Linux.
- GetAsyncKeyState: cancelamento via interface e atalhos suportados pela sessão.
- DWM: aparência controlada pelo toolkit e pelo compositor.

## Sessão gráfica

Em X11, captura de tela e entrada podem ser integradas com Xlib e XTest. Em Wayland, não se deve presumir que a captura e a entrada em janelas XWayland concedam acesso ao desktop inteiro. O caminho de integração é uma sessão autorizada via XDG Desktop Portal (ScreenCast/RemoteDesktop), com aquisição dos frames por PipeWire. A disponibilidade das interfaces deve ser consultada no ambiente real.

COSMIC é um desktop Wayland. É necessário confirmar a versão e a sessão do Pop!_OS antes de escolher o backend de captura.

## Reaproveitamento

Match, Anchors, Consensus, DetectMotion e Seam podem ser portados para operar sobre buffers RGB em vez de Bitmap. O controle da sessão precisa ser separado da interface e dos recursos do Windows. Os testes com vídeo, legendas escassas e barra fixa devem ser mantidos e executados com o mesmo conteúdo no backend portátil.

## Referências oficiais

- https://system76.com/cosmic
- https://docs.avaloniaui.net/docs/deployment/linux
- https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.RemoteDesktop.html

Nenhum pacote Linux foi gerado nesta preparação.
