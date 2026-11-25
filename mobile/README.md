# GymZ para Android e iPhone

O GymZ móvel é um aplicativo web instalável. Na primeira abertura, ele baixa o app, os desenhos e o catálogo. Depois que **Configurações** indica que o conteúdo está pronto, ele reabre sem internet. Os registros ficam no aparelho, sem conta ou assinatura.

**[Abrir GymZ no celular](https://jordaozz.github.io/GymZ/)**

- **Android**. Abra o endereço HTTPS no Chrome e use **Instalar app** no menu ou em **Configurações** do GymZ quando o navegador oferecer a opção.
- **iPhone**. Abra o endereço HTTPS no Safari, toque em **Compartilhar → Adicionar à Tela de Início** e selecione **Abrir como App da Web** quando disponível.
- **Treinos** abre a lista de hoje. Consulte datas em **Presença** e use **Mapa** ou **Relatórios** para explorar exercícios e cobertura. **Mais** reúne Visão geral, Meu perfil e Configurações.
- Esqueceu de registrar um treino? Em **Presença**, abra o dia da semana atual para anotar presença, séries e observações. A semana anterior fica disponível para consulta.
- Use **Registrar série** no cartão ou **Carga** para editar várias séries. Os rascunhos ficam salvos por dia no aparelho nesta semana. **Salvar séries** grava os valores e **Salvar e concluir** também marca o exercício como feito.
- Toque no intervalo do exercício para iniciar o timer. Em **Configurações**, você também pode iniciá-lo automaticamente ao salvar uma nova série. Em **Trocar**, selecione **Aparelho ocupado** para aplicar a escolha só naquele treino.
- Para levar seus dados do Windows, copie o arquivo gymz-backup.json da pasta de backup e importe em **Configurações → Restaurar backup**. O backup exportado no celular também abre no Windows. Cada restauração preserva uma cópia anterior no aparelho.

## Gerar e abrir a versão

Use o SDK .NET 10 na raiz do projeto. O motor, o catálogo, os desenhos do mapa, os exemplos de equipamentos e o formato de backup são compartilhados com a versão Windows.

```powershell
dotnet publish mobile/GymZ.Mobile.csproj -c Release -o work/mobile-build
node mobile/serve.mjs work/mobile-build/wwwroot
```

A prévia local abre em `http://127.0.0.1:4177/gymz/`. Para instalar em celulares, sirva o conteúdo de `wwwroot` em um endereço HTTPS, mantendo as pastas e os arquivos com seus nomes originais. O pacote estático funciona em um subdiretório, incluindo no GitHub Pages. Inclua `.nojekyll` para que o Pages preserve a pasta `_framework`.

Para verificar os fluxos em um navegador, mantenha a prévia aberta e execute `node mobile/verify.mjs` com Playwright disponível. `GYMZ_BROWSER` pode apontar para o executável do Chromium ou Edge, e `GYMZ_WEBKIT=1` usa o WebKit do Playwright. `GYMZ_DESKTOP_BACKUP` pode apontar para um backup de teste do Windows.
