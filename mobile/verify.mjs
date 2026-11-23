import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const modulePath = process.env.GYMZ_PLAYWRIGHT
  ? pathToFileURL(resolve(process.env.GYMZ_PLAYWRIGHT)).href : 'playwright-core';
const { chromium, webkit, devices } = await import(modulePath);
const url = process.env.GYMZ_TEST_URL || 'http://127.0.0.1:4177/gymz/';
const output = resolve(process.env.GYMZ_TEST_OUTPUT || 'work/qa-mobile');
const browserType = process.env.GYMZ_WEBKIT ? webkit : chromium;
const phone = process.env.GYMZ_WEBKIT ? 'iPhone 13' : 'Pixel 7';
await mkdir(output, { recursive: true });
const browser = await browserType.launch({ executablePath: process.env.GYMZ_BROWSER, headless: true });
let page;

try {
  const context = await browser.newContext({
    ...devices[phone], locale: 'pt-BR', timezoneId: 'America/Sao_Paulo', acceptDownloads: true
  });
  page = await context.newPage();
  page.setDefaultTimeout(30_000);
  const date = new Date('2026-11-23T14:00:00Z');
  await page.clock.setFixedTime(date);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const button = (name) => page.getByRole('button', { name, exact: true });
  const heading = (name) => page.getByRole('heading', { name, exact: true });
  const snapshot = (name) => page.screenshot({ scale: 'css', path: resolve(output, phone.replaceAll(' ', '-') + '-' + name + '.png') });
  const state = () => page.evaluate(async () => JSON.parse(await window.gymz.load()));

  await page.goto(url);
  await heading('Monte seu treino').waitFor();
  assert.equal((await state()).Configured, false);
  await page.getByLabel('Nome (opcional)').fill('Gabriel');
  await page.getByLabel('Peso (kg)', { exact: true }).fill('75');
  await page.getByLabel('Altura (cm)', { exact: true }).fill('178');
  for (const day of ['Segunda-feira', 'Terça-feira', 'Quinta-feira', 'Sexta-feira']) await button(day).click();
  await page.getByLabel('Objetivo', { exact: true }).selectOption('recomp');
  await page.getByLabel('Equipamento de musculação').selectOption('machines-dumbbell');
  await page.getByLabel('Equipamento de cardio').selectOption('bike');
  await button('Montar meu plano').click();
  await heading('Treino de hoje').waitFor();
  await heading('Peito e tríceps').waitFor();
  assert.equal(await page.getByRole('navigation').getByRole('button').count(), 5);
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await snapshot('treino');

  const lateCard = page.locator('.movement').nth(3);
  const lateId = await lateCard.getAttribute('id');
  await lateCard.getByRole('button', { name: 'Ver exercício' }).click();
  await heading('Como executar').waitFor();
  await page.evaluate(() => history.back());
  await heading('Treino de hoje').waitFor();
  await page.waitForFunction(id => document.activeElement?.id === id, lateId);

  const exercise = await page.locator('.movement h3').first().textContent();
  const exerciseId = (await page.locator('.movement').first().getAttribute('id')).slice('workout-'.length);
  await button('Ver exercício').first().click();
  await heading(exercise).waitFor();
  await page.waitForFunction(() => [...document.querySelectorAll('.poses img')].length >= 1 &&
    [...document.querySelectorAll('.poses img')].every(img => img.naturalWidth > 0));
  await snapshot('guia');
  await button('Voltar').click();
  await heading('Treino de hoje').waitFor();
  await button('Carga').first().click();
  await heading(exercise).waitFor();
  await button('Salvar séries').click();
  await page.getByRole('alert').filter({ hasText: 'Preencha ou marque pelo menos uma série.' }).waitFor();
  assert.equal((await state()).Sessions['2026-11-23'], undefined);
  await page.getByLabel('Repetições', { exact: true }).first().fill('10');
  await page.getByLabel('Carga (kg)', { exact: true }).first().fill('14,5');
  await page.getByText('Rascunho guardado', { exact: true }).waitFor();
  await button('Voltar').click();
  await heading('Treino de hoje').waitFor();
  await page.reload();
  await heading('Treino de hoje').waitFor();
  await button('Carga').first().click();
  await page.getByText('Rascunho recuperado.', { exact: true }).waitFor();
  assert.equal(await page.getByLabel('Carga (kg)', { exact: true }).first().inputValue(), '14,5');
  await page.setViewportSize({ width: 320, height: 700 });
  await page.getByLabel('Carga (kg)', { exact: true }).first().fill('1501');
  await page.waitForFunction(async id => (await window.gymz.draft('2026-11-23', id))?.rows[0].kg === '1501', exerciseId);
  assert.ok((await button('Salvar séries').boundingBox()).y < 700);
  await button('Salvar séries').click();
  await page.getByRole('alert').filter({ hasText: 'Revise os campos destacados.' }).waitFor();
  assert.equal(await page.getByLabel('Carga (kg)', { exact: true }).first().getAttribute('aria-invalid'), 'true');
  await page.waitForFunction(() => document.activeElement?.id === 'entry-kg-0');
  await page.getByLabel('Carga (kg)', { exact: true }).first().fill('14,5');
  await button('Esforço da última série (opcional)').click();
  await page.getByLabel('Repetições em reserva', { exact: true }).fill('2');
  await snapshot('registro');
  await button('Salvar e concluir').click();
  await heading('Treino de hoje').waitFor();
  let data = await state();
  let session = data.Sessions['2026-11-23'];
  assert.equal(session.Items[0].Done, true);
  assert.equal(session.Items[0].Efforts[0].Kilograms, 14.5);

  assert.equal(await page.locator('.movement').first().getByRole('button', { name: 'Trocar' }).count(), 0);
  await page.locator('.movement').nth(1).getByRole('button', { name: 'Trocar' }).click();
  await heading('Trocar exercício').waitFor();
  const oldId = session.Items[1].Id;
  assert.ok(await page.locator('.swap-suggestions .menu-item').count() <= 3);
  await button('Aparelho ocupado').click();
  await page.getByText('A próxima troca vale só para o treino de hoje.').waitFor();
  await snapshot('troca');
  await button('Trocar automaticamente').click();
  await heading('Treino de hoje').waitFor();
  data = await state();
  session = data.Sessions['2026-11-23'];
  assert.notEqual(session.Items[1].Id, oldId);
  assert.equal(data.Swaps.length, 0);

  const quickCard = page.locator('.movement').nth(1);
  await quickCard.getByRole('button', { name: 'Registrar série', exact: true }).click();
  await quickCard.getByLabel('Repetições', { exact: true }).fill('12');
  await quickCard.getByRole('button', { name: 'Salvar série', exact: true }).click();
  await quickCard.getByText('1 de', { exact: false }).waitFor();
  data = await state();
  assert.equal(data.Sessions['2026-11-23'].Items[1].ActualSets, 1);
  assert.equal(data.Sessions['2026-11-23'].Items[1].Done, false);

  await button('Configurações').click();
  await page.getByLabel('Iniciar ao registrar uma nova série').check();
  await page.waitForFunction(() => localStorage.getItem('gymz-auto-rest') === 'true');
  await button('Treinos').click();
  await heading('Treino de hoje').waitFor();
  await quickCard.getByRole('button', { name: 'Registrar série', exact: true }).click();
  assert.equal(await quickCard.getByLabel('Repetições', { exact: true }).inputValue(), '12');
  await quickCard.getByLabel('Repetições', { exact: true }).fill('13');
  await quickCard.getByText('Rascunho guardado', { exact: true }).waitFor();
  await quickCard.getByRole('button', { name: 'Fechar registro rápido' }).click();
  await quickCard.getByRole('button', { name: 'Registrar série', exact: true }).click();
  assert.equal(await quickCard.getByLabel('Repetições', { exact: true }).inputValue(), '13');
  await quickCard.getByRole('button', { name: 'Salvar série', exact: true }).click();
  await button('Pausar').click();
  const timer = page.getByRole('region', { name: 'Timer de descanso' });
  const paused = Number(await timer.getAttribute('data-seconds'));
  await button('Adicionar 15 segundos').click();
  await page.waitForFunction(value => Number(document.querySelector('.rest-timer')?.dataset.seconds) === value, paused + 15);
  await snapshot('timer');
  await button('Mapa').click();
  await heading('Mapa muscular').waitFor();
  assert.equal(Number(await timer.getAttribute('data-seconds')), paused + 15);
  await page.reload();
  await heading('Treino de hoje').waitFor();
  await timer.waitFor();
  assert.equal(Number(await timer.getAttribute('data-seconds')), paused + 15);
  await button('Retomar').click();
  await button('Encerrar descanso').click();
  await timer.waitFor({ state: 'hidden' });

  await button('Finalizar treino').click();
  await heading('Treino concluído').waitFor();
  await snapshot('concluido');
  await button('Relatórios').click();
  await heading('Relatórios').waitFor();
  await heading('Cobertura muscular').waitFor();
  assert.ok((await page.getByText('Pendente ·', { exact: false }).count()) > 0);
  await snapshot('relatorio');
  const csvPromise = page.waitForEvent('download');
  await button('Exportar CSV').click();
  const csv = await csvPromise;
  await csv.saveAs(resolve(output, phone + '-relatorio.csv'));

  await button('Presença').click();
  await heading('Presença').waitFor();
  await page.getByRole('button', { name: 'Ver treino de 24/11' }).click();
  await heading('Treino do dia').waitFor();
  assert.equal(await button('Treinei').count(), 0);
  assert.equal(await button('Carga').count(), 0);
  await button('Voltar').click();
  await button('Calendário mensal').click();
  await heading('Calendário').waitFor();
  await button('Mês atual').click();
  await snapshot('calendario');
  await button('Mapa').click();
  await heading('Mapa muscular').waitFor();
  await page.getByRole('button', { name: 'Quadríceps', exact: true }).last().click();
  await page.getByLabel('Buscar exercício').fill('agachamento com elastico');
  await page.getByRole('heading', { name: 'Agachamento com elástico' }).waitFor();
  await button('Ver exercício').click();
  await page.waitForFunction(() => [...document.querySelectorAll('.poses img')].every(img => img.naturalWidth > 0));
  await snapshot('agachamento');

  await button('Configurações').click();
  await heading('Configurações').waitFor();
  await button('Claro').click();
  await page.waitForFunction(() => document.querySelector('.shell').dataset.theme === 'light');
  await button('Violeta').click();
  await page.waitForFunction(() => document.querySelector('.shell').dataset.accent === 'violet');
  await button('125%').click();
  await page.getByLabel('Alto contraste').check();
  await page.waitForFunction(() => document.querySelector('.shell').dataset.contrast === 'true');
  await snapshot('configuracoes');
  const backupPromise = page.waitForEvent('download');
  await button('Salvar backup').click();
  const backup = await backupPromise;
  await backup.saveAs(resolve(output, phone + '-backup.json'));

  if (process.env.GYMZ_DESKTOP_BACKUP) {
    await page.locator('input[type=file]').setInputFiles(process.env.GYMZ_DESKTOP_BACKUP);
    await button('Confirmar restauração').click();
    await page.getByText('Backup restaurado.', { exact: true }).waitFor();
    assert.ok(Object.keys((await state()).Sessions).length >= 4);
    await button('Usar a cópia anterior à restauração').click();
    await button('Confirmar restauração').click();
    await page.waitForFunction(async () => JSON.parse(await window.gymz.load()).Sessions['2026-11-23']?.Finalized === true);
  }

  await page.waitForFunction(() => !!navigator.serviceWorker.controller, null, { timeout: 60_000 });
  if (!process.env.GYMZ_WEBKIT) {
    const session = await context.newCDPSession(page);
    const result = await session.send('Page.getInstallabilityErrors');
    // Playwright uses an isolated browsing context, which Chromium flags as private.
    assert.deepEqual(result.installabilityErrors.filter(issue => issue.errorId !== 'in-incognito'), []);
    await session.detach();
  }
  await context.setOffline(true);
  await page.reload();
  await heading('Treino concluído').waitFor();
  assert.equal((await state()).Preferences.Accent, 'violet');
  await button('Mapa').click();
  await page.getByLabel('Buscar exercício').fill('extensao de punho com halter');
  await page.locator('.library-item').filter({ has: heading('Extensão de punho com halter') }).getByRole('button', { name: 'Ver exercício' }).click();
  await page.waitForFunction(() => [...document.querySelectorAll('.poses img')].length >= 1 &&
    [...document.querySelectorAll('.poses img')].every(img => img.naturalWidth > 0));
  await page.clock.setFixedTime(new Date('2026-11-25T14:00:00Z'));
  await page.evaluate(() => window.dispatchEvent(new Event('pageshow')));
  await button('Treinos').click();
  await heading('Descanso').waitFor();
  await button('Montar treino extra').click();
  await heading('Treino extra leve').waitFor();
  assert.equal((await state()).Sessions['2026-11-25'].Complement, true);
  await button('Cancelar extra').click();
  await heading('Descanso').waitFor();
  await button('Mais').click();
  await button('Meu perfil').click();
  await page.getByLabel('Divisão da semana').selectOption('groups');
  await page.getByRole('combobox', { name: 'Segunda-feira', exact: true }).selectOption('chest-biceps');
  await button('Salvar perfil e atualizar plano').click();
  await heading('Treino de hoje').waitFor();
  data = await state();
  assert.equal(data.Profiles.at(-1).Profile.DayThemes[0], 'chest-biceps');
  assert.equal(data.Sessions['2026-11-23'].Items[0].Done, true);
  assert.deepEqual(errors, []);
  console.log(`${phone}: retorno ao exercício, rascunho após reabrir, validação, série rápida, troca temporária, timer, backup e modo offline verificados.`);
} catch (error) {
  await page?.screenshot({ scale: 'css', path: resolve(output, phone.replaceAll(' ', '-') + '-falha.png') }).catch(() => {});
  throw error;
} finally {
  await browser.close();
}
