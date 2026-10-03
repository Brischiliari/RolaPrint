# RolaPrint para Linux — prévia

Interface em C#/.NET 8 e Avalonia, com alinhamento compartilhado com o Windows. Um pequeno processo Python usa as bibliotecas do sistema para os portais Wayland e PipeWire; não usa pip e não envia imagens pela rede.

O pacote é destinado a **Pop!_OS 24.04/COSMIC, Intel/AMD de 64 bits**. O runtime .NET vem incluído. Instale o `.deb` das Releases com:

```sh
sudo apt install ./RolaPrint-linux-amd64.deb
```

Abra RolaPrint pelo menu. Autorize um monitor e o controle do mouse no seletor do sistema. Marque a área que rola arrastando na prévia. Mantenha o documento no início e visível. Ao iniciar, o aplicativo minimiza e espera 1,8 segundos. Mova o painel Parar para fora da região selecionada. Clique nele para finalizar e salvar o PNG.

## Limitações desta prévia

- É uma implementação nova: compilação automatizada não substitui teste real no COSMIC.
- A versão instalada do portal COSMIC precisa oferecer RemoteDesktop, ScreenCast e os comandos NotifyPointer. Se não oferecer, o aplicativo informa o erro; não tenta contornar as permissões do compositor.
- Se faltar informação estática para alinhar (vídeos grandes, telas uniformes), a coleta pausa. Parar mantém a imagem já coletada.
- O aplicativo captura o monitor autorizado: selecione uma região que exclua barras, painéis e outras janelas.
- Esc funciona quando uma janela do RolaPrint tem foco. Para interromper enquanto outro aplicativo está ativo, use o painel Parar.
- Máximo de 40 megapixels ou 30.000 pixels de altura por imagem.

Desenvolvimento: `dotnet run --project linux/RolaPrint.Linux`. O workflow Linux package compila a aplicação e publica o `.deb` como pré-lançamento.
