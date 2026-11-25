Blazor.start({ applicationCulture: 'pt-BR' }).catch(() => {
  const loadingMessage = document.querySelector('.loading span');
  if (loadingMessage) loadingMessage.textContent = 'Falha ao abrir o GymZ. Recarregue a página.';
});
